using Godot;
namespace Heartbeat;

/// <summary>Reusable, complete forest locations. The chunk system only places these scenes.</summary>
public partial class ForestPoi : Node3D
{
    [Export] public string Kind { get; set; } = "Cabin";

    public override void _Ready()
    {
        if (GetChildCount() > 0) return;
        switch (Kind) { case "Camp": BuildCamp(); break; case "Ruin": BuildRuin(); break; case "Shelter": BuildShelter(); break; default: BuildCabin(); break; }
    }

    void BuildCabin()
    {
        Box("Floor", new(0, .18f, 0), new(7, .36f, 6), "49392b", true);
        Box("BackWall", new(0, 1.75f, -2.85f), new(7, 3.5f, .3f), "72543b", true);
        Box("LeftWall", new(-3.35f, 1.75f, 0), new(.3f, 3.5f, 6), "72543b", true);
        Box("RightWall", new(3.35f, 1.75f, 0), new(.3f, 3.5f, 6), "72543b", true);
        Box("FrontLeft", new(-2.25f, 1.75f, 2.85f), new(2.5f, 3.5f, .3f), "72543b", true);
        Box("FrontRight", new(2.25f, 1.75f, 2.85f), new(2.5f, 3.5f, .3f), "72543b", true);
        Box("DoorHeader", new(0, 3.05f, 2.85f), new(2, .9f, .3f), "72543b", true);
        Box("WindowLeft", new(-2.25f, 1.9f, 3.03f), new(1.25f, 1.15f, .06f), "79949a", false);
        Box("WindowRight", new(2.25f, 1.9f, 3.03f), new(1.25f, 1.15f, .06f), "79949a", false);
        Box("WindowBarL", new(-2.25f, 1.9f, 3.08f), new(.07f, 1.2f, .05f), "49392b", false);
        Box("WindowBarR", new(2.25f, 1.9f, 3.08f), new(.07f, 1.2f, .05f), "49392b", false);
        Box("RoofL", new(-1.75f, 3.95f, 0), new(4.2f, .3f, 6.8f), "384135", true, new(0, 0, -.42f));
        Box("RoofR", new(1.75f, 3.95f, 0), new(4.2f, .3f, 6.8f), "384135", true, new(0, 0, .42f));
        Box("Chimney", new(2, 4.55f, -1.25f), new(.65f, 1.8f, .65f), "59625a", true);
        Box("Porch", new(0, .14f, 3.65f), new(4.2f, .28f, 1.5f), "4f3d2e", true);
        Box("TableTop", new(1.25f, 1, -.8f), new(1.6f, .12f, 1), "8a6848", true);
        Box("TableLeg", new(1.25f, .55f, -.8f), new(.16f, .9f, .16f), "49392b", true);
        Box("Bed", new(-1.75f, .48f, -1.4f), new(2.4f, .55f, 1.2f), "66736a", true);
        WarmLight(new(0, 2.65f, 0), 2.6f, 8);
    }

    void BuildCamp()
    {
        // Two tents, a log seat and a lit fire define a complete outdoor camp.
        Box("TentLeft", new(-2.6f, 1.05f, -.8f), new(2.8f, .12f, 3.6f), "6b704c", true, new(0, 0, .62f));
        Box("TentRight", new(-.9f, 1.05f, -.8f), new(2.8f, .12f, 3.6f), "6b704c", true, new(0, 0, -.62f));
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.Tau;
            Sphere("FireStone", new(Mathf.Cos(a) * .8f, .18f, Mathf.Sin(a) * .8f + 1.1f), .28f, "62645f");
        }
        Cylinder("FireLogA", new(0, .25f, 1.1f), .12f, 1.15f, "49392b", new(Mathf.Pi / 2, 0, .55f));
        Cylinder("FireLogB", new(0, .27f, 1.1f), .12f, 1.15f, "49392b", new(Mathf.Pi / 2, 0, -.55f));
        Sphere("FireGlow", new(0, .55f, 1.1f), .38f, "e07a34", false, true);
        Cylinder("Seat", new(2.2f, .38f, 1.2f), .3f, 3.2f, "49392b", new(0, 0, Mathf.Pi / 2));
        WarmLight(new(0, 1.25f, 1.1f), 3.2f, 9);
    }

    void BuildRuin()
    {
        Box("StoneFloor", new(0, .12f, 0), new(6.5f, .24f, 6), "59625a", true);
        Box("Back", new(0, 1.6f, -2.85f), new(6.5f, 3.2f, .45f), "59625a", true);
        Box("Left", new(-3f, 1.15f, 0), new(.45f, 2.3f, 6), "59625a", true);
        Box("RightRear", new(3f, 1.5f, -1.45f), new(.45f, 3f, 2.8f), "59625a", true);
        Box("BrokenWall", new(1.9f, .75f, 2.8f), new(2.5f, 1.5f, .45f), "59625a", true);
        for (int i = 0; i < 7; i++) Sphere("Rubble", new(-2.4f + i * .8f, .18f, 2.3f + (i % 2) * .35f), .3f + (i % 3) * .08f, "59625a");
    }

    void BuildShelter()
    {
        Box("Floor", new(0, .15f, 0), new(5.4f, .3f, 4.3f), "49392b", true);
        Box("Back", new(0, 1.6f, -2f), new(5.4f, 3.2f, .28f), "5e4633", true);
        Box("Roof", new(0, 3.15f, 0), new(5.8f, .25f, 4.8f), "384135", true, new(.16f, 0, 0));
        foreach (float x in new[] { -2.4f, 2.4f }) Cylinder("Post", new(x, 1.55f, 1.8f), .15f, 3.1f, "49392b");
        Box("Bench", new(0, .6f, -1.25f), new(3.6f, .22f, .8f), "795a3e", true);
        WarmLight(new(0, 2.5f, -1.2f), 1.8f, 6);
    }

    void WarmLight(Vector3 position, float energy, float range)
    {
        var light = new OmniLight3D { Position = position, LightColor = new Color("ffb46e"), LightEnergy = energy, OmniRange = range, ShadowEnabled = true };
        light.AddToGroup("artificial_lights"); AddChild(light);
    }

    void Box(string name, Vector3 pos, Vector3 size, string color, bool collision, Vector3? rotation = null)
    {
        var r = rotation ?? Vector3.Zero;
        AddChild(new MeshInstance3D { Name = name, Position = pos, Rotation = r, Mesh = new BoxMesh { Size = size }, MaterialOverride = SurfaceMaterials.For(color) });
        if (!collision) return;
        var body = new StaticBody3D { Position = pos, Rotation = r }; body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } }); AddChild(body);
    }
    void Sphere(string name, Vector3 pos, float radius, string color, bool collision = false, bool emission = false)
    {
        Material material = emission ? new StandardMaterial3D { AlbedoColor = new Color(color), EmissionEnabled = true, Emission = new Color(color), EmissionEnergyMultiplier = 2 } : SurfaceMaterials.For(color);
        AddChild(new MeshInstance3D { Name = name, Position = pos, Mesh = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 10, Rings = 6 }, MaterialOverride = material });
        if (collision) { var body = new StaticBody3D { Position = pos }; body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = radius } }); AddChild(body); }
    }
    void Cylinder(string name, Vector3 pos, float radius, float height, string color, Vector3? rotation = null)
    {
        var r = rotation ?? Vector3.Zero;
        AddChild(new MeshInstance3D { Name = name, Position = pos, Rotation = r, Mesh = new CylinderMesh { TopRadius = radius * .85f, BottomRadius = radius, Height = height, RadialSegments = 10 }, MaterialOverride = SurfaceMaterials.For(color) });
        var body = new StaticBody3D { Position = pos, Rotation = r }; body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } }); AddChild(body);
    }
}
