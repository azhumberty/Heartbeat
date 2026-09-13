using System.Text.Json.Serialization;

namespace Heartbeat;

/// <summary>Defines a single animation from a sprite sheet.</summary>
public sealed class SpriteAnimationDef
{
    /// <summary>Animation name following the pattern: idle_front, walk_left, talk_right, etc.</summary>
    public string Name { get; set; } = "idle_front";
    /// <summary>Path to the image file, relative to the character package folder (user://Characters/id/).</summary>
    public string ImagePath { get; set; } = "";
    public int Columns { get; set; } = 1;
    public int Rows { get; set; } = 1;
    /// <summary>0-based index of the first frame in the sheet for this animation.</summary>
    public int StartFrame { get; set; } = 0;
    public int FrameCount { get; set; } = 1;
    public float Fps { get; set; } = 8f;
    public bool Loop { get; set; } = true;
    public int Margin { get; set; } = 0;
    public int Spacing { get; set; } = 0;
    public float PivotX { get; set; } = 0.5f;
    public float PivotY { get; set; } = 1.0f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
}

/// <summary>A period-based schedule entry for NPC routine.</summary>
public sealed class ScheduleEntry
{
    public string Period { get; set; } = "Morning";
    /// <summary>One of: Idle, Walking, Working, Resting, Interacting</summary>
    public string Activity { get; set; } = "Idle";
    /// <summary>Logical target location name: Cafe, Park, Street, Home, etc.</summary>
    public string TargetLocation { get; set; } = "Street";
}

public sealed class CharacterData
{
    /// <summary>Only named relationship characters accumulate affinity and unlock companion cards.</summary>
    public bool CanBuildRelationship { get; set; } = true;
    public CompanionCardData? CardData { get; set; }
    public List<CharacterOutfit> Outfits { get; set; } = new();
    public string DefaultOutfitId { get; set; } = "";
    public int CharacterFormatVersion { get; set; } = 3;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Novo personagem";
    public int Age { get; set; } = 21;
    public string Description { get; set; } = "Personagem adulto fictÃ­cio.";
    public string Personality { get; set; } = "gentil e curiosa";
    public List<string> Traits { get; set; } = new();
    public List<string> Likes { get; set; } = new();
    public List<string> Dislikes { get; set; } = new();
    public List<string> Interests { get; set; } = new();
    public string SpeechStyle { get; set; } = "natural";
    public string Profession { get; set; } = "";
    public List<string> Hobbies { get; set; } = new();
    public string BehaviorNotes { get; set; } = "";
    public string AdditionalInfo { get; set; } = "";
    public string MainImagePath { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<CharacterImage> Images { get; set; } = new();
    public List<string> SpecialEvents { get; set; } = new();

    // --- New fields for animated sprites and NPC behavior ---
    /// <summary>Physical height in meters. The visible sprite area maps to this height.</summary>
    public float HeightMeters { get; set; } = 1.78f;
    /// <summary>Walk speed in meters per second.</summary>
    public float WalkSpeed { get; set; } = 1.4f;
    /// <summary>Collision cylinder radius in meters.</summary>
    public float CollisionRadius { get; set; } = 0.35f;
    /// <summary>Interaction radius in meters (how close player must be to press E).</summary>
    public float InteractionRadius { get; set; } = 2.5f;
    /// <summary>Visual scale multiplier.</summary>
    public float VisualScale { get; set; } = 1.0f;
    /// <summary>Vertical offset in meters to adjust foot placement.</summary>
    public float FootOffset { get; set; } = 0.0f;
    /// <summary>If true, left sprites are mirrored right (use only for symmetric characters).</summary>
    public bool MirrorHorizontal { get; set; } = false;
    /// <summary>Sprite animation definitions. Empty = legacy portrait-only character.</summary>
    public List<SpriteAnimationDef> SpriteAnimations { get; set; } = new();
    /// <summary>NPC home/anchor location for night schedule.</summary>
    public string HomeLocation { get; set; } = "Home";
    /// <summary>Simple daily schedule. If empty, uses CharacterScheduleSystem defaults.</summary>
    public List<ScheduleEntry> Schedule { get; set; } = new();
    /// <summary>Whether breathing procedural animation is enabled (disabled when idle animation exists).</summary>
    public bool EnableBreathing { get; set; } = true;
    /// <summary>Stable, provider-independent behavioral dimensions.</summary>
    public PersonalityProfile PersonalityProfile { get; set; } = new();
    /// <summary>Topics and limits discovered naturally through conversation.</summary>
    public CharacterPreferences Preferences { get; set; } = new();
}

public sealed class CharacterImage
{
    public string OriginalPath { get; set; } = "";
    public string ProcessedPath { get; set; } = "";
    public List<string> Tags { get; set; } = new() { "neutral" };
}

public sealed class CharacterState
{
    public string CurrentOutfitId { get; set; } = "";
    public int Mood { get; set; } = 55;
    public int Affection { get; set; } = 18;
    public int Trust { get; set; } = 15;
    public int Romance { get; set; }
    public int Attraction { get; set; } = 5;
    public int Energy { get; set; } = 75;
    public int Stress { get; set; } = 20;
    public int Curiosity { get; set; } = 55;
    public int Jealousy { get; set; }
    public string Relationship { get; set; } = "Stranger";
    public List<string> RecentMemories { get; set; } = new();
    public List<string> ImportantMemories { get; set; } = new();
    public List<string> RelationshipMemories { get; set; } = new();
    public string CurrentDesire { get; set; } = "conversar";
    public string CurrentEmotion { get; set; } = "neutral";
    public string CurrentLocation { get; set; } = "Cafe";
    public string LastInteraction { get; set; } = "";
    public List<string> RecentInputs { get; set; } = new();
    public List<string> Conversation { get; set; } = new();
    public string TimeContext { get; set; } = "";
    public List<string> Flags { get; set; } = new();

    // --- New fields for world position and NPC activity ---
    /// <summary>World X position (logical, persisted across chunk unload).</summary>
    public float WorldX { get; set; } = 0f;
    /// <summary>World Z position (logical, persisted across chunk unload).</summary>
    public float WorldZ { get; set; } = 0f;
    /// <summary>Current NPC activity: Idle, Walking, Working, Resting, Talking, Interacting</summary>
    public string CurrentActivity { get; set; } = "Idle";
    /// <summary>Confirmed canon owned by the save, never by an AI provider.</summary>
    public List<CharacterMemory> Memories { get; set; } = new();
    public string MemorySummary { get; set; } = "";
    public EmotionalState Emotions { get; set; } = new();
    public string CurrentMood { get; set; } = "content";
    public float LastEmotionUpdateMinutes { get; set; } = -1;
    public string WorldContext { get; set; } = "";

    public void Clamp()
    {
        Mood = Math.Clamp(Mood, 0, 100); Affection = Math.Clamp(Affection, 0, 100);
        Trust = Math.Clamp(Trust, 0, 100); Romance = Math.Clamp(Romance, 0, 100);
        Attraction = Math.Clamp(Attraction, 0, 100); Energy = Math.Clamp(Energy, 0, 100);
        Stress = Math.Clamp(Stress, 0, 100); Curiosity = Math.Clamp(Curiosity, 0, 100); Jealousy = Math.Clamp(Jealousy, 0, 100);
        Emotions ??= new(); Emotions.Clamp();
    }
}

public sealed class PersonalityProfile
{
    public int Extraversion { get; set; } = 50;
    public int Courage { get; set; } = 50;
    public int Patience { get; set; } = 55;
    public int Humor { get; set; } = 50;
    public int Aggression { get; set; } = 25;
    public int Empathy { get; set; } = 55;
    public int Loyalty { get; set; } = 55;
    public int Curiosity { get; set; } = 55;
    public int Romanticism { get; set; } = 45;
    public int Competitiveness { get; set; } = 45;
    public int Pride { get; set; } = 45;
    public bool InitializedFromLegacy { get; set; }
}

public sealed class CharacterPreferences
{
    public List<string> FavoriteTopics { get; set; } = new();
    public List<string> AvoidedTopics { get; set; } = new();
    public List<string> Goals { get; set; } = new();
    public List<string> Fears { get; set; } = new();
    public List<string> FavoriteActivities { get; set; } = new();
    public List<string> SocialBoundaries { get; set; } = new();
    public string HumorStyle { get; set; } = "natural";
}

public sealed class EmotionalState
{
    public int Happiness { get; set; } = 55;
    public int Anger { get; set; }
    public int Anxiety { get; set; } = 10;
    public int Excitement { get; set; } = 15;
    public int Embarrassment { get; set; }
    public int Respect { get; set; } = 10;
    public int Gratitude { get; set; }
    public void Clamp()
    {
        Happiness=Math.Clamp(Happiness,0,100); Anger=Math.Clamp(Anger,0,100); Anxiety=Math.Clamp(Anxiety,0,100);
        Excitement=Math.Clamp(Excitement,0,100); Embarrassment=Math.Clamp(Embarrassment,0,100); Respect=Math.Clamp(Respect,0,100); Gratitude=Math.Clamp(Gratitude,0,100);
    }
}

public sealed class CharacterMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "episode";
    public string Content { get; set; } = "";
    public int Importance { get; set; } = 1;
    public int Day { get; set; } = 1;
    public float WorldMinutes { get; set; }
    public string Emotion { get; set; } = "neutral";
    public List<string> Tags { get; set; } = new();
    public List<string> People { get; set; } = new() { "player" };
    public string Source { get; set; } = "game";
    public bool Confirmed { get; set; } = true;
}

public sealed class PlayerStats
{
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int Mana { get; set; } = 80;
    public int MaxMana { get; set; } = 80;
    public int Energy { get; set; } = 100;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int Damage { get; set; } = 8;
    public int Defense { get; set; } = 2;
    public int Social { get; set; } = 3;
    public int Confidence { get; set; } = 2;
    public int Luck { get; set; } = 2;
    // Legacy attributes are retained for save compatibility.
    public int Charm { get; set; } = 3;
    public int Empathy { get; set; } = 3;
    public int Intelligence { get; set; } = 2;
    
    // --- Economy ---
    public int Coins { get; set; } = 50;

    public void Clamp()
    {
        MaxHealth = Math.Max(1, MaxHealth);
        MaxMana = Math.Max(0, MaxMana);
        Health = Math.Clamp(Health, 0, MaxHealth);
        Mana = Math.Clamp(Mana, 0, MaxMana);
        Energy = Math.Clamp(Energy, 0, 100);
        Level = Math.Max(1, Level);
        Experience = Math.Max(0, Experience);
        Damage = Math.Max(0, Damage);
        Defense = Math.Max(0, Defense);
        Coins = Math.Max(0, Coins);
    }
}
public sealed class GameSave
{
    public PlayerDeck Deck { get; set; } = new();
    public Dictionary<string, EncounterProgress> Encounters { get; set; } = new();
    public CombatState? ActiveCombat { get; set; }
    public List<string> CombatHistory { get; set; } = new();
    public int SaveVersion { get; set; } = 10;
    // Legacy fields are retained only to migrate existing slot files safely.
    public CharacterData? Character { get; set; } = null; public CharacterState? State { get; set; } = null;
    public List<string> CharacterIds { get; set; } = new(); public Dictionary<string, CharacterState> CharacterStates { get; set; } = new(); public PlayerStats Player { get; set; } = new();
    public int Day { get; set; } = 1; public string Period { get; set; } = "Morning"; public int ActionsLeft { get; set; } = 3;
    public List<string> UnlockedCinematics { get; set; } = new(); public List<string> Flags { get; set; } = new();
    public GameSettings Settings { get; set; } = new();
    public float PlayerX { get; set; } = 0;
    public float PlayerZ { get; set; } = 5;
    public Dictionary<string, string> MomentDetails { get; set; } = new();

    // --- New fields for procedural world ---
    /// <summary>Persistent world seed. Generated once on new game.</summary>
    public long WorldSeed { get; set; } = 0;
    /// <summary>Version of the chunk generator that created this world.</summary>
    public int GeneratorVersion { get; set; } = 1;
    /// <summary>Player camera Y rotation in radians.</summary>
    public float PlayerRotationY { get; set; } = 0f;
    /// <summary>Minutes since midnight, persisted independently from display labels.</summary>
    public float WorldMinutes { get; set; } = -1f;
    public bool FirstPerson { get; set; } = true;
    /// <summary>Narrative identity only. The player never receives a visual avatar.</summary>
    public string PlayerName { get; set; } = "Viajante";
    public WorldLore WorldLore { get; set; } = new();
    public List<AtlasNodeData> AtlasNodes { get; set; } = new();
    public List<string> RecentEvents { get; set; } = new();
    /// <summary>Zero-based endless expedition number.</summary>
    public int ExpeditionIndex { get; set; }
    /// <summary>Backdrop selected for the current procedural Atlas expedition.</summary>
    public string AtlasBackgroundPath { get; set; } = "res://Assets/ArtKit/Backgrounds/atlas_map.png";
    /// <summary>Relationship characters invited to the persistent camp.</summary>
    public List<string> CampResidents { get; set; } = new();
    /// <summary>Independent campaign folder under user://worlds.</summary>
    public string WorldId { get; set; } = "";
    public string WorldPrompt { get; set; } = "";
    public WorldDefinition? WorldDefinition { get; set; }
    public List<string> SelectedLibraryIds { get; set; } = new();
    public string CreatedAt { get; set; } = "";
    public string LastPlayedAt { get; set; } = "";
    public int PlayedSeconds { get; set; }
}

public sealed class WorldLore
{
    public string RegionName { get; set; } = "Terras sem nome";
    public string Premise { get; set; } = "Uma estrada antiga chama viajantes para segredos esquecidos.";
    public string Threat { get; set; } = "Algo desperta nas ruínas.";
    public List<string> Factions { get; set; } = new();
    public List<string> Rumors { get; set; } = new();
    public string Atmosphere { get; set; } = "melancólica e misteriosa";
}

public enum AtlasNodeKind { Event, Scene, Combat, Character, Merchant, Rest, Mystery, Boss }
public enum AtlasNodeStatus { Locked, Available, Completed }

public sealed class AtlasNodeData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateId { get; set; } = "";
    public string Title { get; set; } = "Novo caminho";
    public AtlasNodeKind Kind { get; set; } = AtlasNodeKind.Event;
    public AtlasNodeStatus Status { get; set; } = AtlasNodeStatus.Locked;
    public float X { get; set; }
    public float Y { get; set; }
    public string BackgroundId { get; set; } = "forest_dark";
    public string ContentId { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Connections { get; set; } = new();
    public bool Persistent { get; set; }
    public int Risk { get; set; } = 1;
    public int Layer { get; set; } = 1;
}
public sealed class GameSettings { public float Volume { get; set; } = 0.8f; public float TextSpeed { get; set; } = 1f; public bool AutoAdvance { get; set; } = false; public bool UseOnlineAi { get; set; } = false; public string Provider { get; set; } = "Groq"; public string Endpoint { get; set; } = "https://api.groq.com/openai/v1"; public string Model { get; set; } = "openai/gpt-oss-20b"; public float Temperature { get; set; } = 0.75f; public int MaxResponseTokens { get; set; } = 180; public int ContextMemorySize { get; set; } = 5; public float WorldTimeScale { get; set; } = 1f; public bool WorldTimePaused { get; set; }
    /// <summary>When true, uses OpenRouter instead of Groq (allows uncensored models).</summary>
    public bool UseOpenRouter { get; set; } = false;
    /// <summary>OpenRouter model slug, e.g. "mistralai/mistral-7b-instruct:free" or "cognitivecomputations/dolphin-mixtral-8x7b".</summary>
    public string OpenRouterModel { get; set; } = "cognitivecomputations/dolphin-mistral-24b-venice-edition:free";
}
