namespace Heartbeat;

// Small focused facades keep MVP systems replaceable as content grows.
public sealed class CharacterManager { public CharacterData Active { get; private set; } = new(); public void Select(CharacterData character) => Active = character; }
public sealed class RelationshipSystem
{
    public const int DailyCap = 8;
    public void Apply(CharacterState state, int affection, int trust, int romance = 0, int attraction = 0, int day = 0)
    {
        if (state.SocialGainDay != day)
        {
            state.SocialGainDay = day;
            state.DailyAffectionGained = 0;
            state.DailyTrustGained = 0;
        }
        int aff = affection > 0 ? Math.Min(affection, Math.Max(0, DailyCap - state.DailyAffectionGained)) : affection;
        int tru = trust > 0 ? Math.Min(trust, Math.Max(0, DailyCap - state.DailyTrustGained)) : trust;
        if (aff > 0) state.DailyAffectionGained += aff;
        if (tru > 0) state.DailyTrustGained += tru;
        state.Affection += aff;
        state.Trust += tru;
        state.Romance += romance;
        state.Attraction += attraction;
        int respect = state.Emotions?.Respect ?? 0;
        state.Relationship = state.Romance >= 65 ? "Partner"
            : state.Romance >= 40 ? "Dating"
            : state.Romance >= 20 && state.Trust >= 25 ? "RomanticInterest"
            : state.Affection >= 45 ? "CloseFriend"
            : state.Affection >= 25 || (state.Affection >= 15 && respect >= 40) ? "Friend"
            : state.Affection >= 10 || respect >= 30 ? "Acquaintance"
            : "Stranger";
        state.Clamp();
    }
}
public sealed class MemoryManager
{
    public void Remember(CharacterState state,string memory,bool important=false)
    {
        if(string.IsNullOrWhiteSpace(memory))return; SocialModelMigrator.Migrate(state);
        state.RecentMemories.Insert(0,memory); if(state.RecentMemories.Count>8)state.RecentMemories.RemoveAt(8);
        if(important&&!state.ImportantMemories.Contains(memory))state.ImportantMemories.Insert(0,memory);
        if(!state.Memories.Any(m=>m.Content==memory))state.Memories.Add(new CharacterMemory { Content=memory,Kind=important?"episode":"short_term",Importance=important?5:2,Source="game_event",Confirmed=true,Tags=new(){important?"important_event":"conversation"} });
    }
}
public sealed class TimeManager { public static readonly string[] Periods = { "Morning", "Afternoon", "Evening", "Night" }; public string Next(GameSave save) { var index = (Array.IndexOf(Periods, save.Period) + 1) % Periods.Length; if (index == 0) save.Day++; return save.Period = Periods[index]; } }

/// <summary>
/// Determines NPC location based on time period.
/// Uses CharacterData.Schedule if available, otherwise falls back to defaults.
/// </summary>
public sealed class CharacterScheduleSystem
{
    /// <summary>Default location for a period (when character has no custom schedule).</summary>
    public string LocationFor(string period) => period switch { "Morning" => "Street", "Afternoon" => "Cafe", "Evening" => "Park", _ => "Home" };

    /// <summary>Get the target location for a specific character at a given period.</summary>
    public string LocationFor(CharacterData data, string period)
    {
        // Check character-specific schedule first
        var entry = data.Schedule.FirstOrDefault(s => s.Period == period);
        if (entry != null) return entry.TargetLocation;
        return LocationFor(period);
    }

    /// <summary>Get the activity type for a specific character at a given period.</summary>
    public string ActivityFor(CharacterData data, string period)
    {
        var entry = data.Schedule.FirstOrDefault(s => s.Period == period);
        if (entry != null) return entry.Activity;
        return period switch { "Morning" => "Walking", "Afternoon" => "Working", "Evening" => "Resting", _ => "Idle" };
    }

    public void Update(CharacterState state, string period) => state.CurrentLocation = LocationFor(period);
    public void Update(CharacterData data, CharacterState state, string period) => state.CurrentLocation = LocationFor(data, period);
}

public sealed class EventManager { public bool CanPlayRainEvent(GameSave game) => game.State != null && game.State.Affection >= 20 && !game.UnlockedCinematics.Contains("rainy_evening"); public bool CanUnlockParkDate(CharacterState state, string period) => state.Affection >= 40 && state.Trust >= 30 && state.CurrentLocation == "Park" && period == "Night" && state.CurrentDesire == "spend_time"; }
public sealed class ImageImportService { public string Import(string source, string destination) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true); return destination; } }
public sealed class CinematicManager { public bool IsUnlocked(GameSave game, string id) => game.UnlockedCinematics.Contains(id); }
public sealed class GalleryManager { public IEnumerable<string> Entries(GameSave game) => game.UnlockedCinematics; }
public sealed class AudioManager { public float MasterVolume { get; set; } = 1f; }
