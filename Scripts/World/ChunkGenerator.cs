using Godot;
namespace Heartbeat;

/// <summary>Deterministic forest layout built from lightweight batches and reusable POIs.</summary>
public sealed class ChunkGenerator
{
    public const int ChunkSize = 32;
    public const int GeneratorVersion = 3;
    readonly long _seed;
    public long Seed => _seed;
    public ChunkGenerator(long seed) => _seed = seed;

    public ChunkData Generate(Vector2I coord)
    {
        var chunk = new ChunkData { Coord = coord };
        var rng = new Random(DeterministicHash(coord.X, coord.Y));
        var origin = new Vector3(coord.X * ChunkSize, 0, coord.Y * ChunkSize);
        BuildForestFloor(chunk, rng, origin);
        BuildForestScatter(chunk, rng, origin, coord == Vector2I.Zero);
        BuildPoi(chunk, origin, coord);
        AddActivityPoints(chunk, origin, coord);
        return chunk;
    }

    void BuildForestFloor(ChunkData chunk, Random rng, Vector3 origin)
    {
        chunk.Add(new TerrainDef { Origin=origin,Seed=_seed,Resolution=16 });
        chunk.Add(new TrailDef { Origin=origin,Seed=_seed,HalfWidth=1.25f,Segments=16 });
    }

    void BuildForestScatter(ChunkData chunk, Random rng, Vector3 origin, bool home)
    {
        // 2.5D Octopath/Doom billboards for trees (one sprite each) — cheaper than trunk+crown meshes.
        var pines = new ForestBatchDef { Kind = ForestKind.BillboardPine };
        var oaks = new ForestBatchDef { Kind = ForestKind.BillboardOak };
        var bushes = new ForestBatchDef { Kind = ForestKind.Bush };
        var stones = new ForestBatchDef { Kind = ForestKind.Stone };
        var grass = new ForestBatchDef { Kind = ForestKind.Grass };
        int treeCount = home ? 28 : 36;
        for (int i = 0; i < treeCount; i++)
        {
            float x = -15 + (float)rng.NextDouble() * 30, z = -15 + (float)rng.NextDouble() * 30;
            if (Math.Abs(x) < 2.7f || (home && new Vector2(x + 8, z + 8).Length() < 8.2f)) { i--; continue; }
            float h = 5.5f + (float)rng.NextDouble() * 4.8f;
            float yaw = (float)rng.NextDouble() * Mathf.Tau;
            var basePos = Ground(origin + new Vector3(x, 0, z));
            var batch = i % 3 == 0 ? pines : oaks;
            batch.Items.Add(new ForestInstance(basePos + new Vector3(0, h * .5f, 0), new(h * .45f, h, 1f), yaw));
            if (i < 9) batch.CollisionBases.Add(basePos);
        }
        for (int i = 0; i < 22; i++)
        {
            float x = -15 + (float)rng.NextDouble() * 30, z = -15 + (float)rng.NextDouble() * 30;
            if (Math.Abs(x) < 2.1f) continue;
            float s = .45f + (float)rng.NextDouble() * 1.15f;
            var p=Ground(origin+new Vector3(x,0,z)); p.Y+=s*.4f;
            bushes.Items.Add(new ForestInstance(p, new(s * 1.5f, s, s * 1.35f), (float)rng.NextDouble() * Mathf.Tau));
        }
        for (int i = 0; i < 20; i++)
        {
            float x = -15 + (float)rng.NextDouble() * 30, z = -15 + (float)rng.NextDouble() * 30;
            float s = .18f + (float)rng.NextDouble() * .55f;
            var p=Ground(origin+new Vector3(x,0,z)); p.Y+=s*.25f;
            stones.Items.Add(new ForestInstance(p, new(s * 1.45f, s, s), (float)rng.NextDouble() * Mathf.Tau));
        }
        for (int i = 0; i < 2200; i++)
        {
            float x = -15 + (float)rng.NextDouble() * 30, z = -15 + (float)rng.NextDouble() * 30;
            if (Math.Abs(x) < 1.4f) continue;
            float s = .45f + (float)rng.NextDouble() * .85f;
            var p=Ground(origin+new Vector3(x,0,z)); p.Y+=s*.45f;
            grass.Items.Add(new ForestInstance(p, new(s, s, s), (float)rng.NextDouble() * Mathf.Tau));
        }
        chunk.Add(pines); chunk.Add(oaks); chunk.Add(bushes); chunk.Add(stones); chunk.Add(grass);
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 && home ? 7 : -11 + (float)rng.NextDouble() * 22;
            float z = i == 0 && home ? 8 : -12 + (float)rng.NextDouble() * 24;
            var p=Ground(origin+new Vector3(x,0,z)); p.Y+=.42f;
            chunk.Add(new CylinderDef { Position = p, Radius = .32f, Height = 4.2f, Rotation = new(0, (float)rng.NextDouble() * Mathf.Tau, Mathf.Pi / 2), Color = "49392b", HasCollision = true });
        }
    }

    Vector3 Ground(Vector3 point) { point.Y=TerrainHeight.Sample(point.X,point.Z,_seed); return point; }

    void BuildPoi(ChunkData chunk, Vector3 origin, Vector2I coord)
    {
        if (coord == Vector2I.Zero) chunk.Add(new PoiDef("res://Scenes/World/POI/ForestCabin.tscn", Ground(origin + new Vector3(-8, 0, -8)), .12f, "cabin", "cabin_meeting"));
        else if (coord == new Vector2I(1, 0)) chunk.Add(new PoiDef("res://Scenes/World/POI/ForestCamp.tscn", Ground(origin + new Vector3(8, 0, 3)), -.45f, "camp", "campfire_talk"));
        else if (coord == new Vector2I(0, 1)) chunk.Add(new PoiDef("res://Scenes/World/POI/ForestRuin.tscn", Ground(origin + new Vector3(-8, 0, 5)), .2f, "ruin", "ruin_discovery"));
        else if (coord == new Vector2I(-1, 0)) chunk.Add(new PoiDef("res://Scenes/World/POI/ForestShelter.tscn", Ground(origin + new Vector3(-7, 0, -2)), -.2f, "shelter", "storm_shelter"));
    }

    void AddActivityPoints(ChunkData chunk, Vector3 origin, Vector2I coord)
    {
        chunk.ActivityPoints.Add(new ActivityPointDef { Position = Ground(origin + new Vector3(0, 0, -5)), ActivityType = "Walking", LocationName = "Street" });
        chunk.ActivityPoints.Add(new ActivityPointDef { Position = Ground(origin + new Vector3(1.2f, 0, 6)), ActivityType = "Socialize", LocationName = "Park" });
        if (coord == Vector2I.Zero) chunk.ActivityPoints.Add(new ActivityPointDef { Position = Ground(origin + new Vector3(-5, 0, -3)), ActivityType = "Rest", LocationName = "Cafe" });
    }

    int DeterministicHash(int cx, int cz)
    {
        unchecked { long h = _seed ^ 0x811c9dc5L; h = (h * 16777619L) ^ cx; h = (h * 16777619L) ^ cz; return (int)(h & 0x7fffffff); }
    }
}

public sealed class ChunkData { public Vector2I Coord { get; set; } public List<object> Elements { get; } = new(); public List<ActivityPointDef> ActivityPoints { get; } = new(); public List<LightDef> Lights { get; } = new(); public void Add(object element) => Elements.Add(element); }
public sealed class BoxDef { public Vector3 Position { get; set; } public Vector3 Size { get; set; } public string Color { get; set; } = "808080"; public bool HasCollision { get; set; } = true; }
public sealed class SphereDef { public Vector3 Position { get; set; } public float Radius { get; set; } public float Height { get; set; } public string Color { get; set; } = "808080"; }
public sealed class CylinderDef { public Vector3 Position { get; set; } public Vector3 Rotation { get; set; } public float Radius { get; set; } public float Height { get; set; } public string Color { get; set; } = "808080"; public bool HasCollision { get; set; } }
public enum ForestKind { Trunk, Crown, Conifer, Bush, Stone, Grass, Leaf, BillboardPine, BillboardOak }
public readonly record struct ForestInstance(Vector3 Position, Vector3 Scale, float Yaw);
public sealed class ForestBatchDef { public ForestKind Kind { get; set; } public List<ForestInstance> Items { get; } = new(); public List<Vector3> CollisionBases { get; } = new(); }
public sealed record PoiDef(string ScenePath, Vector3 Position, float RotationY, string PoiId, string EventHook);
public sealed class TerrainDef { public Vector3 Origin { get; set; } public long Seed { get; set; } public int Resolution { get; set; }=16; }
public sealed class TrailDef { public Vector3 Origin { get; set; } public long Seed { get; set; } public float HalfWidth { get; set; }=1.25f; public int Segments { get; set; }=16; }
public sealed class LightDef { public Vector3 Position { get; set; } public Color Color { get; set; } public float Energy { get; set; } = 1; public float Range { get; set; } = 5; }
public sealed class ActivityPointDef { public Vector3 Position { get; set; } public string ActivityType { get; set; } = "Wait"; public float FacingAngle { get; set; } public string LocationName { get; set; } = "Street"; }

public static class TerrainHeight
{
    public static float Sample(float x,float z,long seed)
    {
        float phase=(seed&65535)/65535f*Mathf.Tau;
        float broad=Mathf.Sin(x*.055f+phase)*.82f+Mathf.Cos(z*.047f-phase*.7f)*.68f;
        float detail=Mathf.Sin((x+z)*.115f+phase*1.3f)*.24f+Mathf.Cos((x-z)*.083f)*.18f;
        return broad+detail;
    }
}
