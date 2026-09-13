using Godot;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

/// <summary>Free OpenRouter chat. Key lives in Opções (user://). Models rotate because free slugs disappear.</summary>
public static class OpenRouterClient
{
    public const string Url = "https://openrouter.ai/api/v1/chat/completions";
    public const string DefaultModel = "openrouter/free";
    static readonly string[] Fallbacks =
    {
        "openrouter/free",
        "z-ai/glm-5.2:free",
        "google/gemma-4-31b-it:free",
        "inclusionai/ling-3.0-flash-fin:free"
    };

    public static string Key(GameSettings? settings)
    {
        var fromSave = settings?.OpenRouterApiKey?.Trim() ?? "";
        if (fromSave.Length > 0) return fromSave;
        return (System.Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "").Trim();
    }

    public static bool HasKey(GameSettings? settings) => Key(settings).Length > 8;

    public static string Model(GameSettings? settings)
    {
        var model = settings?.OpenRouterModel?.Trim() ?? "";
        if (model.Length == 0 || model.Contains("dolphin-mistral-24b-venice", StringComparison.OrdinalIgnoreCase) || model.Contains("mistral-7b-instruct", StringComparison.OrdinalIgnoreCase))
            return DefaultModel;
        return model;
    }

    public static async Task<string> CompleteJson(GameSettings settings, string user, int maxTokens, CancellationToken token = default)
    {
        var key = Key(settings);
        if (key.Length < 8) throw new InvalidOperationException("Cole a chave OpenRouter em Opcoes.");
        Exception? last = null;
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in ModelsToTry(settings))
        {
            if (!tried.Add(model)) continue;
            try { return await Post(key, model, user, maxTokens, true, token); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                last = e;
                GD.Print("[OpenRouter] " + model + " falhou: " + e.GetType().Name);
                try { return await Post(key, model, user, maxTokens, false, token); }
                catch (Exception e2) when (e2 is not OperationCanceledException)
                {
                    last = e2;
                }
            }
        }
        throw last ?? new InvalidOperationException("OpenRouter sem resposta.");
    }

    static IEnumerable<string> ModelsToTry(GameSettings settings)
    {
        yield return Model(settings);
        foreach (var f in Fallbacks) yield return f;
    }

    static async Task<string> Post(string key, string model, string user, int maxTokens, bool jsonMode, CancellationToken token)
    {
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(28) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        client.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://github.com/azhumberty/Heartbeat");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", "Heartbeat Dating Sim");
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["temperature"] = 0.85f,
            ["max_tokens"] = Math.Clamp(maxTokens, 128, 900),
            ["messages"] = new[] { new { role = "user", content = user } }
        };
        if (jsonMode) request["response_format"] = new { type = "json_object" };
        using var response = await client.PostAsync(Url, new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"), token);
        var raw = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenRouter HTTP {(int)response.StatusCode}: {raw[..Math.Min(raw.Length, 180)]}");
        using var json = JsonDocument.Parse(raw);
        var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        content = content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
        return content;
    }
}
