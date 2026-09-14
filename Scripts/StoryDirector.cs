namespace Heartbeat;

public sealed class GeneratedEnemy
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Guardião";
    public int Health { get; set; } = 80;
    public int Damage { get; set; } = 14;
    public int RewardXp { get; set; } = 28;
    public string Color { get; set; } = "6b4a32";
    public int CoinMin { get; set; } = 14;
    public int CoinMax { get; set; } = 28;
    public int LootTier { get; set; } = 2;
}

public static class WorldCast
{
    public static IReadOnlyList<CharacterData> For(GameSave game)
    {
        var list = new CharacterRepository().List()
            .Where(c => c.Id != "merchant" && !c.Tags.Contains("demo") && !c.Tags.Contains("creative-disabled"))
            .ToList();
        foreach (var g in game.GeneratedCast ?? new())
        {
            SocialModelMigrator.Migrate(g);
            list.RemoveAll(c => c.Id == g.Id);
            list.Add(g);
        }
        return list;
    }

    public static CharacterData? Load(GameSave game, string id) =>
        For(game).FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Turns WorldDefinition into living atlas, generated people, and enemies. Data only — never code.</summary>
public static class StoryDirector
{
    public static void ComposeWorld(GameSave save)
    {
        save.GeneratedCast ??= new();
        save.GeneratedEnemies ??= new();
        save.StoryLog ??= new();
        save.WorldLore ??= new();
        if (save.GeneratedCast.Count == 0) save.GeneratedCast.AddRange(BuildCast(save));
        if (save.GeneratedEnemies.Count == 0) save.GeneratedEnemies.AddRange(BuildEnemies(save));
        foreach (var person in save.GeneratedCast)
        {
            if (!save.CharacterIds.Contains(person.Id)) save.CharacterIds.Add(person.Id);
            save.CharacterStates.TryAdd(person.Id, new CharacterState { CurrentLocation = "road" });
        }
        BindAtlas(save);
        save.CurrentStoryBeat = save.WorldLore.Rumors.FirstOrDefault() ?? save.WorldLore.Threat;
        if (save.StoryLog.Count == 0)
            save.StoryLog.Add("inicio:" + save.WorldLore.RegionName + " · " + Limit(save.WorldLore.Threat, 80));
    }

    public static void OnNewExpedition(GameSave save)
    {
        BindAtlas(save);
        save.StoryLog.Add("expedicao:" + save.ExpeditionIndex + ":" + Limit(save.WorldLore.Threat, 60));
        while (save.StoryLog.Count > 24) save.StoryLog.RemoveAt(0);
    }

    public static void OnNodeCompleted(GameSave save, AtlasNodeData node)
    {
        save.StoryLog ??= new();
        save.StoryLog.Add("no:" + node.Kind + ":" + Limit(node.Title, 40));
        while (save.StoryLog.Count > 24) save.StoryLog.RemoveAt(0);
        save.CurrentStoryBeat = Limit(save.WorldLore.Threat + " depois de " + node.Title, 120);
        var locked = save.AtlasNodes.Where(n => n.Status == AtlasNodeStatus.Locked && !n.Persistent).OrderBy(n => n.X).ToList();
        if (locked.Count == 0) return;
        var next = locked[0];
        next.Description = Limit("Depois de " + node.Title + ", " + save.WorldLore.Threat + " avança por " + save.WorldLore.RegionName + ".", 220);
        if (next.Kind == AtlasNodeKind.Character)
        {
            var extra = save.GeneratedCast.FirstOrDefault(c => c.CanBuildRelationship);
            if (extra != null) next.ContentId = extra.Id;
        }
    }

    public static void BindAtlas(GameSave save)
    {
        var lore = save.WorldLore ?? new WorldLore();
        var ward = save.GeneratedEnemies.FirstOrDefault(e => e.Id.Contains("_ward")) ?? save.GeneratedEnemies.FirstOrDefault();
        var boss = save.GeneratedEnemies.FirstOrDefault(e => e.Id.Contains("_boss")) ?? save.GeneratedEnemies.LastOrDefault();
        var merchant = save.GeneratedCast.FirstOrDefault(c => c.Tags.Contains("merchant"));
        var people = save.GeneratedCast.Where(c => c.CanBuildRelationship).Select(c => c.Id).ToList();
        int pi = 0;
        var biome = lore.Atmosphere.Length > 0 ? lore.Atmosphere : "nevoa";
        foreach (var node in save.AtlasNodes)
        {
            if (node.Persistent)
            {
                node.Title = "Acampamento de " + lore.RegionName;
                node.Description = Limit("Refúgio em " + lore.RegionName + ". " + lore.Premise, 200);
                continue;
            }
            if (node.Kind == AtlasNodeKind.Combat && ward != null)
            {
                node.ContentId = ward.Id;
                if (!node.Title.StartsWith("Elite", StringComparison.OrdinalIgnoreCase))
                    node.Title = Limit(ward.Name + " · " + node.Title, 48);
                node.Description = Limit(lore.Threat + " ronda este caminho.", 200);
            }
            else if (node.Kind == AtlasNodeKind.Boss && boss != null)
            {
                node.ContentId = boss.Id;
                node.Title = Limit(boss.Name, 48);
                node.Description = Limit("O fim desta expedição: " + lore.Threat, 200);
            }
            else if (node.Kind == AtlasNodeKind.Merchant && merchant != null)
            {
                node.ContentId = merchant.Id;
                node.Title = Limit("Tenda de " + merchant.Name, 48);
                node.Description = Limit(merchant.Description, 200);
            }
            else if (node.Kind == AtlasNodeKind.Character && people.Count > 0)
            {
                node.ContentId = people[pi % people.Count];
                pi++;
                var person = save.GeneratedCast.First(c => c.Id == node.ContentId);
                node.Title = Limit(person.Name + " em " + lore.RegionName, 48);
                node.Description = Limit(person.Description, 200);
            }
            else
            {
                node.Description = Limit(lore.Premise + " " + lore.Threat, 200);
                if (node.Kind is AtlasNodeKind.Mystery or AtlasNodeKind.Event or AtlasNodeKind.Scene)
                    node.Title = Limit(PickHook(lore, node.Kind) + " · " + lore.RegionName, 48);
            }
            node.BackgroundId = BackgroundFor(lore, node);
        }
    }

    static List<CharacterData> BuildCast(GameSave save)
    {
        var lore = save.WorldLore;
        var token = Token(save.WorldPrompt + " " + lore.RegionName);
        var prefix = "gen" + Short(save.WorldId);
        var wanderer = new CharacterData
        {
            Id = prefix + "_w",
            Name = token,
            Age = 24,
            CanBuildRelationship = true,
            Description = Limit("Nasceu em " + lore.RegionName + ". Vive à sombra de: " + lore.Threat, 220),
            Personality = "reservado, leal ao sítio onde cresceu, desconfiado de forasteiros",
            Traits = new() { "observador", "teimoso" },
            Likes = new() { lore.StartingRegion.Length > 0 ? lore.StartingRegion : lore.RegionName },
            Dislikes = new() { "promessas vazias" },
            Interests = lore.Factions.Take(2).ToList(),
            SpeechStyle = "baixo, fala do lugar como se fosse um corpo",
            Profession = "habitante",
            MainImagePath = "res://Assets/ArtKit/Characters/NPCs/npc_knight_chroma.png",
            Tags = new() { "generated", "relationship", "adulto-ficticio" },
            HomeLocation = "Camp"
        };
        wanderer.Images.Add(new CharacterImage { OriginalPath = wanderer.MainImagePath, ProcessedPath = wanderer.MainImagePath, Tags = new() { "neutral", "chroma" } });
        wanderer.PersonalityProfile = new() { Extraversion = 30, Empathy = 70, Courage = 45, Pride = 35, Romanticism = 55, InitializedFromLegacy = true };
        var merchant = new CharacterData
        {
            Id = prefix + "_m",
            Name = "Mercador de " + lore.RegionName,
            Age = 38,
            CanBuildRelationship = false,
            Description = Limit("Compra rumores em " + lore.RegionName + " e vende o que a ameaça ainda não levou.", 220),
            Personality = "encantador, calculista, nunca se apaixona no trabalho",
            Traits = new() { "persuasivo", "viajado" },
            SpeechStyle = "cantado, irônico, cheio de ofertas",
            Profession = "mercador",
            MainImagePath = "res://Assets/ArtKit/Characters/NPCs/merchant_traveler_chroma.png",
            Tags = new() { "generated", "merchant", "adulto-ficticio" },
            HomeLocation = "Camp"
        };
        merchant.Images.Add(new CharacterImage { OriginalPath = merchant.MainImagePath, ProcessedPath = merchant.MainImagePath, Tags = new() { "neutral", "chroma" } });
        SocialModelMigrator.Migrate(wanderer);
        SocialModelMigrator.Migrate(merchant);
        return new() { wanderer, merchant };
    }

    static List<GeneratedEnemy> BuildEnemies(GameSave save)
    {
        var lore = save.WorldLore;
        var prefix = "gen" + Short(save.WorldId);
        var threatWord = Token(lore.Threat);
        return new()
        {
            new() { Id = prefix + "_ward", Name = "Sentinela de " + threatWord, Health = 88, Damage = 15, RewardXp = 30, LootTier = 2, CoinMin = 14, CoinMax = 26, Color = "5a6b48" },
            new() { Id = prefix + "_boss", Name = "Guardião de " + lore.RegionName, Health = 168, Damage = 22, RewardXp = 56, LootTier = 4, CoinMin = 32, CoinMax = 54, Color = "6b4a32" }
        };
    }

    static string BackgroundFor(WorldLore lore, AtlasNodeData node)
    {
        if (node.Persistent) return "camp";
        var blob = (lore.Atmosphere + " " + lore.Premise + " " + string.Join(' ', lore.Factions)).ToLowerInvariant();
        if (node.Kind == AtlasNodeKind.Boss) return blob.Contains("caverna") || blob.Contains("cave") ? "cave_chamber" : "ruins_hall";
        if (node.Kind == AtlasNodeKind.Merchant) return "merchant_tent";
        if (node.Kind == AtlasNodeKind.Rest) return "camp";
        if (blob.Contains("cidade") || blob.Contains("rua")) return "street";
        if (blob.Contains("taverna") || blob.Contains("bar")) return "tavern";
        if (blob.Contains("pantano") || blob.Contains("pântano") || blob.Contains("noite")) return "forest_dark";
        if (blob.Contains("ilha") || blob.Contains("costa")) return "ruins_hall";
        return node.Kind == AtlasNodeKind.Combat ? "forest_dark" : node.BackgroundId;
    }

    static string PickHook(WorldLore lore, AtlasNodeKind kind)
    {
        if (lore.Rumors.Count > 0) return lore.Rumors[kind == AtlasNodeKind.Mystery ? 0 : lore.Rumors.Count - 1];
        return kind == AtlasNodeKind.Mystery ? lore.Threat : lore.RegionName;
    }

    static string Token(string text)
    {
        var words = (text ?? "").Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length >= 4)
            .ToList();
        var pick = words.FirstOrDefault(w => char.IsUpper(w[0])) ?? words.FirstOrDefault() ?? "Esen";
        pick = new string(pick.Where(char.IsLetter).ToArray());
        if (pick.Length < 3) pick = "Esen";
        return char.ToUpperInvariant(pick[0]) + pick[1..].ToLowerInvariant();
    }

    static string Short(string id)
    {
        var s = new string((id ?? "").Where(char.IsLetterOrDigit).ToArray());
        return s.Length >= 8 ? s[..8] : (s + "worldgen").PadRight(8, 'x')[..8];
    }

    static string Limit(string? value, int n)
    {
        var t = (value ?? "").Trim();
        return t.Length <= n ? t : t[..n].Trim() + "...";
    }
}
