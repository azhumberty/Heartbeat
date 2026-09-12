using Godot;
namespace Heartbeat;

/// <summary>Reusable forest POIs as 2.5D billboards plus simple collision pads.</summary>
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
        BillboardSprites.Attach(this, "cabin", new Vector3(0, 3.2f, 0), new Vector2(9f, 6.5f));
        Box("DoorBlock", new(0, 1.2f, 2.6f), new(1.6f, 2.4f, .2f), "49392b", true);
        WarmLight(new(0, 2.65f, 0), 2.6f, 8);
    }

    void BuildCamp()
    {
        Box("Pad", new(0, .08f, .6f), new(5.5f, .16f, 5f), "4f3d2e", true);
        BillboardSprites.Attach(this, "campfire", new Vector3(0, 1.6f, 1.0f), new Vector2(3.2f, 3.0f));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.Tau;
            Box($"FireStone{i}", new(Mathf.Cos(a) * .8f, .12f, Mathf.Sin(a) * .8f + 1.1f), new(.35f, .24f, .35f), "62645f", true);
        }
        WarmLight(new(0, 1.25f, 1.1f), 3.2f, 9);
    }

    void BuildRuin()
    {
        Box("StoneFloor", new(0, .12f, 0), new(6.5f, .24f, 6), "59625a", true);
        BillboardSprites.Attach(this, "ruins", new Vector3(0, 2.8f, -0.5f), new Vector2(8f, 5.5f));
        Box("RubblePad", new(0, .4f, 2.2f), new(4f, .5f, 1.2f), "59625a", true);
    }

    void BuildShelter()
    {
        Box("Floor", new(0, .15f, 0), new(5.4f, .3f, 4.3f), "49392b", true);
        BillboardSprites.Attach(this, "cabin", new Vector3(0, 2.6f, 0), new Vector2(6.5f, 4.8f));
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
}
