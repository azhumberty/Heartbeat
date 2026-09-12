using Godot;
namespace Heartbeat;

public partial class ChunkManager
{

    // ── Configuration ──
    /// <summary>How many chunks around the player to keep loaded (radius). 1 = 3×3 grid.</summary>
    public int LoadRadius { get; set; } = 1;
    /// <summary>Extra margin before unloading (prevents load/unload thrashing).</summary>
    public int UnloadMargin { get; set; } = 1;
    /// <summary>Maximum chunks loaded at once.</summary>
    public int MaxChunks { get; set; } = 16;
    /// <summary>Maximum objects instantiated per frame during chunk building.</summary>
    public int ObjectsPerFrame { get; set; } = 20;

    public ChunkGenerator Generator { get; set; } = null!;

    // ── State ──
    readonly Dictionary<Vector2I, ChunkNode> _loadedChunks = new();
    readonly Queue<PendingChunk> _buildQueue = new();
    Vector2I _lastPlayerChunk = new(int.MaxValue, int.MaxValue);

    /// <summary>All activity points across loaded chunks.</summary>
    public IReadOnlyList<ActivityPoint> AllActivityPoints
    {
        get
        {
            var list = new List<ActivityPoint>();
            foreach (var chunk in _loadedChunks.Values)
                list.AddRange(chunk.ActivityPoints);
            return list;
        }
    }

    /// <summary>Find the nearest available activity point matching a location name.</summary>
    public ActivityPoint? FindNearestActivityPoint(Vector3 worldPos, string locationName)
    {
        ActivityPoint? best = null;
        var bestDist = float.MaxValue;
        foreach (var chunk in _loadedChunks.Values)
        {
            foreach (var ap in chunk.ActivityPoints)
            {
                if (ap.LocationName != locationName || !ap.IsAvailable) continue;
                var dist = worldPos.DistanceTo(ap.GlobalPosition);
                if (dist < bestDist) { bestDist = dist; best = ap; }
            }
        }
        return best;
    }

    /// <summary>Find any available activity point matching a location name.</summary>
    public ActivityPoint? FindActivityPoint(string locationName)
    {
        foreach (var chunk in _loadedChunks.Values)
            foreach (var ap in chunk.ActivityPoints)
                if (ap.LocationName == locationName && ap.IsAvailable)
                    return ap;
        return null;
    }

    /// <summary>Get the chunk coordinate for a world position.</summary>
    public static Vector2I WorldToChunk(Vector3 worldPos)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / ChunkGenerator.ChunkSize + 0.5f),
            Mathf.FloorToInt(worldPos.Z / ChunkGenerator.ChunkSize + 0.5f));
    }

    /// <summary>Update chunk loading based on player position. Call from _Process.</summary>
    public void UpdateAroundPlayer(Vector3 playerPos)
    {
        var playerChunk = WorldToChunk(playerPos);
        if (playerChunk == _lastPlayerChunk && _buildQueue.Count == 0) return;
        _lastPlayerChunk = playerChunk;

        // Request loading of nearby chunks
        for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
            for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                var coord = playerChunk + new Vector2I(dx, dz);
                if (!_loadedChunks.ContainsKey(coord) && !_buildQueue.Any(p => p.Coord == coord))
                {
                    if (_loadedChunks.Count + _buildQueue.Count < MaxChunks)
                        EnqueueChunk(coord);
                }
            }

        // Unload distant chunks (with hysteresis margin)
        var unloadRadius = LoadRadius + UnloadMargin;
        var toUnload = new List<Vector2I>();
        foreach (var kv in _loadedChunks)
        {
            var dist = Math.Max(Math.Abs(kv.Key.X - playerChunk.X), Math.Abs(kv.Key.Y - playerChunk.Y));
            if (dist > unloadRadius)
                toUnload.Add(kv.Key);
        }
        foreach (var coord in toUnload)
            UnloadChunk(coord);
    }

    void EnqueueChunk(Vector2I coord)
    {
        // Generate data (deterministic, fast)
        var data = Generator.Generate(coord);
        _buildQueue.Enqueue(new PendingChunk { Coord = coord, Data = data });
    }

    /// <summary>Process the build queue, instantiating a limited number of objects per frame.</summary>
    public void ProcessBuildQueue()
    {
        if (_buildQueue.Count == 0) return;

        int budget = ObjectsPerFrame;
        while (_buildQueue.Count > 0 && budget > 0)
        {
            var pending = _buildQueue.Peek();
            budget = BuildChunkIncremental(pending, budget);
            if (pending.IsComplete)
            {
                _buildQueue.Dequeue();
                var chunkNode = pending.Node!;
                _loadedChunks[pending.Coord] = chunkNode;
                AddChild(chunkNode);
            }
        }
    }

}
