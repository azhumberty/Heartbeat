namespace Heartbeat;

/// <summary>Validated campaign seed. Produced by WorldGenerationService; never executed as code.</summary>
public sealed class WorldDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Terras sem nome";
    public string Description { get; set; } = "";
    public string Genre { get; set; } = "dark fantasy";
    public string Tone { get; set; } = "sombria, íntima e misteriosa";
    public long Seed { get; set; }
    public string Prompt { get; set; } = "";
    public string StartingRegion { get; set; } = "";
    public string Threat { get; set; } = "";
    public string Atmosphere { get; set; } = "";
    public List<string> Biomes { get; set; } = new();
    public List<string> Regions { get; set; } = new();
    public List<string> Factions { get; set; } = new();
    public List<string> StoryHooks { get; set; } = new();
    public List<string> SelectedLibraryIds { get; set; } = new();
    /// <summary>BuiltIn, CreativeLibrary or WorldGenerator.</summary>
    public string Origin { get; set; } = "WorldGenerator";
    public string ProviderStatus { get; set; } = "Offline · mundo procedural";
}

public sealed class WorldManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public long Seed { get; set; }
    public string CreatedAt { get; set; } = "";
    public string LastPlayedAt { get; set; } = "";
    public int PlayedSeconds { get; set; }
    public string Summary { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
}

public sealed class WorldIndexFile
{
    public string ActiveId { get; set; } = "";
    public List<WorldManifest> Worlds { get; set; } = new();
}
