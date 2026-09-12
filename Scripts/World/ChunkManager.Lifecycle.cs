using Godot;
namespace Heartbeat;

public partial class ChunkManager
{
    void UnloadChunk(Vector2I coord)
    {
        if (!_loadedChunks.TryGetValue(coord, out var chunk)) return;
        // Release activity points
        foreach (var ap in chunk.ActivityPoints)
            ap.QueueFree();
        chunk.QueueFree();
        _loadedChunks.Remove(coord);
    }

    /// <summary>Whether a chunk is currently loaded.</summary>
    public bool IsChunkLoaded(Vector2I coord) => _loadedChunks.ContainsKey(coord);

    /// <summary>Get nearest valid walkable position (on a loaded chunk ground).</summary>
    public Vector3 ResolveValidPosition(Vector3 pos)
    {
        var chunk = WorldToChunk(pos);
        if (_loadedChunks.ContainsKey(chunk)) return new Vector3(pos.X, TerrainHeight.Sample(pos.X, pos.Z, Generator.Seed) + .1f, pos.Z);

        // Fallback: find nearest loaded chunk center
        var nearest = _loadedChunks.Keys.OrderBy(c =>
            (c - chunk).LengthSquared()).FirstOrDefault();

        return new Vector3(
            nearest.X * ChunkGenerator.ChunkSize,
            TerrainHeight.Sample(nearest.X * ChunkGenerator.ChunkSize, nearest.Y * ChunkGenerator.ChunkSize, Generator.Seed) + .1f,
            nearest.Y * ChunkGenerator.ChunkSize);
    }

    // ── Internal types ──

    sealed class PendingChunk
    {
        public Vector2I Coord;
        public ChunkData Data = null!;
        public ChunkNode? Node;
        public int ElementIndex;
        public int LightIndex;
        public int ActivityIndex;
        public bool IsComplete;
    }
}
