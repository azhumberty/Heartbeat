using Godot;
namespace Heartbeat;

public partial class ChunkManager
{
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

}
