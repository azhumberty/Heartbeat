using Godot;

namespace Heartbeat;

public partial class MainMenuController : Node
{
    readonly SaveManager _saves = new(); readonly CharacterRepository _characters = new();
    Control _root=null!; Control? _screen; GameSave _menuSave=new();
    public override void _Ready()
    {
        var root = new ColorRect { Color = new Color("10121d") }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        _root=root; _menuSave=_saves.Load()??new();
        BuildBackdrop(root);
        var panel = new VBoxContainer { Position = new Vector2(120, 48), Size = new Vector2(430, 630) }; panel.AddThemeConstantOverride("separation", 7); root.AddChild(panel);
        panel.AddChild(Label("HEARTBEAT", 48, new Color("e6c593"))); panel.AddChild(Label("vínculos, mistérios e cartas na floresta", 17, new Color("aaa9c8")));
        panel.AddChild(Button("Continuar", Continue)); panel.AddChild(Button("Novo jogo", NewGame)); panel.AddChild(Button("Personagens", Characters)); panel.AddChild(Button("Galeria", () => { if(_screen==null)_screen=AuxiliaryScreens.Gallery(_root,_menuSave,Close); })); panel.AddChild(Button("Configurações", () => { if(_screen==null)_screen=AuxiliaryScreens.Settings(_root,_menuSave.Settings,()=>{_saves.Save(_menuSave);Close();}); })); panel.AddChild(Button("Sair", () => GetTree().Quit()));
        var deckButton=Button("Baralho",OpenDeck);panel.AddChild(deckButton);panel.MoveChild(deckButton,5);
        var cardsButton=Button("Criar cartas",OpenCards);panel.AddChild(cardsButton);panel.MoveChild(cardsButton,6);
        root.Modulate = new Color(1,1,1,0); root.CreateTween().TweenProperty(root,"modulate:a",1f,.3).SetTrans(Tween.TransitionType.Quad);
    }
    void BuildBackdrop(Control root)
    {
        var glow = new ColorRect { Color = new Color("7f385044"), Position = new(700, 140), Size = new(390, 390), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(glow); glow.Rotation = .78f;
        var random = new Random(42);
        for (int i = 0; i < 18; i++)
        {
            var width = 36 + random.Next(0, 54); var height = 90 + random.Next(0, 260);
            var building = new ColorRect { Color = new Color(i % 3 == 0 ? "171e31" : "141a29"), Position = new(i * 76f - 20, 720 - height), Size = new(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
            root.AddChild(building);
            for (int row = 0; row < Math.Min(5, height / 45); row++)
                if ((i + row) % 3 == 0) building.AddChild(new ColorRect { Color = new Color("d9877255"), Position = new(10, 18 + row * 38), Size = new(Math.Max(8, width - 20), 4), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        var line = new ColorRect { Color = new Color("ff739f88"), Position = new(690, 530), Size = new(470, 2), MouseFilter = Control.MouseFilterEnum.Ignore }; root.AddChild(line);
    }
    Label Label(string text, int size, Color color) { var l = new Label { Text = text }; l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color); return l; }
    Button Button(string text, Action action) { var b = Ui.Button(text, action); b.CustomMinimumSize = new Vector2(340, 46); b.AddThemeFontSizeOverride("font_size", 18); return b; }
    void Continue() { if (_saves.Load() != null) GetTree().ChangeSceneToFile("res://Scenes/World/World.tscn"); else NewGame(); }
    void NewGame()
    {
        var cast = _characters.List().ToList(); if (cast.Count == 0) cast.Add(_characters.LoadDemo());
        var save = new GameSave { CharacterIds = cast.Select(c => c.Id).ToList(), CharacterStates = cast.ToDictionary(c => c.Id, _ => new CharacterState { CurrentLocation = "Cafe" }) };
        save.Settings=_menuSave.Settings; save.ActionsLeft=6;
        _saves.Save(save); GetTree().ChangeSceneToFile("res://Scenes/World/World.tscn");
    }
    void Characters() { if(_screen!=null)return; var creator=new CharacterCreatorController { Closed=Close }; _screen=creator; _root.AddChild(creator); }
    void OpenDeck()
    {
        if(_screen!=null)return;
        if(_menuSave.ActiveCombat!=null){Notice("Termine a batalha antes de editar o baralho.");return;}
        var editor=new DeckEditor {Game=_menuSave,Closed=Close,Saved=()=>_saves.Save(_menuSave)};_screen=editor;_root.AddChild(editor);
    }
    void OpenCards(){if(_screen!=null)return;var editor=new CardEditor {Closed=Close};_screen=editor;_root.AddChild(editor);}
    void Close() { _screen?.QueueFree(); _screen=null; }
    public override void _UnhandledInput(InputEvent e) { if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape)Close(); }
    void Notice(string message) { GD.Print("[Menu] " + message); }
}
