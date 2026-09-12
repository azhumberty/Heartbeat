using Godot;
namespace Heartbeat;

public partial class CombatArenaController : Control
{

    public CombatManager Manager {get;set;}=null!;
    public GameSave Game {get;set;}=null!;
    public Action? Changed;
    public Action? Finished;
    public bool Busy {get;private set;}
    public bool ReduceMotion {get;set;}
    HBoxContainer _hand=null!;
    Label _playerName=null!,_enemyName=null!,_playerHpText=null!,_enemyHpText=null!,_playerManaText=null!,_message=null!,_turn=null!;
    ProgressBar _playerHealth=null!,_enemyHealth=null!,_playerMana=null!;
    HBoxContainer _intentRow=null!;
    VBoxContainer _detail=null!;
    Button _play=null!,_end=null!,_return=null!,_flee=null!;
    Node3D _enemy=null!;
    Camera3D _camera=null!;
    SubViewport _viewport=null!;
    int _selected=-1;
    Control _effects=null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildArena();BuildInterface();Refresh();
    }
    void BuildArena()
    {
        var container=new SubViewportContainer {Stretch=true,MouseFilter=MouseFilterEnum.Ignore};AddChild(container);container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _viewport=new SubViewport {OwnWorld3D=true,Size=new(1280,720),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};container.AddChild(_viewport);
        var stage=new Node3D();_viewport.AddChild(stage);
        bool night=Manager.State.Hour<6||Manager.State.Hour>=18;string arena=Manager.State.Arena;
        var env=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color(night?"0c1820":"455954"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("9eb1ae"),AmbientLightEnergy=night?.65f:.85f,FogEnabled=true,FogDensity=.022f,FogLightColor=new Color("384a42"),TonemapMode=Godot.Environment.ToneMapper.Filmic};
        stage.AddChild(new WorldEnvironment {Environment=env});
        stage.AddChild(new DirectionalLight3D {RotationDegrees=new(-38,-25,0),LightColor=new Color("f9dbad"),LightEnergy=night?.6f:1.2f,ShadowEnabled=true,DirectionalShadowMaxDistance=24});
        void Mesh(Mesh mesh,Vector3 pos,Material mat){stage.AddChild(new MeshInstance3D {Mesh=mesh,Position=pos,MaterialOverride=mat});}
        Mesh(new PlaneMesh {Size=new(45,45)},Vector3.Zero,SurfaceMaterials.ForestGround());
        // Billboard trees around the arena
        var rng=new Random(42);
        for(int i=0;i<14;i++)
        {
            float x=(i%2==0?-1:1)*(5+rng.Next(8));float z=-9+rng.Next(13);
            float h=6f+rng.Next(4);
            string tex=i%3==0?"tree_pine":"tree_oak_dead";
            stage.AddChild(BillboardSprites.Create(tex,new(x,h*.5f,z),new(h*.55f,h)));
        }
        for(int i=0;i<9;i++)Mesh(new SphereMesh {Radius=.45f,Height=.5f,RadialSegments=8,Rings=4},new(-5+i*1.3f,.15f,-4),SurfaceMaterials.MossyRock());
        if(arena=="Ruin") stage.AddChild(BillboardSprites.Create("ruins",new(0,2.5f,-6),new(7,5)));
        if(arena=="Camp")
        {
            stage.AddChild(BillboardSprites.Create("campfire",new(-3,1.4f,-1),new(3,2.8f)));
            stage.AddChild(new OmniLight3D {Position=new(-3,1,-1),LightColor=new Color("ffae5a"),LightEnergy=2.5f,OmniRange=9});
        }
        _enemy=EnemyVisual.Create(Manager.State.EnemyId);_enemy.Position=new(0,0,-1);stage.AddChild(_enemy);
        stage.AddChild(new OmniLight3D {Position=new(0,3,2),LightColor=new Color("a7d3db"),LightEnergy=2,OmniRange=8});
        _camera=new Camera3D {Position=new(0,3.5f,9),Fov=54,Current=true};stage.AddChild(_camera);_camera.LookAt(new Vector3(0,1.15f,-1));
    }

    PanelContainer StatPanel(string title, out Label nameLabel, out Label hpLabel, out ProgressBar hpBar, out Label? manaLabel, out ProgressBar? manaBar, bool withMana)
    {
        var panel=new PanelContainer {CustomMinimumSize=new(300,0)};
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor=new Color("0a0e12cc"), BorderWidthLeft=1, BorderWidthTop=1, BorderWidthRight=1, BorderWidthBottom=1,
            BorderColor=new Color("b39a6488"), ContentMarginLeft=14, ContentMarginRight=14, ContentMarginTop=10, ContentMarginBottom=12,
            CornerRadiusTopLeft=12, CornerRadiusTopRight=12, CornerRadiusBottomLeft=12, CornerRadiusBottomRight=12,
            ShadowColor=new Color(0,0,0,.45f), ShadowSize=8
        });
        var col=new VBoxContainer(); panel.AddChild(col);
        nameLabel=Ui.Text(title,18); nameLabel.AutowrapMode=TextServer.AutowrapMode.Off; col.AddChild(nameLabel);
        hpLabel=Ui.Text("Vida",13); hpLabel.AutowrapMode=TextServer.AutowrapMode.Off; col.AddChild(hpLabel);
        hpBar=Bar(col,new Color("b14743"),new Color("e67373"));
        if (withMana)
        {
            manaLabel=Ui.Text("Mana",13); manaLabel.AutowrapMode=TextServer.AutowrapMode.Off; col.AddChild(manaLabel);
            manaBar=Bar(col,new Color("3d6ebd"),new Color("7eb6ff"));
        }
        else { manaLabel=null; manaBar=null; }
        return panel;
    }

}
