using Godot;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

public interface IDialogueProvider { Task<DialogueResult> ReplyAsync(CharacterData character, CharacterState state, string playerText, GameSettings settings, CancellationToken cancellationToken = default); }
public sealed class DialogueResult
{
    [System.Text.Json.Serialization.JsonIgnore] public string ProviderStatus { get; set; } = "Offline · resposta procedural";
    public string Dialogue { get; set; } = ""; public string Emotion { get; set; } = "neutral"; public string Desire { get; set; } = "talk";
    public int AffectionDelta { get; set; } public int TrustDelta { get; set; } public int RomanceDelta { get; set; } public int AttractionDelta { get; set; } public int EnergyDelta { get; set; } public int StressDelta { get; set; } public string Memory { get; set; } = ""; public bool ImportantMemory { get; set; }
}
public sealed class ProceduralDialogueProvider : IDialogueProvider
{
    readonly ConsistentOfflineDialogueProvider _provider=new();
    public DialogueResult Reply(CharacterData c,CharacterState s,string input)=>_provider.Reply(c,s,input);
    public Task<DialogueResult> ReplyAsync(CharacterData c,CharacterState s,string input,GameSettings settings,CancellationToken cancellationToken=default)=>_provider.ReplyAsync(c,s,input,settings,cancellationToken);
}

// Kept as an implementation reference while old saves and tests transition to the contextual provider.
internal sealed class LegacyProceduralDialogueProvider : IDialogueProvider
{
    readonly Random _random = new();
    public DialogueResult Reply(CharacterData c, CharacterState s, string input)
    {
        var lower = input.ToLowerInvariant(); var kind = lower.Contains("presente") || lower.Contains("flor") ? "gift" : lower.Contains("como") || lower.Contains("tudo bem") ? "care" : "talk";
        if (new[] { "não quero", "nao quero", "pare", "ir devagar", "sem pressa" }.Any(lower.Contains))
            return new DialogueResult { Dialogue = "Tudo bem. Vamos no seu ritmo; podemos só conversar.", Emotion = "neutral", Desire = "talk", TrustDelta = 1, Memory = "O jogador pediu para respeitar seu ritmo." };
        if (new[] { "flert", "beij", "bonito", "atraente", "gosto de você", "gosto de voce", "namor", "intimidade", "sexo" }.Any(lower.Contains))
        {
            var comfortable = c.Age >= 18 && s.Trust >= 30 && s.Affection >= 35 && s.Stress < 65 && s.Energy >= 25;
            return new DialogueResult {
                Dialogue = comfortable ? (c.Personality.Contains("confiante") ? "Você chamou minha atenção também. Quero me aproximar, se for bom para nós dois." : "Fico um pouco tímido, mas gosto de saber disso. Podemos nos aproximar com calma.") : "Prefiro conhecer você melhor antes de dar esse passo. Vamos com calma?",
                Emotion = comfortable ? "flirty" : "shy", Desire = comfortable ? "flirt" : "talk",
                AffectionDelta = comfortable ? 1 : 0, RomanceDelta = comfortable ? 1 : 0,
                AttractionDelta = comfortable ? 1 : 0, Memory = "Conversaram sobre aproximação e respeitar o ritmo de ambos." };
        }
        var interest = c.Interests.Count > 0 ? c.Interests[_random.Next(c.Interests.Count)] : "coisas simples";
        var lines = kind switch
        {
            "gift" => new[] { "Você trouxe isso para mim? Eu vou guardar com carinho.", "Que gesto bonito... obrigado por pensar em mim." },
            "care" => new[] { "Estou melhor agora que você perguntou. Acho que queria falar um pouco.", "Hoje estou meio pensativo, mas sua companhia ajuda." },
            _ => new[] { $"Isso me faz pensar em {interest}. Quer ouvir uma ideia que tive?", c.Personality.Contains("confiante") ? "Você sempre chega assim, me deixando curioso? Senta aqui comigo." : "Eu gosto quando a conversa pode ir devagar. Parece mais verdadeira." }
        };
        var positive = kind != "talk" || _random.NextDouble() > .35;
        var line = s.Energy < 30 ? "Estou cansado hoje. Podemos descansar juntos um pouco?" : lines[_random.Next(lines.Length)];
        if (lower.Contains("trabalho")) line = $"Depois do trabalho gosto de vir aqui e pensar em {interest}. Você também tem um lugar assim?";
        if (lower.Contains("parque")) line = "Uma caminhada no parque parece boa. Vamos quando estiver mais tranquilo?";
        return new DialogueResult { Dialogue = line, Emotion = s.Energy < 30 ? "tired" : positive ? "happy" : "neutral", Desire = positive ? "spend_time" : "talk", AffectionDelta = positive ? 2 : 1, TrustDelta = 2, EnergyDelta = -1, Memory = $"O jogador disse: {input[..Math.Min(input.Length, 120)]}" };
    }
    public Task<DialogueResult> ReplyAsync(CharacterData c, CharacterState s, string input, GameSettings settings, CancellationToken cancellationToken = default) => Task.FromResult(Reply(c, s, input));
}

public sealed class GroqDialogueProvider : IDialogueProvider
{
    readonly HttpMessageHandler? _handler;
    public GroqDialogueProvider(HttpMessageHandler? handler = null) { _handler = handler; }
    readonly ProceduralDialogueProvider _fallback = new();
    public bool IsConfigured => !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
    public async Task<DialogueResult> ReplyAsync(CharacterData c, CharacterState s, string input, GameSettings settings, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return Fallback("IA online indisponível: GROQ_API_KEY ausente. Usando diálogo offline.");
        try
        {
            if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || endpoint.Host != "api.groq.com") return Fallback("Endpoint Groq inválido. Usando diálogo offline.");
            using var client = _handler == null ? new System.Net.Http.HttpClient() : new System.Net.Http.HttpClient(_handler, false);
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
            var prompt = CharacterPromptBuilder.Build(c,s,input,settings);
            bool gptOss=settings.Model.StartsWith("openai/gpt-oss",StringComparison.OrdinalIgnoreCase);
            int completionTokens=gptOss?Math.Clamp(settings.MaxResponseTokens*3,512,1024):Math.Clamp(settings.MaxResponseTokens,128,512);
            object responseFormat=gptOss?StrictDialogueFormat():new { type="json_object" };
            var request=new Dictionary<string,object>
            {
                ["model"]=settings.Model,
                ["temperature"]=Math.Clamp(settings.Temperature,0f,1.2f),
                ["max_completion_tokens"]=completionTokens,
                ["response_format"]=responseFormat,
                // GPT-OSS works more reliably when instructions and input share the user turn.
                ["messages"]=new[] { new { role="user",content=prompt+"\n\nMENSAGEM DO JOGADOR:\n"+input } }
            };
            if(gptOss) { request["reasoning_format"]="hidden"; request["reasoning_effort"]="low"; }
            var payload=JsonSerializer.Serialize(request);
            using var response = await PostWithRetry(client, settings.Endpoint.TrimEnd('/') + "/chat/completions", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string detail=await ReadErrorAsync(response,cancellationToken);
                throw new HttpRequestException($"Groq HTTP {(int)response.StatusCode}: {detail}");
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var choice = json.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            if ((message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(refusal.GetString()))
                || (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() == "content_filter"))
                return Fallback("Restrição informada pelo provedor · resposta alternativa offline");
            var content = message.GetProperty("content").GetString() ?? "{}";
            content = content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
            var result = JsonSerializer.Deserialize<DialogueResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null || string.IsNullOrWhiteSpace(result.Dialogue)) throw new JsonException("Empty AI response"); result.ProviderStatus = "Online · Groq respondeu"; GD.Print("[AI] Using Groq provider"); return DialogueValidator.Sanitize(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            string detail=(e.GetType().Name+": "+e.Message).Replace('\n',' ').Replace('\r',' ');
            detail=detail[..Math.Min(detail.Length,240)];
            return Fallback(detail+" · usando offline");
        }
        DialogueResult Fallback(string reason) { GD.Print("[AI] " + reason); var r = _fallback.Reply(c, s, input); r.ProviderStatus = reason; return r; }
    }
    static async Task<HttpResponseMessage> PostWithRetry(System.Net.Http.HttpClient client, string url, string payload, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await client.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"), token);
            if (attempt > 0 || ((int)response.StatusCode != 429 && (int)response.StatusCode < 500)) return response;
            var delay = Math.Clamp(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 1, 1, 3); response.Dispose(); await Task.Delay(TimeSpan.FromSeconds(delay), token);
        }
    }
    static async Task<string> ReadErrorAsync(HttpResponseMessage response,CancellationToken token)
    {
        try
        {
            string body=await response.Content.ReadAsStringAsync(token);
            using var json=JsonDocument.Parse(body);
            string message=json.RootElement.GetProperty("error").GetProperty("message").GetString()??"requisição inválida";
            message=message.Replace('\n',' ').Replace('\r',' '); return message[..Math.Min(message.Length,220)];
        }
        catch { return "requisição rejeitada"; }
    }
    static object StrictDialogueFormat()
    {
        var properties=new Dictionary<string,object>
        {
            ["dialogue"]=new { type="string" }, ["emotion"]=new { type="string" }, ["desire"]=new { type="string" },
            ["affectionDelta"]=new { type="integer" }, ["trustDelta"]=new { type="integer" }, ["romanceDelta"]=new { type="integer" },
            ["attractionDelta"]=new { type="integer" }, ["energyDelta"]=new { type="integer" }, ["stressDelta"]=new { type="integer" },
            ["memory"]=new { type="string" }, ["importantMemory"]=new { type="boolean" }
        };
        string[] required={"dialogue","emotion","desire","affectionDelta","trustDelta","romanceDelta","attractionDelta","energyDelta","stressDelta","memory","importantMemory"};
        return new { type="json_schema",json_schema=new { name="heartbeat_dialogue",strict=true,schema=new { type="object",properties,required,additionalProperties=false } } };
    }
}

public static class DialogueValidator
{
    public static DialogueResult Sanitize(DialogueResult r)
    {
        if (string.IsNullOrWhiteSpace(r.Dialogue)) throw new JsonException("Resposta vazia");
        r.AffectionDelta=Math.Clamp(r.AffectionDelta,-3,3); r.TrustDelta=Math.Clamp(r.TrustDelta,-3,3); r.RomanceDelta=Math.Clamp(r.RomanceDelta,-3,3); r.AttractionDelta=Math.Clamp(r.AttractionDelta,-3,3); r.EnergyDelta=Math.Clamp(r.EnergyDelta,-3,3); r.StressDelta=Math.Clamp(r.StressDelta,-3,3);
        r.Dialogue=r.Dialogue[..Math.Min(r.Dialogue.Length,700)]; r.Memory??=""; r.Memory=r.Memory[..Math.Min(r.Memory.Length,240)];
        if(!new[]{"neutral","happy","shy","flirty","romantic","sad","angry","tired","surprised","embarrassed"}.Contains(r.Emotion))r.Emotion="neutral";
        if(!new[]{"talk","spend_time","rest","be_alone","eat","walk","go_home","flirt"}.Contains(r.Desire))r.Desire="talk";
        return r;
    }
}

public sealed class CharacterDesireSystem
{
    readonly Random _random = new();
    public string Choose(CharacterData c, CharacterState s, string period)
    {
        var options = new List<(string text, int weight)> { ("talk", 4), ("spend_time", s.Affection / 10 + 1), ("rest", s.Energy < 35 ? 12 : 1), ("be_alone", s.Stress > 65 ? 10 : 1), ("flirt", s.Romance > 20 ? 5 : 1), ("eat", s.CurrentLocation == "Cafe" ? 4 : 1) };
        if (period == "Night") options.Add(("go_home", 3));
        if (c.Personality.Contains("sociável")) options.Add(("talk", 4));
        var total = options.Sum(x => x.weight); var roll = _random.Next(total);
        foreach (var option in options) { roll -= option.weight; if (roll < 0) return option.text; } return "conversar";
    }
}

public sealed class ExpressionResolver
{
    public string Resolve(CharacterData c, CharacterState s)
    {
        if (s.Energy < 30) return "tired"; if (s.Stress > 70) return "angry"; if (s.Romance > 35 && s.Affection > 55) return "romantic";
        if (s.CurrentEmotion != "neutral") return s.CurrentEmotion;
        if (s.Mood > 60) return "happy"; return "neutral";
    }
    public CharacterImage? ImageFor(CharacterData c, string tag) => c.Images.FirstOrDefault(i => !i.Tags.Contains("special") && i.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))) ?? c.Images.FirstOrDefault(i=>i.ProcessedPath==c.MainImagePath && !i.Tags.Contains("special")) ?? c.Images.FirstOrDefault(i => i.Tags.Contains("neutral") && !i.Tags.Contains("special"));
}

public interface IBackgroundRemovalService { string Process(string sourcePath, string targetFolder); }
// Safe local fallback: keeps the original and caches a copy. An ONNX implementation can replace this service.
public sealed class BackgroundRemovalService : IBackgroundRemovalService
{
    public string Process(string source, string folder)
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        var name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))) + ".png"; var target = folder.PathJoin(name);
        // The fallback preserves pixels but standardizes a cached PNG. A later ONNX implementation can write alpha here.
        var absTarget = ProjectSettings.GlobalizePath(target);
        if (!File.Exists(absTarget))
        {
            var image = Image.LoadFromFile(source);
            if (image != null && !image.IsEmpty()) image.SavePng(absTarget);
            else File.Copy(source, absTarget, true);
        }
        return target;
    }
}
