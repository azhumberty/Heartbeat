using Godot;
using System.Text.Json;

namespace Heartbeat;

/// <summary>One folder per world. Never overwrites a different world's files.</summary>
public sealed class WorldStore
{
    readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    const string Root = "user://worlds";

    string RootPath => ProjectSettings.GlobalizePath(Root);
    string IndexPath => Path.Combine(RootPath, "index.json");

    static string SafeId(string id)
    {
        var cleaned = new string((id ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (cleaned.Length < 8) throw new ArgumentException("Id de mundo inválido.");
        return cleaned;
    }

    string DirFor(string id) => Path.Combine(RootPath, SafeId(id));

    public List<WorldManifest> List()
    {
        var index = ReadIndex();
        return index.Worlds
            .OrderByDescending(w => w.LastPlayedAt)
            .ThenByDescending(w => w.CreatedAt)
            .ToList();
    }

    public int Count => List().Count;

    public GameSave? Load(string id)
    {
        try
        {
            var path = Path.Combine(DirFor(id), "save.json");
            if (!File.Exists(path)) return null;
            var save = JsonSerializer.Deserialize<GameSave>(File.ReadAllText(path), _json);
            if (save == null || save.SaveVersion < 13) return null;
            new SaveManager().Migrate(save);
            return save;
        }
        catch (Exception e)
        {
            GD.PushWarning("[Worlds] Load falhou: " + e.GetType().Name);
            return null;
        }
    }

    public bool TryLoadActive(out GameSave? save)
    {
        save = null;
        var id = ReadIndex().ActiveId;
        if (string.IsNullOrWhiteSpace(id)) return false;
        save = Load(id);
        return save != null;
    }

    public void SetActive(string id)
    {
        var index = ReadIndex();
        index.ActiveId = SafeId(id);
        WriteAtomic(IndexPath, index);
    }

    public void Save(GameSave game)
    {
        if (string.IsNullOrWhiteSpace(game.WorldId))
            game.WorldId = Guid.NewGuid().ToString("N");
        var id = SafeId(game.WorldId);
        game.WorldId = id;
        if (string.IsNullOrWhiteSpace(game.CreatedAt))
            game.CreatedAt = DateTime.UtcNow.ToString("o");
        game.LastPlayedAt = DateTime.UtcNow.ToString("o");
        var dir = DirFor(id);
        Directory.CreateDirectory(dir);
        var def = game.WorldDefinition ?? WorldGenerationService.FromSave(game);
        def.Id = id;
        def.Seed = game.WorldSeed;
        def.Prompt = string.IsNullOrWhiteSpace(def.Prompt) ? game.WorldPrompt : def.Prompt;
        game.WorldDefinition = def;
        WriteAtomic(Path.Combine(dir, "world.json"), def);
        WriteAtomic(Path.Combine(dir, "save.json"), game);
        var index = ReadIndex();
        index.ActiveId = id;
        var man = ToManifest(game, def);
        var at = index.Worlds.FindIndex(w => w.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) index.Worlds[at] = man;
        else index.Worlds.Add(man);
        WriteAtomic(IndexPath, index);
    }

    public bool Delete(string id)
    {
        var safe = SafeId(id);
        var dir = DirFor(safe);
        var index = ReadIndex();
        index.Worlds.RemoveAll(w => w.Id.Equals(safe, StringComparison.OrdinalIgnoreCase));
        if (index.ActiveId.Equals(safe, StringComparison.OrdinalIgnoreCase))
            index.ActiveId = index.Worlds.FirstOrDefault()?.Id ?? "";
        WriteAtomic(IndexPath, index);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        return true;
    }

    public int DiscardIncompatible(int minVersion = 13)
    {
        int n = 0;
        foreach (var man in List().ToList())
        {
            int ver = 0;
            try
            {
                var path = Path.Combine(DirFor(man.Id), "save.json");
                if (!File.Exists(path)) { Delete(man.Id); n++; continue; }
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("SaveVersion", out var v)) ver = v.GetInt32();
            }
            catch { ver = 0; }
            if (ver < minVersion) { Delete(man.Id); n++; }
        }
        return n;
    }

    public void ImportLegacy(SaveManager saves)
    {
        if (List().Count > 0) return;
        var legacy = saves.LoadSlot(1);
        if (legacy == null || legacy.SaveVersion < 13) return;
        if (string.IsNullOrWhiteSpace(legacy.WorldId))
            legacy.WorldId = "legacy_" + Math.Abs(legacy.WorldSeed).ToString("x8").PadLeft(8, '0');
        if (string.IsNullOrWhiteSpace(legacy.WorldPrompt))
            legacy.WorldPrompt = legacy.WorldLore?.Premise ?? "Campanha anterior";
        if (string.IsNullOrWhiteSpace(legacy.CreatedAt))
            legacy.CreatedAt = DateTime.UtcNow.ToString("o");
        if (legacy.WorldDefinition == null)
            legacy.WorldDefinition = WorldGenerationService.FromSave(legacy);
        Save(legacy);
    }

    WorldIndexFile ReadIndex()
    {
        try
        {
            if (!File.Exists(IndexPath)) return new WorldIndexFile();
            return JsonSerializer.Deserialize<WorldIndexFile>(File.ReadAllText(IndexPath), _json) ?? new WorldIndexFile();
        }
        catch { return new WorldIndexFile(); }
    }

    void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, _json));
        File.Move(tmp, path, true);
    }

    static WorldManifest ToManifest(GameSave game, WorldDefinition def) => new()
    {
        Id = game.WorldId,
        Name = string.IsNullOrWhiteSpace(def.Name) ? game.WorldLore.RegionName : def.Name,
        Prompt = game.WorldPrompt,
        Seed = game.WorldSeed,
        CreatedAt = game.CreatedAt,
        LastPlayedAt = game.LastPlayedAt,
        PlayedSeconds = game.PlayedSeconds,
        Summary = string.IsNullOrWhiteSpace(def.Description) ? game.WorldLore.Premise : def.Description,
        ThumbnailPath = game.AtlasBackgroundPath
    };
}
