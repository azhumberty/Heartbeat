using Godot;
using System.Text.Json;

namespace Heartbeat;

public sealed class CharacterRepository
{
    static bool _optionalWarningShown;
    readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public CharacterData LoadDemo()
    {
        var text = Godot.FileAccess.GetFileAsString("res://Characters/demo/character.json");
        var character=JsonSerializer.Deserialize<CharacterData>(text, _json) ?? new CharacterData { Name = "Luna" };
        SocialModelMigrator.Migrate(character); return character;
    }
    public IReadOnlyList<CharacterData> List()
    {
        var result = new List<CharacterData>();
        var roots = new[] { ProjectSettings.GlobalizePath("res://Characters"), ProjectSettings.GlobalizePath("user://Characters") };
        foreach (var absolute in roots.Where(Directory.Exists))
        {
            string[] files;
            try{files=Directory.GetFiles(absolute,"character.json",SearchOption.AllDirectories);}catch(Exception e){if(!_optionalWarningShown){GD.Print($"[CharacterRepository] Pasta opcional indisponível: {e.GetType().Name}");_optionalWarningShown=true;}continue;}
            foreach (var file in files)
            {
                try { var item = JsonSerializer.Deserialize<CharacterData>(File.ReadAllText(file), _json); if (item != null) { Wardrobe.Migrate(item); SocialModelMigrator.Migrate(item); result.RemoveAll(x => x.Id == item.Id); result.Add(item); } }
                catch (Exception e) { GD.PushWarning($"[CharacterRepository] Ignored invalid character: {e.Message}"); }
            }
        }
        return result;
    }
    public CharacterData? Load(string id) => List().FirstOrDefault(c => c.Id == id);
    public bool Exists(string id) => Load(id) != null;
    public CharacterData Create(string name = "Novo personagem") => new() { Id = Guid.NewGuid().ToString("N"), Name = name, Age = 18 };
    public CharacterData? Duplicate(string id)
    {
        var source = Load(id); if (source == null) return null;
        var copy = JsonSerializer.Deserialize<CharacterData>(JsonSerializer.Serialize(source, _json), _json)!; copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " (cópia)"; Save(copy); return copy;
    }
    public bool Delete(string id)
    {
        var path = ProjectSettings.GlobalizePath($"user://Characters/{id}");
        if (!Directory.Exists(path)) return false; Directory.Delete(path, true); return true;
    }
    public void Save(CharacterData data)
    {
        Wardrobe.Migrate(data); SocialModelMigrator.Migrate(data); data.CharacterFormatVersion = 4;
        if (data.Age < 18) throw new ArgumentException("O personagem precisa ter pelo menos 18 anos.");
        if (string.IsNullOrWhiteSpace(data.Id) || data.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || data.Id.Contains("..")) throw new ArgumentException("ID inválido.");
        var folder = $"user://Characters/{data.Id}"; DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        using var file = Godot.FileAccess.Open($"{folder}/character.json", Godot.FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(data, _json));
    }
}

public sealed class SaveManager
{
    readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    string PathFor(int slot) => $"user://saves/slot_{slot}.json";
    const string SettingsPath="user://settings.json";
    public GameSettings LoadSettings()
    {
        var full=ProjectSettings.GlobalizePath(SettingsPath);if(!File.Exists(full))return Load()?.Settings??new GameSettings();
        try
        {
            var settings=JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(full),_json)??new GameSettings();
            if(string.IsNullOrWhiteSpace(settings.OpenRouterModel)
                || settings.OpenRouterModel.Contains("mistral-small-3.1",StringComparison.OrdinalIgnoreCase)
                || settings.OpenRouterModel.Contains("dolphin-mistral-24b-venice",StringComparison.OrdinalIgnoreCase)
                || settings.OpenRouterModel.Equals("openrouter/free",StringComparison.OrdinalIgnoreCase))
                settings.OpenRouterModel=OpenRouterClient.DefaultModel;
            return settings;
        }
        catch{return new GameSettings();}
    }
    public void SaveSettings(GameSettings settings)
    {
        var full=ProjectSettings.GlobalizePath(SettingsPath);Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllText(full+".tmp",JsonSerializer.Serialize(settings,_json));File.Move(full+".tmp",full,true);
    }
    public void Save(GameSave game, int slot = 1)
    {
        Migrate(game); game.SaveVersion = 11;
        if (!string.IsNullOrWhiteSpace(game.WorldId))
        {
            new WorldStore().Save(game);
            return;
        }
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://saves"));
        var snapshot = JsonSerializer.SerializeToNode(game, _json)!;
        snapshot.AsObject().Remove("Character"); snapshot.AsObject().Remove("State");
        var path = ProjectSettings.GlobalizePath(PathFor(slot));
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.WriteAllText(path + ".tmp", snapshot.ToJsonString(_json)); File.Move(path + ".tmp", path, true);
    }
    public GameSave? LoadSlot(int slot = 1)
    {
        if (!Godot.FileAccess.FileExists(PathFor(slot))) return null;
        try
        {
            var save = JsonSerializer.Deserialize<GameSave>(Godot.FileAccess.GetFileAsString(PathFor(slot)), _json);
            if (save == null || save.SaveVersion < 11) return null;
            Migrate(save); return save;
        }
        catch (Exception e) { var detail=e.Message.Replace('\n',' ').Replace('\r',' ');GD.PushWarning("[Save] Arquivo inválido preservado: "+e.GetType().Name+" · "+detail[..Math.Min(detail.Length,180)]); return null; }
    }
    public GameSave? Load(int slot = 1)
    {
        var worlds = new WorldStore();
        worlds.ImportLegacy(this);
        if (worlds.TryLoadActive(out var active) && active != null) return active;
        return LoadSlot(slot);
    }
    public void Migrate(GameSave save)
    {
        save.CharacterStates ??= new(); save.CharacterIds ??= new();
        save.Player ??= new PlayerStats();
        save.WorldLore ??= new(); save.AtlasNodes ??= new(); save.RecentEvents ??= new();save.CampResidents??=new();
        save.SelectedLibraryIds ??= new();
        save.WorldId ??= "";
        save.WorldPrompt ??= "";
        save.CreatedAt ??= "";
        save.LastPlayedAt ??= "";
        save.PlayerName=(save.PlayerName??"Viajante").Trim();
        if(string.IsNullOrWhiteSpace(save.PlayerName))save.PlayerName="Viajante";
        save.PlayerName=save.PlayerName[..Math.Min(32,save.PlayerName.Length)];
        save.Player.Clamp();
        DeckManager.Migrate(save);

        // v2→v3: migrate inline character to package + state dictionary
        if (save.Character != null)
        {
            var id = save.Character.Id; if (!save.CharacterIds.Contains(id)) save.CharacterIds.Add(id);
            var repository=new CharacterRepository();
            if(!repository.Exists(id))repository.Save(save.Character);
            if (!save.CharacterStates.ContainsKey(id)) save.CharacterStates[id] = save.State ?? new CharacterState();
            GD.Print("[SaveMigration] Preserved legacy character package and state");
        }
        foreach (var id in save.CharacterIds) if (!save.CharacterStates.ContainsKey(id)) save.CharacterStates[id] = new();
        foreach (var state in save.CharacterStates.Values) SocialModelMigrator.Migrate(state);

        // v3→v4: add world seed and generator version
        if (save.SaveVersion < 4)
        {
            if (save.WorldSeed == 0)
                save.WorldSeed = new Random().NextInt64();
            save.GeneratorVersion = 2;
            GD.Print("[SaveMigration] Added world seed and generator version");
        }

        // v4→v5: continuous clock derived from the old period label.
        if (save.SaveVersion < 5 || save.WorldMinutes < 0)
        {
            save.WorldMinutes = save.Period switch { "Morning" => 8 * 60, "Afternoon" => 14 * 60, "Evening" => 18 * 60, "Night" => 22 * 60, _ => 8 * 60 };
        }
        // v5→v6: narrative campaign data. Existing saves receive a stable offline campaign.
        if (save.SaveVersion < 6 || save.AtlasNodes.Count == 0)
        {
            save.WorldLore ??= WorldLoreManager.CreateOffline(save.WorldSeed == 0 ? 1 : save.WorldSeed);
            save.AtlasNodes = AtlasGenerator.Create(save.WorldSeed == 0 ? 1 : save.WorldSeed);
        }
        // v6→v7: endless expeditions and persistent camp. Existing map IDs remain valid.
        if(save.SaveVersion<7)
        {
            save.ExpeditionIndex=Math.Max(0,save.ExpeditionIndex);
            foreach(var node in save.AtlasNodes)if(string.IsNullOrWhiteSpace(node.TemplateId))node.TemplateId=AtlasGenerator.TemplateFromId(node.Id);
        }
        // v7→v8: illustrated branching Atlas and an always accessible camp node.
        if(save.SaveVersion<8)
        {
            var camp=save.AtlasNodes.FirstOrDefault(node=>node.TemplateId=="camp"||node.Id=="camp");
            if(camp==null)
            {
                camp=new AtlasNodeData {Id="camp",TemplateId="camp",Title="Acampamento",Kind=AtlasNodeKind.Rest,X=.08f,Y=.82f,BackgroundId="camp",Description="Seu refúgio permanente entre expedições."};
                camp.Connections=save.AtlasNodes.Where(node=>node.Status==AtlasNodeStatus.Available).Take(2).Select(node=>node.Id).ToList();save.AtlasNodes.Insert(0,camp);
            }
            camp.Persistent=true;camp.Status=AtlasNodeStatus.Available;camp.Risk=0;camp.Layer=Math.Max(1,save.ExpeditionIndex+1);
            foreach(var node in save.AtlasNodes){node.Layer=Math.Max(1,node.Layer);node.Risk=Math.Clamp(node.Risk,0,5);}
        }
        // v8→v9: each endless expedition persists its own deterministic backdrop.
        if(save.SaveVersion<9||string.IsNullOrWhiteSpace(save.AtlasBackgroundPath))
            save.AtlasBackgroundPath=AtlasGenerator.Backdrop(save.WorldSeed==0?1:save.WorldSeed,save.ExpeditionIndex);
        save.FirstPerson = false;
        save.UnlockedUpgrades ??= new();
        save.SaveVersion = 11;
    }
}
