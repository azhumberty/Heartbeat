using Godot;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

/// <summary>Turns a player prompt plus optional library picks into a validated WorldDefinition. AI returns data, never code.</summary>
public static class WorldGenerationService
{
    public static WorldDefinition CreateOffline(string prompt, long seed, IReadOnlyList<string>? selectedIds = null)
    {
        var rng = new Random(unchecked((int)seed));
        var text = Limit(prompt, 1500, "Um mundo medieval sombrio à espera de um viajante.");
        var lower = text.ToLowerInvariant();
        var def = new WorldDefinition
        {
            Seed = seed,
            Prompt = text,
            Origin = (selectedIds?.Count ?? 0) > 0 ? "CreativeLibrary" : "WorldGenerator",
            SelectedLibraryIds = (selectedIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList(),
            Name = InferName(text, rng),
            Description = text.Length > 500 ? text[..500].Trim() + "..." : text,
            Genre = lower.Contains("academia") ? "academia arcana" : lower.Contains("ilha") ? "ilha amaldiçoada" : "dark fantasy medieval",
            Tone = lower.Contains("romance") || lower.Contains("amor") ? "sombria e romântica" : "sombria, íntima e cheia de promessas",
            StartingRegion = InferRegion(lower, rng),
            Atmosphere = lower.Contains("floresta") ? "névoa, raízes e lanternas fracas" : "ruínas úmidas e conversas baixas",
            Threat = InferThreat(lower, rng),
            Biomes = InferList(lower, new[] { ("floresta", "floresta amaldiçoada"), ("pântano", "pântano"), ("cidade", "cidades decadentes"), ("ilha", "costa rochosa"), ("deserto", "deserto") }, "bosques sombrios", "vilas decadentes"),
            Regions = new() { InferRegion(lower, rng) },
            Factions = InferList(lower, new[] { ("guerra", "três facções em guerra"), ("igreja", "a vigília das lanternas"), ("mercador", "os mercadores de cinza") }, "a Vigília das Lanternas", "os Mercadores de Cinza"),
            StoryHooks = InferList(lower, new[] { ("minotauro", "Um minotauro guarda um pacto antigo."), ("lobisomem", "Uivos marcam o limite da aldeia."), ("romance", "Alguém espera um nome que ainda não foi dito.") }, "Uma luz permanece acesa depois da meia-noite.", "O primeiro caminho do Atlas ainda não tem dono."),
            ProviderStatus = "Offline · mundo procedural"
        };
        def.Regions = def.Regions.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        if (def.Regions.Count == 0) def.Regions.Add(def.StartingRegion);
        return Sanitize(def);
    }

    public static async Task<WorldDefinition> CreateAsync(string prompt, long seed, IReadOnlyList<string> selectedIds, GameSettings settings, CancellationToken token = default)
    {
        var offline = CreateOffline(prompt, seed, selectedIds);
        if (OpenRouterClient.HasKey(settings) && settings.UseOpenRouter)
        {
            try
            {
                var selected = selectedIds.Take(12).ToArray();
                var payload = new { seed, prompt = Limit(prompt, 1500, ""), selectedLibraryIds = selected };
                var user = "Crie o COMECO de uma campanha 2D (nao a campanha inteira) em portugues, a partir do pedido do jogador. Responda somente JSON com: name, description, genre, tone, startingRegion, threat, atmosphere, biomes, regions, factions, storyHooks. Nao cite IDs. Pedido: " + JsonSerializer.Serialize(payload);
                var content = await OpenRouterClient.CompleteJson(settings, user, 700, token);
                var parsed = JsonSerializer.Deserialize<WorldDefinition>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException("definicao vazia");
                parsed.Seed = seed;
                parsed.Prompt = Limit(prompt, 1500, parsed.Prompt);
                parsed.SelectedLibraryIds = selectedIds.ToList();
                parsed.Origin = selectedIds.Count > 0 ? "CreativeLibrary" : "WorldGenerator";
                parsed.ProviderStatus = "Online · OpenRouter (Dolphin)";
                GD.Print("[AI] WorldDefinition OpenRouter validada");
                return Sanitize(parsed);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                GD.Print("[AI] Mundo OpenRouter indisponivel (" + e.GetType().Name + "); tentando Groq/offline");
            }
        }
        if (!settings.UseOnlineAi) return offline;
        if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GROQ_API_KEY")))
        {
            offline.ProviderStatus = "Offline · sem chave Groq";
            return offline;
        }
        try
        {
            if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || endpoint.Host != "api.groq.com")
            {
                offline.ProviderStatus = "Endpoint inválido · mundo criado offline";
                return offline;
            }
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
            var selected = selectedIds.Take(12).ToArray();
            var payload = new
            {
                seed,
                prompt = Limit(prompt, 1500, ""),
                selectedLibraryIds = selected
            };
            var user = "Crie o COMEÇO de uma campanha 2D (não a campanha inteira) em português, a partir do pedido do jogador. Responda somente JSON do schema. Não cite IDs. Incorpore os nomes persistentes de forma orgânica se existirem. Pedido: " + JsonSerializer.Serialize(payload);
            bool gptOss = settings.Model.StartsWith("openai/gpt-oss", StringComparison.OrdinalIgnoreCase);
            var request = new Dictionary<string, object>
            {
                ["model"] = settings.Model,
                ["temperature"] = Math.Clamp(settings.Temperature, 0f, 1.1f),
                ["max_completion_tokens"] = gptOss ? 900 : 600,
                ["response_format"] = gptOss ? StrictFormat() : new { type = "json_object" },
                ["messages"] = new[] { new { role = "user", content = user } }
            };
            if (gptOss) { request["reasoning_format"] = "hidden"; request["reasoning_effort"] = "low"; }
            using var response = await client.PostAsync(settings.Endpoint.TrimEnd('/') + "/chat/completions", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"), token);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Groq HTTP {(int)response.StatusCode}");
            using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var content = envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            content = content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
            var parsed = JsonSerializer.Deserialize<WorldDefinition>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException("definição vazia");
            parsed.Seed = seed;
            parsed.Prompt = Limit(prompt, 1500, parsed.Prompt);
            parsed.SelectedLibraryIds = selectedIds.ToList();
            parsed.Origin = selectedIds.Count > 0 ? "CreativeLibrary" : "WorldGenerator";
            parsed.ProviderStatus = "Online · começo proposto pela Groq";
            GD.Print("[AI] WorldDefinition Groq validada");
            return Sanitize(parsed);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            GD.Print($"[AI] Mundo Groq indisponível ({e.GetType().Name}); usando offline");
            offline.ProviderStatus = "Falha na IA · mundo criado offline";
            return offline;
        }
    }

    public static WorldDefinition Sanitize(WorldDefinition def)
    {
        def ??= new WorldDefinition();
        def.Name = Limit(def.Name, 70, "Terras sem nome");
        def.Description = Limit(def.Description, 600, "Uma estrada antiga chama viajantes.");
        def.Genre = Limit(def.Genre, 40, "dark fantasy");
        def.Tone = Limit(def.Tone, 160, "sombria e misteriosa");
        def.StartingRegion = Limit(def.StartingRegion, 70, def.Name);
        def.Threat = Limit(def.Threat, 180, "Algo desperta nas ruínas.");
        def.Atmosphere = Limit(def.Atmosphere, 160, "medieval, sombria e romântica");
        def.Prompt = Limit(def.Prompt, 1500, def.Description);
        def.Biomes = Clean(def.Biomes, 6, 40);
        def.Regions = Clean(def.Regions, 5, 50);
        def.Factions = Clean(def.Factions, 3, 80);
        def.StoryHooks = Clean(def.StoryHooks, 4, 150);
        if (def.Factions.Count == 0) def.Factions.Add("a Vigília das Lanternas");
        if (def.StoryHooks.Count == 0) def.StoryHooks.Add("Há olhos atentos na estrada.");
        if (def.Regions.Count == 0) def.Regions.Add(def.StartingRegion);
        if (def.Biomes.Count == 0) def.Biomes.Add("bosques sombrios");
        def.SelectedLibraryIds = (def.SelectedLibraryIds ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();
        if (string.IsNullOrWhiteSpace(def.Origin)) def.Origin = def.SelectedLibraryIds.Count > 0 ? "CreativeLibrary" : "WorldGenerator";
        if (string.IsNullOrWhiteSpace(def.ProviderStatus)) def.ProviderStatus = "Offline · mundo procedural";
        return def;
    }

    public static WorldLore ToLore(WorldDefinition def) => new()
    {
        RegionName = def.Name,
        Premise = Limit(string.IsNullOrWhiteSpace(def.Prompt) ? def.Description : def.Prompt, 600, def.Description),
        Threat = def.Threat,
        Atmosphere = string.IsNullOrWhiteSpace(def.Atmosphere) ? def.Tone : def.Atmosphere,
        Factions = def.Factions.ToList(),
        Rumors = def.StoryHooks.Take(4).ToList()
    };

    public static WorldDefinition FromSave(GameSave save)
    {
        var lore = save.WorldLore ?? new WorldLore();
        return Sanitize(new WorldDefinition
        {
            Id = save.WorldId,
            Name = lore.RegionName,
            Description = lore.Premise,
            Tone = lore.Atmosphere,
            Seed = save.WorldSeed,
            Prompt = string.IsNullOrWhiteSpace(save.WorldPrompt) ? lore.Premise : save.WorldPrompt,
            StartingRegion = lore.RegionName,
            Threat = lore.Threat,
            Atmosphere = lore.Atmosphere,
            Factions = lore.Factions.ToList(),
            StoryHooks = lore.Rumors.ToList(),
            SelectedLibraryIds = save.SelectedLibraryIds?.ToList() ?? new(),
            Origin = "WorldGenerator"
        });
    }

    public static GameSave BuildSave(WorldDefinition def, GameSettings settings)
    {
        def = Sanitize(def);
        if (string.IsNullOrWhiteSpace(def.Id)) def.Id = Guid.NewGuid().ToString("N");
        if (def.Seed == 0) def.Seed = Random.Shared.NextInt64();
        var save = new GameSave
        {
            WorldId = def.Id,
            WorldSeed = def.Seed,
            WorldPrompt = def.Prompt,
            WorldDefinition = def,
            SelectedLibraryIds = def.SelectedLibraryIds.ToList(),
            Settings = settings,
            ActionsLeft = 6,
            FirstPerson = false,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            LastPlayedAt = DateTime.UtcNow.ToString("o"),
            SaveVersion = 10
        };
        save.WorldLore = ToLore(def);
        save.AtlasBackgroundPath = AtlasGenerator.Backdrop(def.Seed, 0);
        save.AtlasNodes = AtlasGenerator.Create(def.Seed, 0, def.SelectedLibraryIds);
        var repo = new CharacterRepository();
        var characters = repo.List().Where(c => c.CanBuildRelationship && !c.Tags.Contains("creative-disabled")).ToList();
        foreach (var id in def.SelectedLibraryIds)
        {
            var extra = repo.Load(id);
            if (extra != null && characters.All(c => c.Id != extra.Id)) characters.Add(extra);
        }
        if (characters.Count == 0) characters.Add(repo.LoadDemo());
        save.CharacterIds = characters.Select(c => c.Id).Distinct().ToList();
        save.CharacterStates = characters.ToDictionary(c => c.Id, _ => new CharacterState { CurrentLocation = "road" });
        DeckManager.Migrate(save);
        var catalog = new CardRepository().Catalog();
        foreach (var id in def.SelectedLibraryIds)
            if (catalog.TryGetValue(id, out var card) && card.CharacterId.Length == 0)
                save.Deck.Owned[card.Id] = save.Deck.Owned.GetValueOrDefault(card.Id) + 1;
        return save;
    }

    static string InferName(string prompt, Random rng)
    {
        var words = prompt.Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length >= 4 && !Stop.Contains(w.ToLowerInvariant()))
            .Take(6)
            .ToList();
        var token = words.FirstOrDefault(w => char.IsUpper(w[0])) ?? (words.Count > 0 ? words[rng.Next(words.Count)] : "Eredan");
        token = char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
        var lower = prompt.ToLowerInvariant();
        if (lower.Contains("ilha")) return "Ilha de " + token;
        if (lower.Contains("academia")) return "Academia de " + token;
        if (lower.Contains("reino")) return "Reino de " + token;
        string[] prefixes = { "Vale de", "Marca de", "Bosque de", "Terras de" };
        return prefixes[rng.Next(prefixes.Length)] + " " + token;
    }

    static string InferRegion(string lower, Random rng)
    {
        if (lower.Contains("floresta")) return "a orla da floresta amaldiçoada";
        if (lower.Contains("ilha")) return "o porto das lanternas";
        if (lower.Contains("academia")) return "os claustros da academia";
        string[] options = { "a estrada quebrada", "o acampamento na orla", "a vila de cinza" };
        return options[rng.Next(options.Length)];
    }

    static string InferThreat(string lower, Random rng)
    {
        if (lower.Contains("minotauro")) return "um pacto antigo acorda o minotauro";
        if (lower.Contains("lobisom")) return "a lua negra puxa as presas da floresta";
        if (lower.Contains("guerra")) return "três facções rasgam o mapa";
        string[] options = { "uma lua negra se aproxima das ruínas", "as raízes da floresta começaram a lembrar nomes", "um pacto antigo está se desfazendo" };
        return options[rng.Next(options.Length)];
    }

    static List<string> InferList(string lower, (string Key, string Value)[] map, params string[] fallback)
    {
        var hits = map.Where(p => lower.Contains(p.Key)).Select(p => p.Value).ToList();
        if (hits.Count == 0) hits.AddRange(fallback.Take(2));
        return hits.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
    }

    static List<string> Clean(List<string>? source, int take, int length) =>
        (source ?? new()).Where(v => !string.IsNullOrWhiteSpace(v)).Take(take).Select(v => Limit(v, length, "")).Where(v => v.Length > 0).ToList();

    static string Limit(string? value, int length, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text[..Math.Min(text.Length, length)];
    }

    static object StrictFormat()
    {
        var properties = new Dictionary<string, object>
        {
            ["name"] = new { type = "string" },
            ["description"] = new { type = "string" },
            ["genre"] = new { type = "string" },
            ["tone"] = new { type = "string" },
            ["startingRegion"] = new { type = "string" },
            ["threat"] = new { type = "string" },
            ["atmosphere"] = new { type = "string" },
            ["biomes"] = new { type = "array", items = new { type = "string" } },
            ["regions"] = new { type = "array", items = new { type = "string" } },
            ["factions"] = new { type = "array", items = new { type = "string" } },
            ["storyHooks"] = new { type = "array", items = new { type = "string" } }
        };
        return new Dictionary<string, object>
        {
            ["type"] = "json_schema",
            ["json_schema"] = new Dictionary<string, object>
            {
                ["name"] = "heartbeat_world_definition",
                ["strict"] = true,
                ["schema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = properties.Keys.ToArray(),
                    ["additionalProperties"] = false
                }
            }
        };
    }

    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "um","uma","uns","umas","o","a","os","as","de","da","do","das","dos","e","ou","que","para","com","em","no","na","nos","nas","por","mundo","onde","existe","existem","muito","muita","como","entre"
    };
}
