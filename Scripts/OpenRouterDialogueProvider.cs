using Godot;
using System.Text.Json;

namespace Heartbeat;

/// <summary>OpenRouter dialogue. Key is pasted in Opções or OPENROUTER_API_KEY. Default model is Dolphin Venice uncensored (free).</summary>
public sealed class OpenRouterDialogueProvider : IDialogueProvider
{
    readonly ProceduralDialogueProvider _fallback = new();

    public async Task<DialogueResult> ReplyAsync(
        CharacterData c, CharacterState s, string input,
        GameSettings settings, CancellationToken cancellationToken = default)
    {
        if (!OpenRouterClient.HasKey(settings))
            return Fallback("Cole a chave OpenRouter em Opcoes (gratis em openrouter.ai/keys). Usando dialogo offline.");

        var model = OpenRouterClient.Model(settings);
        try
        {
            var prompt = CharacterPromptBuilder.Build(c, s, input, settings);
            var user = prompt + "\n\nMENSAGEM DO JOGADOR:\n" + input;
            var content = await OpenRouterClient.CompleteJson(settings, user, Math.Clamp(settings.MaxResponseTokens, 128, 600), cancellationToken);
            var result = JsonSerializer.Deserialize<DialogueResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null || string.IsNullOrWhiteSpace(result.Dialogue))
                throw new JsonException("Empty OpenRouter response");
            result.ProviderStatus = "Online · OpenRouter (" + OpenRouterClient.LastModel + ")";
            GD.Print("[AI] OpenRouter responded (" + model + ")");
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
