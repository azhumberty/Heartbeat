using Godot;
namespace Heartbeat;

public sealed class EncounterDefinition
{
    public string Id {get;set;}="";
    public string Biome {get;set;}="Forest";
    public string Period {get;set;}="Any";
    public float Chance {get;set;}=1;
    public float DetectionRadius {get;set;}=5;
    public string EnemyId {get;set;}="forest";
    public string Arena {get;set;}="Forest";
    public string Trigger {get;set;}="Território";
    public int Reward {get;set;}=24;
    public double Cooldown {get;set;}=360;
    public string RequiredFlag {get;set;}="";
    public bool Repeat {get;set;}=true;
}
public partial class EncounterManager : Node3D
{
    public GameSave Game {get;set;}=new();
    public ChunkManager Chunks {get;set;}=null!;
    public PlayerController Player {get;set;}=null!;
    public Func<bool> CanEngage {get;set;}=()=>true;
    public Action<EnemyActor>? Engaged;
    public bool Suspended {get;set;}
    public IReadOnlyCollection<EnemyActor> Active=>_actors.Values;
    readonly Dictionary<string,EnemyActor> _actors=new();
    double _timer;
    public double Now=>(Game.Day-1)*1440d+Game.WorldMinutes;
    public static EncounterDefinition Define(Vector2I coord)
    {
        if(coord==new Vector2I(0,1))return new(){Id=$"{coord.X}:{coord.Y}",Biome="Ruin",EnemyId="ruin",Arena="Ruin",Trigger="Guardião da ruína",Repeat=false,Reward=48};
        if(coord==new Vector2I(1,0))return new(){Id=$"{coord.X}:{coord.Y}",Biome="Camp",EnemyId="camp",Arena="Camp",Trigger="Acampamento hostil",Reward=30};
        if((Math.Abs(coord.X)+Math.Abs(coord.Y))%3==2)return new(){Id=$"{coord.X}:{coord.Y}",Period="Night",EnemyId="night",Arena="Night",Trigger="Caçada noturna",DetectionRadius=7,Reward=38};
        return new(){Id=$"{coord.X}:{coord.Y}",Trigger=coord.X%2==0?"Território marcado":"Guardando um cristal"};
    }
    public bool Eligible(EncounterDefinition def)
    {
        if(def.Period=="Night"&&Game.WorldMinutes>=6*60&&Game.WorldMinutes<19*60)return false;
        if(def.RequiredFlag.Length>0&&!Game.Flags.Contains(def.RequiredFlag))return false;
        var progress=Game.Encounters.GetValueOrDefault(def.Id);
        return progress==null||(!progress.Resolved&&progress.AvailableAt<=Now);
    }
    public override void _Process(double delta)
    {
        if(Suspended)return;
        foreach(var actor in _actors.Values)actor.Hunting=CanEngage();
        _timer+=delta;if(_timer<.6)return;_timer=0;
        foreach(var pair in _actors.ToArray())
        {
            if(pair.Value.GlobalPosition.DistanceTo(Player.GlobalPosition)>70||!Eligible(pair.Value.Definition)){pair.Value.QueueFree();_actors.Remove(pair.Key);}
        }
        var center=ChunkManager.WorldToChunk(Player.Position);
        var coords=new List<Vector2I>{center,center+Vector2I.Up,center+Vector2I.Right,center+Vector2I.Down,center+Vector2I.Left};
        foreach(var coord in coords)
        {
            if(_actors.Count>=4)break;var def=Define(coord);
            if(_actors.ContainsKey(def.Id)||!Chunks.IsChunkLoaded(coord)||!Eligible(def))continue;
            var pos=new Vector3(coord.X*32+2.5f,0,coord.Y*32-10);
            pos=Chunks.ResolveValidPosition(pos);
            if(pos.DistanceTo(Player.Position)<9)continue;
            // Reject occupied locations instead of spawning inside a trunk or NPC.
            var shape=new SphereShape3D{Radius=.8f};
            var query=new PhysicsShapeQueryParameters3D {Shape=shape,Transform=new Transform3D(Basis.Identity,pos+Vector3.Up),CollisionMask=1};
            if(GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count>0)continue;
            var actor=new EnemyActor {Definition=def,Position=pos,Player=Player,Seed=Game.WorldSeed};
            actor.Contact=()=>{if(!Suspended&&CanEngage()&&Eligible(def))Engaged?.Invoke(actor);};
            AddChild(actor);_actors[def.Id]=actor;
        }
    }
    public EnemyActor? Nearest()
    {
        return _actors.Values.Where(a=>a.GlobalPosition.DistanceTo(Player.GlobalPosition)<4).OrderBy(a=>a.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)).FirstOrDefault();
    }
    public void RemoveEncounter(string id){if(_actors.Remove(id,out var actor))actor.QueueFree();}
}
public partial class EnemyActor : CharacterBody3D
{
    public EncounterDefinition Definition {get;set;}=new();
    public PlayerController Player {get;set;}=null!;
    public long Seed;
    public bool Hunting;
    public Action? Contact;
    Node3D _visual=null!;
    Vector3 _home;
    float _pulse,_alert;
    public override void _Ready()
    {
        _home=Position;
        AddChild(new CollisionShape3D {Position=new(0,.75f,0),Shape=new CapsuleShape3D{Radius=.45f,Height=1.5f}});
        _visual=EnemyVisual.Create(Definition.EnemyId);AddChild(_visual);
        AddChild(new Label3D {Text=EnemyDefinition.Get(Definition.EnemyId).Name+"\n"+Definition.Trigger,Position=new(0,2.5f,0),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=.004f,Modulate=new Color("e5b77a"),NoDepthTest=false});
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!Hunting){Velocity=Vector3.Zero;return;}
        float d=(float)delta;_pulse+=d;
        _visual.Position=new(0,Mathf.Sin(_pulse*2)*.035f,0);
        float distance=Position.DistanceTo(Player.Position);
        bool sees=false;
        if(distance<Definition.DetectionRadius)
        {
            var q=PhysicsRayQueryParameters3D.Create(GlobalPosition+Vector3.Up,Player.GlobalPosition+Vector3.Up);
            q.Exclude=new Godot.Collections.Array<Rid>{GetRid()};
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(q);
            sees=hit.Count==0||hit["collider"].AsGodotObject()==Player;
        }
        _alert=sees?Math.Min(1,_alert+d):Math.Max(0,_alert-d*.5f);
        var target=_alert>.2f?Player.Position:_home+new Vector3(Mathf.Sin(_pulse*.3f)*1.2f,0,0);
        var direction=target-Position;direction.Y=0;
        if(direction.Length()>.4f){direction=direction.Normalized();_visual.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);}else direction=Vector3.Zero;
        Velocity=new(direction.X*(_alert>.2?1.8f:.45f),IsOnFloor()?-.1f:Velocity.Y-18*d,direction.Z*(_alert>.2?1.8f:.45f));MoveAndSlide();
        if(distance<2.1f&&_alert>.5f)Contact?.Invoke();
    }
}
public static class EnemyVisual
{
    public static Node3D Create(string id)
    {
        var root = new Node3D();
        
        string artkitId = id switch {
            "forest" => "horned",
            "camp" => "horned",
            "ruin" => "stone",
            "night" => "wraith",
            _ => id
        };

        // Define paths to check, including ArtKit generated assets
        string[] paths = {
            $"user://Enemies/{id}.png",
            $"user://Enemies/{id}.jpg",
            $"res://Assets/ArtKit/Characters/Monsters/humanoid_{artkitId}_combat.png",
            $"res://Assets/ArtKit/Characters/Monsters/humanoid_{artkitId}_idle.png",
            $"res://Assets/ArtKit/Characters/Monsters/{artkitId}.png",
            $"res://Assets/ArtKit/Characters/NPCs/{id}_sunga_fullbody.png",
            $"res://Assets/ArtKit/Characters/NPCs/{id}_portrait.png",
            $"res://Enemies/{id}.png"
        };

        foreach (var path in paths)
        {
            var globalPath = ProjectSettings.GlobalizePath(path);
            Texture2D? tex = null;
            if (ResourceLoader.Exists(path))
            {
                // Try ResourceLoader first
                tex = GD.Load<Texture2D>(path);
            }
            
            if (tex == null && System.IO.File.Exists(globalPath))
            {
                var bytes = System.IO.File.ReadAllBytes(globalPath);
                var img = new Godot.Image();
                if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                {
                    if (img.LoadJpgFromBuffer(bytes) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
                }
                else
                {
                    if (img.LoadPngFromBuffer(bytes) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
                }
            }

            if (tex != null)
            {
                // Calculate PixelSize to make the sprite roughly 2.8 meters tall
                float targetHeight = 2.8f;
                float pixelSize = targetHeight / Math.Max(1, tex.GetHeight());

                var sprite = new Sprite3D {
                    Name = "CustomSprite",
                    Texture = tex,
                    PixelSize = pixelSize,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
                    AlphaScissorThreshold = 0.1f,
                    Position = new Vector3(0, targetHeight / 2f, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On
                };
                
                if (ResourceLoader.Exists("res://Assets/ArtKit/Billboards/checker_mask.gdshader"))
                {
                    var shader = GD.Load<Shader>("res://Assets/ArtKit/Billboards/checker_mask.gdshader");
                    var mat = new ShaderMaterial { Shader = shader };
                    mat.SetShaderParameter("tex", tex);
                    sprite.MaterialOverride = mat;
                }
                
                root.AddChild(sprite);
                
                // Add breathing animation (Tween scaling Y)
                if (root.IsInsideTree()) StartBreathing(sprite);
                else sprite.Ready += () => StartBreathing(sprite);

                return root;
            }
        }

        
        var color=new Color(EnemyDefinition.Get(id).Color);
        void Part(Mesh mesh,Vector3 pos,Vector3 scale,Color tint)
        {root.AddChild(new MeshInstance3D {Mesh=mesh,Position=pos,Scale=scale,MaterialOverride=new StandardMaterial3D{AlbedoColor=tint,Roughness=.85f}});}
        Part(new SphereMesh {Radius=.5f,Height=1,RadialSegments=12,Rings=6},new(0,1.1f,0),new(.95f,1.5f,.65f),color);
        Part(new SphereMesh {Radius=.35f,Height=.7f,RadialSegments=10,Rings=5},new(0,1.9f,.08f),new(1,1,1),color.Lightened(.1f));
        foreach(int s in new[]{-1,1})
        {
            Part(new CylinderMesh{TopRadius=.08f,BottomRadius=.13f,Height=.85f,RadialSegments=6},new(s*.27f,.43f,0),Vector3.One,color.Darkened(.25f));
            Part(new CylinderMesh{TopRadius=.07f,BottomRadius=.12f,Height=.9f,RadialSegments=6},new(s*.56f,1.1f,0),Vector3.One,color);
            Part(new CylinderMesh{TopRadius=0,BottomRadius=.12f,Height=.7f,RadialSegments=5},new(s*.23f,2.25f,0),new(1,1,1),new Color("c6bda5"));
            var eye=new MeshInstance3D{Position=new(s*.13f,1.97f,.35f),Mesh=new SphereMesh{Radius=.045f,Height=.09f},MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color("ffa15f"),EmissionEnabled=true,Emission=new Color("ff693b"),EmissionEnergyMultiplier=2}};root.AddChild(eye);
        }
        if(id=="night")root.Scale=new(1.1f,1.15f,1.1f);
        if(id=="ruin")root.Scale=new(1.3f,1.3f,1.3f);
        return root;
    }

    static void StartBreathing(Sprite3D sprite)
    {
        var tween = sprite.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(sprite, "scale", new Vector3(1.02f, 0.96f, 1), 1.5f);
        tween.TweenProperty(sprite, "scale", new Vector3(0.98f, 1.03f, 1), 1.5f);
    }
}
