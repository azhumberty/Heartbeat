using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Heartbeat;

/// <summary>
/// Dialogue provider that calls OpenRouter.ai — a free gateway to hundreds of
/// language models, including uncensored ones (Dolphin, WizardLM, etc.).
///
/// Setup: Set the environment variable  OPENROUTER_API_KEY  to a free key from
///        https://openrouter.ai  (no credit card required for free-tier models).
///
/// Model: Controlled by GameSettings.OpenRouterModel  (default: "mistralai/mistral-7b-instruct:free")
///        For uncensored / dark-fantasy content use:
///          - "cognitivecomputations/dolphin-mixtral-8x7b"
///          - "nousresearch/nous-hermes-2-mixtral-8x7b-dpo"
/// </summary>
public sealed class OpenRouterDialogueProvider : IDialogueProvider
{
    private const string BaseUrl = "https://openrouter.ai/api/v1/chat/completions";
    // Free uncensored model that works without credits
    private const string DefaultModel = "mistralai/mistral-7b-instruct:free";

    private readonly ProceduralDialogueProvider _fallback = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

    public async Task<DialogueResult> ReplyAsync(
        CharacterData c, CharacterState s, string input,
        GameSettings settings, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return Fallback("IA online indisponível: OPENROUTER_API_KEY ausente. Usando diálogo offline.");

        var apiKey = System.Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")!;
        var model = !string.IsNullOrWhiteSpace(settings.OpenRouterModel)
            ? settings.OpenRouterModel
            : DefaultModel;

        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            // OpenRouter requires these headers for free tier
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/azhumberty/Heartbeat");
            client.DefaultRequestHeaders.Add("X-Title", "Heartbeat Dating Sim");

            var prompt = CharacterPromptBuilder.Build(c, s, input, settings);
            var request = new Dictionary<string, object>
            {
                ["model"] = model,
                ["temperature"] = Math.Clamp(settings.Temperature, 0f, 1.4f),
                ["max_tokens"] = Math.Clamp(settings.MaxResponseTokens, 128, 600),
                ["response_format"] = new { type = "json_object" },
                ["messages"] = new[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user",   content = input }
                }
            };

            var payload = JsonSerializer.Serialize(request);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(BaseUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"OpenRouter HTTP {(int)response.StatusCode}: {err[..Math.Min(err.Length, 200)]}");
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var msgContent = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            msgContent = msgContent.Trim().TrimStart('`').TrimEnd('`')
                .Replace("json\n", "", StringComparison.OrdinalIgnoreCase);

            var result = JsonSerializer.Deserialize<DialogueResult>(
                msgContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null || string.IsNullOrWhiteSpace(result.Dialogue))
                throw new JsonException("Empty OpenRouter response");

            result.ProviderStatus = $"Online · OpenRouter ({model})";
            GD.Print($"[AI] OpenRouter responded ({model})");
            return DialogueValidator.Sanitize(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            var detail = (e.GetType().Name + ": " + e.Message).Replace('\n', ' ').Replace('\r', ' ');
            detail = detail[..Math.Min(detail.Length, 240)];
            return Fallback(detail + " · usando offline");
        }

        DialogueResult Fallback(string reason)
        {
            GD.Print("[OpenRouter] " + reason);
            var r = _fallback.Reply(c, s, input);
            r.ProviderStatus = reason;
            return r;
        }
    }
}
