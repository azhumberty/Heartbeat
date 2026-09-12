using Godot;
namespace Heartbeat;

/// <summary>
/// Manages chunk streaming around the player.
/// Loads a 3×3 neighborhood, unloads distant chunks with hysteresis.
/// Generation is distributed across frames to avoid stuttering.
/// </summary>
public partial class ChunkManager : Node3D
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

    int BuildChunkIncremental(PendingChunk pending, int budget)
    {
        pending.Node ??= new ChunkNode { Name = $"Chunk_{pending.Coord.X}_{pending.Coord.Y}" };
        var data = pending.Data;
        var node = pending.Node;

        // Instantiate elements
        while (pending.ElementIndex < data.Elements.Count && budget > 0)
        {
            var element = data.Elements[pending.ElementIndex];
            InstantiateElement(node, element);
            pending.ElementIndex++;
            budget--;
        }

        // Instantiate lights
        while (pending.LightIndex < data.Lights.Count && budget > 0)
        {
            var light = data.Lights[pending.LightIndex];
            var lamp = new OmniLight3D
            {
                Position = light.Position,
                LightColor = light.Color,
                LightEnergy = light.Energy,
                OmniRange = light.Range
            };
            lamp.AddToGroup("artificial_lights");
            node.AddChild(lamp);
            pending.LightIndex++;
            budget--;
        }

        // Instantiate activity points
        while (pending.ActivityIndex < data.ActivityPoints.Count && budget > 0)
        {
            var def = data.ActivityPoints[pending.ActivityIndex];
            var ap = new ActivityPoint
            {
                Position = def.Position,
                ActivityType = def.ActivityType,
                FacingAngle = def.FacingAngle,
                LocationName = def.LocationName,
                ChunkCoord = data.Coord
            };
            node.AddChild(ap);
            node.ActivityPoints.Add(ap);
            pending.ActivityIndex++;
            budget--;
        }

        pending.IsComplete = pending.ElementIndex >= data.Elements.Count
                          && pending.LightIndex >= data.Lights.Count
                          && pending.ActivityIndex >= data.ActivityPoints.Count;
        return budget;
    }

    void InstantiateElement(Node3D parent, object element)
    {
        switch (element)
        {
            case TerrainDef terrain:
                InstantiateTerrain(parent, terrain);
                break;

            case TrailDef trail:
                InstantiateTrail(parent, trail);
                break;

            case BoxDef box:
                var mesh = new MeshInstance3D
                {
                    Position = box.Position,
                    Mesh = new BoxMesh { Size = box.Size },
                    MaterialOverride = SurfaceMaterials.For(box.Color)
                };
                parent.AddChild(mesh);
                if (box.HasCollision)
                {
                    var body = new StaticBody3D { Position = box.Position };
                    body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = box.Size } });
                    parent.AddChild(body);
                }
                break;

            case SphereDef sphere:
                parent.AddChild(new MeshInstance3D
                {
                    Position = sphere.Position,
                    Mesh = new SphereMesh { Radius = sphere.Radius, Height = sphere.Height },
                    MaterialOverride = SurfaceMaterials.For(sphere.Color)
                });
                break;

            case CylinderDef cylinder:
                var cylinderMesh = new MeshInstance3D
                {
                    Position = cylinder.Position,
                    Rotation = cylinder.Rotation,
                    Mesh = new CylinderMesh { TopRadius = cylinder.Radius * .8f, BottomRadius = cylinder.Radius, Height = cylinder.Height, RadialSegments = 10 },
                    MaterialOverride = SurfaceMaterials.For(cylinder.Color)
                };
                parent.AddChild(cylinderMesh);
                if (cylinder.HasCollision)
                {
                    var body = new StaticBody3D { Position = cylinder.Position, Rotation = cylinder.Rotation };
                    body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = cylinder.Radius, Height = cylinder.Height } });
                    parent.AddChild(body);
                }
                break;

            case ForestBatchDef batch:
                InstantiateForestBatch(parent, batch);
                break;

            case PoiDef poi:
                var packed = GD.Load<PackedScene>(poi.ScenePath);
                if (packed?.Instantiate() is Node3D instance)
                {
                    instance.Position = poi.Position;
                    instance.Rotation = new Vector3(0, poi.RotationY, 0);
                    instance.SetMeta("poi_id", poi.PoiId);
                    instance.SetMeta("event_hook", poi.EventHook);
                    parent.AddChild(instance);
                }
                break;
        }
    }

    static void InstantiateForestBatch(Node3D parent, ForestBatchDef batch)
    {
        if (batch.Items.Count == 0) return;
        PrimitiveMesh primitive = batch.Kind switch
        {
            ForestKind.Trunk => new CylinderMesh { TopRadius = .36f, BottomRadius = .5f, Height = 1, RadialSegments = 8 },
            ForestKind.Crown => new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 12, Rings = 6 },
            ForestKind.Conifer => new CylinderMesh { TopRadius = 0, BottomRadius = .5f, Height = 1, RadialSegments = 10 },
            ForestKind.Bush => new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 10, Rings = 5 },
            ForestKind.Stone => new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 },
            ForestKind.Leaf => new BoxMesh { Size = new Vector3(1, .012f, 1) },
            _ => new QuadMesh { Size = Vector2.One }
        };
        string color = batch.Kind switch { ForestKind.Trunk => "49392b", ForestKind.Crown => "315b38", ForestKind.Conifer => "1f4030", ForestKind.Bush => "3b643d", ForestKind.Stone => "667068", _ => "416f3d" };
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = primitive, InstanceCount = batch.Items.Count };
        for (int i = 0; i < batch.Items.Count; i++)
        {
            var item = batch.Items[i];
            var basis = new Basis(Vector3.Up, item.Yaw).Scaled(item.Scale);
            multi.SetInstanceTransform(i, new Transform3D(basis, item.Position));
        }
        Material material = batch.Kind switch
        {
            ForestKind.Trunk => SurfaceMaterials.Bark(),
            ForestKind.Stone => SurfaceMaterials.MossyRock(),
            _ => SurfaceMaterials.Forest(color, batch.Kind)
        };
        float range = batch.Kind switch
        {
            ForestKind.Grass or ForestKind.Leaf => 36,
            ForestKind.Bush => 48,
            ForestKind.Stone => 58,
            _ => 88
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Name = "Forest" + batch.Kind,
            Multimesh = multi,
            MaterialOverride = material,
            VisibilityRangeEnd = range,
            CastShadow = batch.Kind is ForestKind.Trunk or ForestKind.Crown or ForestKind.Conifer ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off
        });
        if (batch.Kind == ForestKind.Trunk)
            foreach (var basePos in batch.CollisionBases)
            {
                var body = new StaticBody3D { Position = basePos + new Vector3(0, 1.5f, 0) };
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .48f, Height = 3 } });
                parent.AddChild(body);
            }
    }

    static void InstantiateTerrain(Node3D parent, TerrainDef terrain)
    {
        int resolution = Math.Max(4, terrain.Resolution);
        float step = ChunkGenerator.ChunkSize / (float)resolution;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            float x0 = terrain.Origin.X - ChunkGenerator.ChunkSize / 2f + x * step;
            float x1 = x0 + step;
            float z0 = terrain.Origin.Z - ChunkGenerator.ChunkSize / 2f + z * step;
            float z1 = z0 + step;
            AddTerrainVertex(surface, x0, z0, terrain.Seed);
            AddTerrainVertex(surface, x1, z1, terrain.Seed);
            AddTerrainVertex(surface, x1, z0, terrain.Seed);
            AddTerrainVertex(surface, x0, z0, terrain.Seed);
            AddTerrainVertex(surface, x0, z1, terrain.Seed);
            AddTerrainVertex(surface, x1, z1, terrain.Seed);
        }
        var mesh = surface.Commit();
        parent.AddChild(new MeshInstance3D
        {
            Name = "NaturalTerrain",
            Mesh = mesh,
            MaterialOverride = SurfaceMaterials.ForestGround(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        });
        var collision = mesh.CreateTrimeshShape();
        if (collision is ConcavePolygonShape3D concave) concave.BackfaceCollision = true;
        var body = new StaticBody3D { Name = "TerrainCollision" };
        body.AddChild(new CollisionShape3D { Shape = collision });
        parent.AddChild(body);
    }

    static void AddTerrainVertex(SurfaceTool surface, float x, float z, long seed)
    {
        const float epsilon = .25f;
        float height = TerrainHeight.Sample(x, z, seed);
        var normal = new Vector3(
            TerrainHeight.Sample(x - epsilon, z, seed) - TerrainHeight.Sample(x + epsilon, z, seed),
            epsilon * 2,
            TerrainHeight.Sample(x, z - epsilon, seed) - TerrainHeight.Sample(x, z + epsilon, seed)).Normalized();
        surface.SetNormal(normal);
        surface.SetUV(new Vector2(x / 6f, z / 6f));
        surface.AddVertex(new Vector3(x, height, z));
    }

    static void InstantiateTrail(Node3D parent, TrailDef trail)
    {
        int segments = Math.Max(4, trail.Segments);
        float step = ChunkGenerator.ChunkSize / (float)segments;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float z0 = trail.Origin.Z - ChunkGenerator.ChunkSize / 2f + i * step;
            float z1 = z0 + step;
            float cx0 = trail.Origin.X + Mathf.Sin((z0 + trail.Seed % 71) * .09f) * .55f;
            float cx1 = trail.Origin.X + Mathf.Sin((z1 + trail.Seed % 71) * .09f) * .55f;
            AddTrailVertex(surface, cx0 - trail.HalfWidth, z0, trail.Seed);
            AddTrailVertex(surface, cx1 + trail.HalfWidth, z1, trail.Seed);
            AddTrailVertex(surface, cx0 + trail.HalfWidth, z0, trail.Seed);
            AddTrailVertex(surface, cx0 - trail.HalfWidth, z0, trail.Seed);
            AddTrailVertex(surface, cx1 - trail.HalfWidth, z1, trail.Seed);
            AddTrailVertex(surface, cx1 + trail.HalfWidth, z1, trail.Seed);
        }
        parent.AddChild(new MeshInstance3D
        {
            Name = "ForestTrail",
            Mesh = surface.Commit(),
            MaterialOverride = SurfaceMaterials.For("5b554d"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    static void AddTrailVertex(SurfaceTool surface, float x, float z, long seed)
    {
        surface.SetNormal(Vector3.Up);
        surface.SetUV(new Vector2(x / 3f, z / 3f));
        surface.AddVertex(new Vector3(x, TerrainHeight.Sample(x, z, seed) + .035f, z));
    }

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

/// <summary>Scene node representing a loaded chunk.</summary>
public partial class ChunkNode : Node3D
{
    public List<ActivityPoint> ActivityPoints { get; } = new();
}
