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
    TextureRect _enemy=null!;
    int _selected=-1;
    Control _effects=null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildArena();BuildInterface();Refresh();
    }

    void BuildArena()
    {
        // 2D Visual Novel Style Background
        var bg = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.6f, 0.6f, 0.6f) // Darken for UI contrast
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        string arena = Manager.State.Arena.ToLowerInvariant();
        string bgPath = "res://Assets/ArtKit/Interiors/" + arena + ".png";
        if (ResourceLoader.Exists(bgPath)) bg.Texture = GD.Load<Texture2D>(bgPath);
        AddChild(bg);

        // 2D Enemy Sprite
        _enemy = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _enemy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        // Mock Enemy visual load (The next agent will implement EnemyVisual for 2D)
        string texName = Manager.State.EnemyId;
        string artKitPng = "res://Assets/ArtKit/Characters/Monsters/humanoid_" + texName + "_combat.png";
        if (ResourceLoader.Exists(artKitPng)) {
            _enemy.Texture = GD.Load<Texture2D>(artKitPng);
            
            // Mask out the checkerboard if it exists
            if (ResourceLoader.Exists("res://Assets/ArtKit/Billboards/checker_mask.gdshader"))
            {
                var shader = GD.Load<Shader>("res://Assets/ArtKit/Billboards/checker_mask.gdshader");
                var mat = new ShaderMaterial { Shader = shader };
                mat.SetShaderParameter("tex", _enemy.Texture);
                _enemy.Material = mat;
            }
        }
        
        AddChild(_enemy);
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
