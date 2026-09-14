using Godot;

namespace Heartbeat;

/// <summary>Loads world visuals (map, node backs, portraits, enemy cutouts) with a progress overlay.</summary>
public static class WorldPrep
{
    public static bool Needed(GameSave game) =>
        game.Settings.UseImageAi
        && game.WorldDefinition != null
        && !string.Equals(game.WorldDefinition.Origin, "BuiltIn", StringComparison.OrdinalIgnoreCase)
        && !game.ImagePaths.ContainsKey("atlas");

    public static async Task Run(GameSave game, Action<string, int, int>? progress, CancellationToken ct)
    {
        game.ImagePaths ??= new();
        var lore = game.WorldLore ?? new WorldLore();
        var jobs = new List<(string id, ImageKind kind, string prompt, int w, int h, bool cut)>();

        jobs.Add(("atlas", ImageKind.Background,
            $"dark fantasy expedition map of {lore.RegionName}, {lore.Atmosphere}, roads toward {lore.Threat}, painted parchment, no readable text",
            1280, 720, false));

        foreach (var kind in game.AtlasNodes.Select(n => n.Kind).Distinct())
        {
            var dummy = new AtlasNodeData { Id = kind.ToString(), Kind = kind, Title = kind.ToString() };
            jobs.Add(("bgkind:" + kind, ImageKind.Background, BackgroundGenerationService.PromptFor(game, dummy), 1280, 720, false));
        }

        foreach (var person in game.GeneratedCast)
        {
            var look = CanonicalLook.Ensure(person, lore);
            jobs.Add(("portrait:" + person.Id, ImageKind.Portrait,
                $"{look}, isolated cutout, solid chroma-key green background #00FF00, no scenery, no floor, no shadow on backdrop",
                768, 1024, true));
        }

        foreach (var foe in game.GeneratedEnemies)
        {
            jobs.Add(("enemy:" + foe.Id, ImageKind.Portrait,
                $"photoreal dark-fantasy creature named {foe.Name}, {lore.Threat}, isolated cutout, solid chroma-key green background #00FF00, no scenery",
                768, 1024, true));
        }

        int i = 0;
        foreach (var job in jobs)
        {
            i++;
            progress?.Invoke(job.id, i, jobs.Count);
            ct.ThrowIfCancellationRequested();
            if (game.ImagePaths.ContainsKey(job.id) && File.Exists(game.ImagePaths[job.id])) continue;
            var seed = BackgroundGenerationService.Seed(game.WorldSeed, job.id);
            var key = ImageCache.Key(job.kind, job.prompt, job.w, job.h, seed);
            var cached = ImageCache.Load(job.kind, key);
            if (cached == null)
            {
                var bytes = await ImageAi.From(game.Settings).GenerateAsync(job.prompt, job.w, job.h, seed, job.kind, ct);
                if (bytes == null || bytes.Length < 32) continue;
                ImageCache.Save(job.kind, key, bytes, job.cut);
                cached = ImageCache.Load(job.kind, key);
            }
            if (cached == null) continue;
            var path = ImageCache.PathFor(job.kind, key);
            game.ImagePaths[job.id] = path;
            if (job.id.StartsWith("portrait:"))
            {
                var id = job.id["portrait:".Length..];
                var person = game.GeneratedCast.FirstOrDefault(c => c.Id == id);
                if (person != null) person.GeneratedPortraitPath = path;
            }
            if (job.id == "atlas") game.AtlasBackgroundPath = path;
        }
    }
}

public static class Inventory
{
    public static int Count(GameSave game, string id) => game.Items.GetValueOrDefault(id);

    public static void Grant(GameSave game, string id, int n = 1)
    {
        game.Items ??= new();
        game.Items[id] = game.Items.GetValueOrDefault(id) + Math.Max(1, n);
    }

    public static bool UseHp(GameSave game, int amount = 25)
    {
        if (Count(game, "potion_hp") <= 0) return false;
        game.Items["potion_hp"]--;
        if (game.Items["potion_hp"] <= 0) game.Items.Remove("potion_hp");
        game.Player.Health = Math.Min(game.Player.MaxHealth, game.Player.Health + amount);
        return true;
    }

    public static bool UseMana(GameSave game, int amount = 20)
    {
        if (Count(game, "potion_mana") <= 0) return false;
        game.Items["potion_mana"]--;
        if (game.Items["potion_mana"] <= 0) game.Items.Remove("potion_mana");
        game.Player.Mana = Math.Min(game.Player.MaxMana, game.Player.Mana + amount);
        return true;
    }

    public static string Label(GameSave game) =>
        $"Poções vida {Count(game, "potion_hp")} · mana {Count(game, "potion_mana")}";
}

public static class WorldArt
{
    public static Texture2D? Background(GameSave game, AtlasNodeData node)
    {
        foreach (var key in new[] { "bg:" + node.Id, "bgkind:" + node.Kind })
        {
            if (game.ImagePaths.TryGetValue(key, out var path))
            {
                var tex = Load(path);
                if (tex != null) return tex;
            }
        }
        if (node.BackgroundId.Contains("://") || Path.IsPathRooted(node.BackgroundId))
            return ContentLibrary.LoadTexture(node.BackgroundId);
        return null;
    }

    public static Texture2D? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var full = path.StartsWith("user://") || path.StartsWith("res://") ? ProjectSettings.GlobalizePath(path) : path;
            if (!File.Exists(full)) return null;
            return ImageCache.Decode(File.ReadAllBytes(full));
        }
        catch { return null; }
    }
}
