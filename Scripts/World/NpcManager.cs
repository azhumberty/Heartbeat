using Godot;
using System;
using System.Collections.Generic;

namespace Heartbeat;

public partial class NpcManager : Node
{
    GameSave _game;
    WorldTimeSystem _time;
    Action<NpcActor> _onInteract;

    readonly Dictionary<string, NpcActor> _actors = new();
    
    public NpcManager(GameSave game, WorldTimeSystem time, Action<NpcActor> onInteract)
    {
        _game = game;
        _time = time;
        _onInteract = onInteract;
        Name = "NpcManager";
    }

    public override void _Ready()
    {
        // Load initial state
        var db = new CharacterRepository();
        foreach (var cData in db.List())
        {
            if (!_game.CharacterStates.TryGetValue(cData.Id, out var state))
            {
                state = new CharacterState { CurrentLocation = "merchant" };
                _game.CharacterStates[cData.Id] = state;
            }
            
            var actor = new NpcActor { Data = cData, State = state };
            _actors[cData.Id] = actor;
            AddChild(actor);
        }
    }

    public NpcActor? GetNpc(string id) => _actors.TryGetValue(id, out var a) ? a : null;

    public void AdvancePeriod(string period)
    {
        // Shuffle locations based on logic or random for the demo
        // E.g. merchant stays at merchant
    }
}
