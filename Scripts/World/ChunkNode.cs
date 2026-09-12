using Godot;
namespace Heartbeat;

/// <summary>Scene node representing a loaded chunk.</summary>
public partial class ChunkNode : Node3D
{
    public List<ActivityPoint> ActivityPoints { get; } = new();
}
