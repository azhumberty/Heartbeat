using Godot;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

/// <summary>Free OpenRouter chat. Discovers live :free models so dead slugs don't break the game.</summary>
public static class OpenRouterClient
{
    public const string Url = "https://openrouter.ai/api/v1/chat/completions";
    public const string ModelsUrl = "https://openrouter.ai/api/v1/models";
    public const string DefaultModel = "z-ai/glm-5.2:free";
    static readonly string[] Seed =
    {
        "z-ai/glm-5.2:free",
        "google/gemma-4-31b-it:free",
        "nvidia/nemotron-3-ultra-550b-a55b:free",
        "inclusionai/ling-3.0-flash-fin:free",
        "meta-llama/llama-3.2-3b-instruct:free",
        "openrouter/free"
    };
    static string[] _live = Array.Empty<string>();
    static DateTime _liveAt = DateTime.MinValue;
    public static string LastModel { get; private set; } = DefaultModel;

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
        if (model.Length == 0 || IsDeadSlug(model)) return DefaultModel;
        return model;
    }

    static bool IsDeadSlug(string model) =>
        model.Contains("dolphin-mistral-24b-venice", StringComparison.OrdinalIgnoreCase)
        || model.Contains("mistral-small-3.1", StringComparison.OrdinalIgnoreCase)
        || model.Contains("gpt-oss-20b:free", StringComparison.OrdinalIgnoreCase);

    public static async Task<string> CompleteJson(GameSettings settings, string user, int maxTokens, CancellationToken token = default)
    {
        var key = Key(settings);
        if (key.Length < 8) throw new InvalidOperationException("Cole a chave OpenRouter em Opcoes.");
        await RefreshLive(key, token);
        Exception? last = null;
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in ModelsToTry(settings))
        {
            if (!tried.Add(model)) continue;
            try
            {
                var content = await Post(key, model, user, maxTokens, true, token);
                LastModel = model;
                if (settings.OpenRouterModel != model) settings.OpenRouterModel = model;
                return content;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                last = e;
                GD.Print("[OpenRouter] " + model + " falhou: " + Short(e));
                if (IsUnavailableFree(e)) continue;
                try
                {
                    var content = await Post(key, model, user, maxTokens, false, token);
                    LastModel = model;
                    if (settings.OpenRouterModel != model) settings.OpenRouterModel = model;
                    return content;
                }
                catch (Exception e2) when (e2 is not OperationCanceledException)
                {
                    last = e2;
                    GD.Print("[OpenRouter] " + model + " (texto) falhou: " + Short(e2));
                }
            }
        }
        throw last ?? new InvalidOperationException("OpenRouter sem modelo gratis disponivel.");
    }

    static IEnumerable<string> ModelsToTry(GameSettings settings)
    {
        yield return Model(settings);
        foreach (var m in _live) yield return m;
        foreach (var m in Seed) yield return m;
    }

    static async Task RefreshLive(string key, CancellationToken token)
    {
        if ((DateTime.UtcNow - _liveAt).TotalMinutes < 10 && _live.Length > 0) return;
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await client.GetAsync(ModelsUrl, token);
            var raw = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode) return;
            using var json = JsonDocument.Parse(raw);
            if (!json.RootElement.TryGetProperty("data", out var data)) return;
            var found = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (id.Length < 4) continue;
                var free = id.EndsWith(":free", StringComparison.OrdinalIgnoreCase);
                if (!free && item.TryGetProperty("pricing", out var pricing)
                    && pricing.TryGetProperty("prompt", out var prompt))
                {
                    var p = prompt.ToString().Trim();
                    free = p is "0" or "0.0" or "0.00";
                }
                if (!free) continue;
                var lower = id.ToLowerInvariant();
                if (lower.Contains("-vl") || lower.Contains("vision") || lower.Contains("image")) continue;
                found.Add(id);
                if (found.Count >= 8) break;
            }
            if (found.Count > 0) { _live = found.ToArray(); _liveAt = DateTime.UtcNow; GD.Print("[OpenRouter] modelos gratis: " + string.Join(", ", _live)); }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            GD.Print("[OpenRouter] catalogo: " + e.GetType().Name);
        }
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
            throw new HttpRequestException($"OpenRouter HTTP {(int)response.StatusCode}: {raw[..Math.Min(raw.Length, 280)]}");
        using var json = JsonDocument.Parse(raw);
        var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        content = content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start >= 0 && end > start) content = content[start..(end + 1)];
        return content;
    }

    static bool IsUnavailableFree(Exception e) =>
        e.Message.Contains("unavailable for free", StringComparison.OrdinalIgnoreCase)
        || e.Message.Contains("No endpoints found", StringComparison.OrdinalIgnoreCase);

    static string Short(Exception e)
    {
        var t = (e.GetType().Name + ": " + e.Message).Replace('\n', ' ').Replace('\r', ' ');
        return t[..Math.Min(t.Length, 160)];
    }
}
