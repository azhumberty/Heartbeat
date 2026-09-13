using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

/// <summary>Free OpenRouter chat. Key lives in Opções (user://) or OPENROUTER_API_KEY.</summary>
public static class OpenRouterClient
{
    public const string Url = "https://openrouter.ai/api/v1/chat/completions";
    public const string DefaultModel = "cognitivecomputations/dolphin-mistral-24b-venice-edition:free";

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
        return model.Length > 0 ? model : DefaultModel;
    }

    public static async Task<string> CompleteJson(GameSettings settings, string user, int maxTokens, CancellationToken token = default)
    {
        var key = Key(settings);
        if (key.Length < 8) throw new InvalidOperationException("Cole a chave OpenRouter em Opcoes.");
        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(28) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        client.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://github.com/azhumberty/Heartbeat");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", "Heartbeat Dating Sim");
        var request = new Dictionary<string, object>
        {
            ["model"] = Model(settings),
            ["temperature"] = Math.Clamp(settings.Temperature, 0f, 1.4f),
            ["max_tokens"] = Math.Clamp(maxTokens, 128, 900),
            ["response_format"] = new { type = "json_object" },
            ["messages"] = new[] { new { role = "user", content = user } }
        };
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
