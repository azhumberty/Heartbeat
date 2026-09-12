using Godot;
namespace Heartbeat;

public partial class ChunkManager
{
    static void InstantiateForestBatch(Node3D parent, ForestBatchDef batch)
    {
        if (batch.Items.Count == 0) return;

        // 2.5D billboard trees — shared textures, Sprite3D BillboardMode.Enabled
        if (batch.Kind is ForestKind.BillboardPine or ForestKind.BillboardOak)
        {
            string tex = batch.Kind == ForestKind.BillboardPine ? "tree_pine" : "tree_oak_dead";
            var holder = new Node3D { Name = "Forest" + batch.Kind };
            parent.AddChild(holder);
            foreach (var item in batch.Items)
            {
                float height = Math.Max(2.5f, item.Scale.Y);
                var sprite = BillboardSprites.Create(tex, item.Position, new Vector2(height * 0.7f, height), item.Yaw);
                sprite.VisibilityRangeEnd = 88;
                holder.AddChild(sprite);
            }
            foreach (var basePos in batch.CollisionBases)
            {
                var body = new StaticBody3D { Position = basePos + new Vector3(0, 1.5f, 0) };
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .48f, Height = 3 } });
                parent.AddChild(body);
            }
            return;
        }

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

}
