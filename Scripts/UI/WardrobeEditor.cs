using Godot;
using System;

namespace Heartbeat;

public partial class WardrobeEditor : Control
{
    public CharacterData Data { get; set; } = null!;
    public Action? Closed;
}
