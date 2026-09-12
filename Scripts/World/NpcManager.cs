using Godot;
namespace Heartbeat;

/// <summary>
/// Manages NPC logical identity separately from visual representation.
/// Handles load/unload of chunks without losing NPC state.
/// Coordinates NPC behavior, pathfinding, and activity point usage.
/// </summary>
public partial class NpcManager : Node
{
    // ── Configuration ──
    public int MaxActiveNpcs { get; set; } = 8;

    // ── Dependencies ──
    public ChunkManager? Chunks { get; set; }
    public PortraitCache Portraits { get; set; } = new();

    // ── State ──
    /// <summary>All known NPC logical states, keyed by character ID.</summary>
    readonly Dictionary<string, NpcLogicState> _logicStates = new();

    /// <summary>Currently instantiated NPC actors, keyed by character ID.</summary>
    readonly Dictionary<string, NpcActor> _activeActors = new();

    readonly CharacterDesireSystem _desires = new();
    readonly CharacterScheduleSystem _schedule = new();
    readonly Random _rng = new();

    /// <summary>All active NPC actors.</summary>
    public IReadOnlyCollection<NpcActor> ActiveActors => _activeActors.Values;

    /// <summary>Register a character for management.</summary>
    public void Register(CharacterData data, CharacterState state)
    {
        if (_logicStates.ContainsKey(data.Id)) return;
        _logicStates[data.Id] = new NpcLogicState
        {
            Data = data,
            State = state,
            WorldX = state.WorldX,
            WorldZ = state.WorldZ,
            CurrentActivity = state.CurrentActivity ?? "Idle"
        };
    }

    /// <summary>Get the actor for a character ID, or null if not loaded.</summary>
    public NpcActor? GetActor(string characterId)
        => _activeActors.TryGetValue(characterId, out var actor) ? actor : null;

    /// <summary>Update NPC loading/unloading based on chunk visibility.</summary>
    public void UpdateVisibility(Vector3 playerPos)
    {
        if (Chunks == null) return;

        foreach (var kv in _logicStates)
        {
            var logic = kv.Value;
            var npcPos = new Vector3(logic.WorldX, 0, logic.WorldZ);
            var npcChunk = ChunkManager.WorldToChunk(npcPos);
            var playerChunk = ChunkManager.WorldToChunk(playerPos);
            var dist = Math.Max(Math.Abs(npcChunk.X - playerChunk.X), Math.Abs(npcChunk.Y - playerChunk.Y));

            bool shouldBeLoaded = dist <= Chunks.LoadRadius + 1 && _activeActors.Count < MaxActiveNpcs;
            bool isLoaded = _activeActors.ContainsKey(kv.Key);

            if (shouldBeLoaded && !isLoaded)
                SpawnActor(logic);
            else if (!shouldBeLoaded && isLoaded && dist > Chunks.LoadRadius + Chunks.UnloadMargin + 1)
                DespawnActor(kv.Key);
        }
    }

    void SpawnActor(NpcLogicState logic)
    {
        if (_activeActors.ContainsKey(logic.Data.Id)) return; // Never duplicate

        var validPos = Chunks?.ResolveValidPosition(new Vector3(logic.WorldX, 0, logic.WorldZ))
                       ?? new Vector3(logic.WorldX, 0.1f, logic.WorldZ);

        var actor = new NpcActor
        {
            Data = logic.Data,
            State = logic.State,
            Portraits = Portraits,
            Position = validPos
        };
        AddChild(actor);
        _activeActors[logic.Data.Id] = actor;

        // Resume activity
        if (logic.CurrentActivity == "Walking" && logic.HasDestination)
            actor.WalkTo(new Vector3(logic.DestX, 0, logic.DestZ));
    }

    void DespawnActor(string characterId)
    {
        if (!_activeActors.TryGetValue(characterId, out var actor)) return;

        // Save position back to logical state
        if (_logicStates.TryGetValue(characterId, out var logic))
        {
            logic.WorldX = actor.Position.X;
            logic.WorldZ = actor.Position.Z;
            logic.CurrentActivity = actor.CurrentNpcState;
            // Preserve state — memories, relationship, everything stays in CharacterState
            logic.State.WorldX = logic.WorldX;
            logic.State.WorldZ = logic.WorldZ;
            logic.State.CurrentActivity = logic.CurrentActivity;
        }

        actor.QueueFree();
        _activeActors.Remove(characterId);
    }

    /// <summary>Update NPC behavior: pick destinations, walk to activity points.</summary>
    public void UpdateBehavior(string period, float delta)
    {
        foreach (var kv in _logicStates)
        {
            var logic = kv.Value;

            if (_activeActors.TryGetValue(kv.Key, out var actor))
            {
                // Active NPC: physical behavior
                if (actor.IsTalking) continue;

                if (actor.IsAtDestination && !logic.HasDestination)
                {
                    // Pick a new activity
                    logic.IdleTimer += delta;
                    if (logic.IdleTimer > logic.IdleDuration)
                    {
                        logic.IdleTimer = 0;
                        logic.IdleDuration = 8f + _rng.Next(0, 15);
                        PickDestination(logic, actor, period);
                    }
                }
                else if (actor.IsAtDestination && logic.HasDestination)
                {
                    // Arrived at activity point
                    logic.HasDestination = false;
                    logic.IdleTimer = 0;
                    logic.IdleDuration = 10f + _rng.Next(0, 20);
                }
            }
            else
            {
                // Distant NPC: lightweight simulation (no physics)
                // Just update logical position toward schedule location
                SimulateDistant(logic, period);
            }
        }
    }

    void PickDestination(NpcLogicState logic, NpcActor actor, string period)
    {
        if (Chunks == null) return;

        // Determine target location from schedule
        var targetLocation = GetScheduleLocation(logic.Data, period);
        logic.State.CurrentLocation = targetLocation;

        // Find an activity point
        var ap = Chunks.FindNearestActivityPoint(actor.GlobalPosition, targetLocation);
        if (ap == null)
            ap = Chunks.FindActivityPoint(targetLocation);
        if (ap == null)
            ap = Chunks.FindActivityPoint("Street"); // fallback to any street point

        if (ap != null && ap.TryClaim())
        {
            logic.DestX = ap.GlobalPosition.X;
            logic.DestZ = ap.GlobalPosition.Z;
            logic.HasDestination = true;
            logic.ClaimedPoint = ap;
            actor.WalkTo(ap.GlobalPosition);
        }
    }

    void SimulateDistant(NpcLogicState logic, string period)
    {
        // Lightweight: just update logical location based on schedule
        var targetLocation = GetScheduleLocation(logic.Data, period);
        logic.State.CurrentLocation = targetLocation;
        logic.CurrentActivity = "Idle";

        // Move logical position toward schedule area (gradual drift)
        // This is approximate — when the NPC becomes visible, it spawns at this position
    }

    string GetScheduleLocation(CharacterData data, string period)
    {
        // Check character-specific schedule first
        var entry = data.Schedule.FirstOrDefault(s => s.Period == period);
        if (entry != null) return entry.TargetLocation;

        // Fall back to default schedule
        return _schedule.LocationFor(period);
    }

    /// <summary>Advance all NPCs to a new time period.</summary>
    public void AdvancePeriod(string period)
    {
        foreach (var kv in _logicStates)
        {
            var logic = kv.Value;
            logic.State.CurrentLocation = GetScheduleLocation(logic.Data, period);
            logic.State.Energy = Math.Min(100, logic.State.Energy + 12);
            logic.State.CurrentDesire = _desires.Choose(logic.Data, logic.State, period);
            logic.HasDestination = false;
            logic.IdleTimer = 0;

            // Release claimed activity point
            logic.ClaimedPoint?.Release();
            logic.ClaimedPoint = null;

            // If actor is loaded, stop and let it pick a new destination
            if (_activeActors.TryGetValue(kv.Key, out var actor))
            {
                actor.StopWalking();
                actor.RefreshPortrait();
            }
        }
    }

    /// <summary>Sync all logical positions from active actors.</summary>
    public void SyncPositions()
    {
        foreach (var kv in _activeActors)
        {
            if (!_logicStates.TryGetValue(kv.Key, out var logic)) continue;
            logic.WorldX = kv.Value.Position.X;
            logic.WorldZ = kv.Value.Position.Z;
            logic.State.WorldX = logic.WorldX;
            logic.State.WorldZ = logic.WorldZ;
        }
    }

    /// <summary>Find the nearest active NPC to a position within interaction radius.</summary>
    public NpcActor? FindNearestInteractable(Vector3 pos)
    {
        NpcActor? best = null;
        float bestDist = float.MaxValue;
        foreach (var actor in _activeActors.Values)
        {
            var dist = actor.GlobalPosition.DistanceTo(pos);
            if (dist < actor.Data.InteractionRadius && dist < bestDist)
            {
                bestDist = dist;
                best = actor;
            }
        }
        return best;
    }

    sealed class NpcLogicState
    {
        public CharacterData Data = new();
        public CharacterState State = new();
        public float WorldX, WorldZ;
        public string CurrentActivity = "Idle";
        public bool HasDestination;
        public float DestX, DestZ;
        public float IdleTimer;
        public float IdleDuration = 10f;
        public ActivityPoint? ClaimedPoint;
    }
}
