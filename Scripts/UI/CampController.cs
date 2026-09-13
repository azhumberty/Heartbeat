using Godot;

namespace Heartbeat;

/// <summary>Persistent camp hub. Relationship characters can live here and be visited from the Atlas at any time.</summary>
public partial class CampController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action? Closed;
    public Action<string>? TalkRequested;
    public Action? Rested;
    VBoxContainer _list = null!;
    Label _status = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.BottomWide);
        panel.OffsetLeft = 54;
        panel.OffsetRight = -54;
        panel.OffsetTop = -320;
        panel.OffsetBottom = -24;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("081019ee"),
            BorderColor = new Color("b9985d99"),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        });
        AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);
        body.AddChild(Ui.Text("ACAMPAMENTO", 26));
        body.AddChild(Ui.Text("Quem tem afeição suficiente pode morar aqui. Conversas no acampamento não fecham o caminho da expedição.", 15));
        _status = Ui.Text("", 14);
        _status.AddThemeColorOverride("font_color", new Color("c9b27a"));
        body.AddChild(_status);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 110) };
        scroll.AddChild(_list);
        body.AddChild(scroll);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        body.AddChild(row);
        row.AddChild(Ui.Button("Descansar", Rest));
        row.AddChild(Ui.Button("Voltar ao atlas", () => Closed?.Invoke()));
        Refresh();
    }

    public void Refresh()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();
        var repo = new CharacterRepository();
        foreach (var id in Game.CampResidents.ToList())
        {
            var data = repo.Load(id);
            var name = data?.Name ?? id;
            var captured = id;
            var button = Ui.Button($"Falar com {name}", () => TalkRequested?.Invoke(captured));
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _list.AddChild(button);
        }
        foreach (var id in Game.CharacterIds)
        {
            if (Game.CampResidents.Contains(id)) continue;
            var data = repo.Load(id);
            if (data == null || !data.CanBuildRelationship) continue;
            if (!Game.CharacterStates.TryGetValue(id, out var state) || state.Affection < 40) continue;
            var captured = id;
            var button = Ui.Button($"Convidar {data.Name} a morar aqui", () => Invite(captured, data));
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _list.AddChild(button);
        }
        if (_list.GetChildCount() == 0)
            _list.AddChild(Ui.Text("Ninguém mora aqui ainda. Aumente a afeição com um personagem de vínculo.", 15));
    }

    void Invite(string id, CharacterData data)
    {
        if(!Game.CharacterStates.TryGetValue(id,out var state)||!CampService.Invite(Game,data,state))return;
        _status.Text = $"{data.Name} aceitou um lugar junto à fogueira.";
        Rested?.Invoke();
        Refresh();
    }

    void Rest()
    {
        Game.Player.Health = Game.Player.MaxHealth;
        Game.Player.Mana = Game.Player.MaxMana;
        Game.Player.Energy = 100;
        _status.Text = "O fogo devolveu Vida, Mana e energia.";
        Rested?.Invoke();
    }
}
