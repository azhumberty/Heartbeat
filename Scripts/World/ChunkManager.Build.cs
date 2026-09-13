using Godot;
namespace Heartbeat;

public partial class ChunkManager
{
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

}
