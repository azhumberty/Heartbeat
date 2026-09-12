using Godot;
namespace Heartbeat;

/// <summary>
/// A point in the world where NPCs can perform activities.
/// Placed during chunk generation and referenced by NPC AI.
/// </summary>
public partial class ActivityPoint : Node3D
{
    /// <summary>Type of activity: Work, Rest, Wait, Socialize, Eat</summary>
    public string ActivityType { get; set; } = "Wait";

    /// <summary>Orientation the NPC should face when using this point (Y rotation in radians).</summary>
    public float FacingAngle { get; set; }

    /// <summary>Maximum NPCs that can use this point simultaneously.</summary>
    public int MaxOccupants { get; set; } = 1;

    /// <summary>Duration in game-time seconds the NPC should spend here.</summary>
    public float Duration { get; set; } = 30f;

    /// <summary>Chunk coordinates this point belongs to.</summary>
    public Vector2I ChunkCoord { get; set; }

    /// <summary>Logical location name: Cafe, Park, Street, etc.</summary>
    public string LocationName { get; set; } = "Street";

    /// <summary>Current number of NPCs using this point.</summary>
    int _currentOccupants;

    /// <summary>Try to claim this point for an NPC. Returns true if successful.</summary>
    public bool TryClaim()
    {
        if (_currentOccupants >= MaxOccupants) return false;
        _currentOccupants++;
        return true;
    }

    /// <summary>Release this point when NPC leaves.</summary>
    public void Release()
    {
        _currentOccupants = Math.Max(0, _currentOccupants - 1);
    }

    /// <summary>Whether this point has room for another NPC.</summary>
    public bool IsAvailable => _currentOccupants < MaxOccupants;
}
