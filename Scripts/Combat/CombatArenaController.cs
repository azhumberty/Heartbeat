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
    PanelContainer _detailPanel=null!;
    Button _play=null!,_end=null!,_return=null!,_flee=null!,_potion=null!;
    TextureRect _enemy=null!;
    int _selected=-1;
    Control _effects=null!;
    Control? _upgrade;

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
            Modulate = new Color(0.75f, 0.75f, 0.78f)
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.Texture = ChromaArt.LoadArt(ChromaArt.BackgroundForArena(Manager.State.Arena));
        AddChild(bg);
        if (Game.Settings.UseImageAi) _ = ApplyAiBackground(bg);

        // 2D Enemy Sprite with chroma-key (#00FF00) cutout
        _enemy = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(420, 560)
        };
        _enemy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (!string.IsNullOrWhiteSpace(Manager.State.OpponentCharacterId) && new CharacterRepository().Load(Manager.State.OpponentCharacterId) is { } opponent)
        {
            _enemy.Texture = new PortraitCache().Get(opponent, Game.CharacterStates.GetValueOrDefault(opponent.Id) ?? new CharacterState(), false);
        }
        else if (new ContentLibrary().Find(Manager.State.EnemyId) is {Category:"Inimigos"} custom && ContentLibrary.LoadTexture(custom.Path) is { } customTexture)
        {
            _enemy.Texture = customTexture;
            if(custom.Path.Contains("chroma",StringComparison.OrdinalIgnoreCase)||custom.Tags.Contains("chroma",StringComparison.OrdinalIgnoreCase))ChromaArt.ApplyChroma(_enemy);
        }
        else if (Game.ImagePaths.TryGetValue("enemy:"+Manager.State.EnemyId, out var enemyPath) && WorldArt.Load(enemyPath) is { } generatedFoe)
        {
            _enemy.Texture = generatedFoe;
        }
        else
        {
            _enemy.Texture = ChromaArt.LoadArt(ChromaArt.EnemySpritePath(Manager.State.EnemyId));
            ChromaArt.ApplyChroma(_enemy);
        }
        AddChild(_enemy);
    }

    async Task ApplyAiBackground(TextureRect bg)
    {
        try
        {
            var id = Manager.State.EncounterId.StartsWith("atlas_") ? Manager.State.EncounterId["atlas_".Length..] : Manager.State.EncounterId;
            var node = Game.AtlasNodes.FirstOrDefault(n => n.Id == id) ?? new AtlasNodeData { Id = id, Kind = AtlasNodeKind.Combat, Title = Manager.State.EnemyId };
            var ready = WorldArt.Background(Game, node);
            if (ready != null) { bg.Texture = ready; return; }
            var tex = await BackgroundGenerationService.FetchAsync(Game, node, null, CancellationToken.None);
            if (tex != null && GodotObject.IsInstanceValid(bg))
            {
                bg.Texture = tex;
                bg.Modulate = new Color(0.9f, 0.9f, 0.92f);
            }
        }
        catch (Exception e) { GD.PushWarning("[ImageAI] arena: " + e.GetType().Name); }
    }
    PanelContainer StatPanel(string title, out Label nameLabel, out Label hpLabel, out ProgressBar hpBar, out Label manaLabel, out ProgressBar manaBar, bool withMana)
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
        else { manaLabel=null!; manaBar=null!; }
        return panel;
    }
}
