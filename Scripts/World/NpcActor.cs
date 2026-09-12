using Godot;

namespace Heartbeat;

/// <summary>
/// Logical representation of an NPC.
/// </summary>
public partial class NpcActor : Node
{
    public CharacterData Data { get; set; } = new();
    public CharacterState State { get; set; } = new();
    public bool IsTalking { get; set; }
    public PortraitCache Portraits { get; set; } = new();

    public void PauseForDialogue()
    {
        IsTalking = true;
    }

    public void ResumeAfterDialogue()
    {
        IsTalking = false;
    }

    public void RefreshPortrait()
    {
    }
}
