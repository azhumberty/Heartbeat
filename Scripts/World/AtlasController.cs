using Godot;
using System;
using System.Linq;

namespace Heartbeat;

/// <summary>2D expedition map in the Path of Exile / Slay the Spire style. Progress lives in GameSave.</summary>
public partial class AtlasController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action<string>? NodeSelected;
    public Action? CampRequested;
    Label _detailTitle = null!, _detailText = null!, _hud = null!;
    Button _travel = null!;
    AtlasCanvas _paths = null!;
    TextureRect _map = null!;
    Control _nodeLayer = null!;
    readonly Dictionary<string, Button> _buttons = new();
    readonly Dictionary<string, Label> _labels = new();
    string _selectedId = "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var backdrop = new ColorRect { Color = new Color("05080d") };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        _map = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.82f, 0.86f, 0.9f)
        };
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_map);
        _map.Texture = ChromaArt.LoadArt("Backgrounds/atlas_map.png");

        var veil = new ColorRect { Color = new Color("07101855"), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        _paths = new AtlasCanvas { Game = Game, MouseFilter = MouseFilterEnum.Ignore };
        _paths.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_paths);

        _nodeLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _nodeLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_nodeLayer);

        var title = Ui.Text("ATLAS DE EXPEDIÇÃO", 30);
        title.Position = new Vector2(36, 22);
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        AddChild(title);

        _hud = Ui.Text("", 15);
        _hud.Position = new Vector2(38, 62);
        _hud.AddThemeColorOverride("font_color", new Color("c5d0dc"));
        AddChild(_hud);

        var camp = Ui.Button("Acampamento", () => CampRequested?.Invoke());
        camp.CustomMinimumSize = new Vector2(180, 42);
        camp.SetAnchorsPreset(LayoutPreset.TopRight);
        camp.AnchorLeft = 1;
        camp.OffsetLeft = -214;
        camp.OffsetRight = -34;
        camp.OffsetTop = 24;
        camp.OffsetBottom = 66;
        AddChild(camp);

        BuildNodes();
        BuildDetail();
        RefreshHud();
        CallDeferred(nameof(LayoutNodes));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutNodes();
    }

    void BuildNodes()
    {
        foreach (var child in _nodeLayer.GetChildren()) child.QueueFree();
        _buttons.Clear();
        _labels.Clear();
        foreach (var node in Game.AtlasNodes)
        {
            var captured = node;
            var button = new Button { Text = Icon(node.Kind), TooltipText = node.Title, CustomMinimumSize = new Vector2(58, 58) };
            button.AddThemeFontSizeOverride("font_size", 22);
            button.Disabled = node.Status == AtlasNodeStatus.Locked && !node.Persistent;
            button.AddThemeStyleboxOverride("normal", NodeStyle(node, false));
            button.AddThemeStyleboxOverride("hover", NodeStyle(node, true));
            button.AddThemeStyleboxOverride("pressed", NodeStyle(node, true));
            button.AddThemeStyleboxOverride("disabled", NodeStyle(node, false));
            button.Pressed += () => Select(captured);
            _nodeLayer.AddChild(button);
            _buttons[node.Id] = button;

            var label = Ui.Text(node.Persistent ? node.Title : node.Title, 12);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(140, 0);
            label.AddThemeColorOverride("font_color", node.Status == AtlasNodeStatus.Locked && !node.Persistent ? new Color("6a7684") : new Color("efe6d2"));
            _nodeLayer.AddChild(label);
            _labels[node.Id] = label;
        }
    }

    void LayoutNodes()
    {
        if (_nodeLayer == null || _paths == null || Size.X < 8 || Size.Y < 8) return;
        foreach (var node in Game.AtlasNodes)
        {
            if (!_buttons.TryGetValue(node.Id, out var button)) continue;
            var pos = new Vector2(node.X * Size.X, node.Y * Size.Y);
            button.Position = pos - new Vector2(29, 29);
            if (_labels.TryGetValue(node.Id, out var label))
                label.Position = pos + new Vector2(-70, 34);
        }
        _paths.QueueRedraw();
    }

    public void RefreshProgress()
    {
        if (_buttons.Count != Game.AtlasNodes.Count || Game.AtlasNodes.Any(n => !_buttons.ContainsKey(n.Id)))
        {
            BuildNodes();
            LayoutNodes();
        }
        foreach (var node in Game.AtlasNodes)
        {
            if (_buttons.TryGetValue(node.Id, out var button))
            {
                button.Disabled = node.Status == AtlasNodeStatus.Locked && !node.Persistent;
                button.AddThemeStyleboxOverride("normal", NodeStyle(node, node.Id == _selectedId));
                button.AddThemeStyleboxOverride("hover", NodeStyle(node, true));
            }
            if (_labels.TryGetValue(node.Id, out var label))
                label.AddThemeColorOverride("font_color", node.Status == AtlasNodeStatus.Locked && !node.Persistent ? new Color("6a7684") : new Color("efe6d2"));
        }
        RefreshHud();
        _paths.QueueRedraw();
    }

    void RefreshHud()
    {
        if (_hud == null) return;
        var difficulty=Game.ExpeditionIndex switch {0=>"Calma",1=>"Tensa",2=>"Severa",_=>"Implacável"};
        _hud.Text = $"{Game.WorldLore.RegionName}  ·  Expedição {Game.ExpeditionIndex+1}  ·  Dificuldade {difficulty}  ·  {Game.Player.Coins} Reais  ·  Nv.{Game.Player.Level}";
    }

    static StyleBoxFlat NodeStyle(AtlasNodeData node, bool selected)
    {
        var color = node.Persistent
            ? new Color("2f6d73")
            : node.Status switch
            {
                AtlasNodeStatus.Completed => new Color("315f55"),
                AtlasNodeStatus.Available => KindColor(node.Kind),
                _ => new Color("2a313c")
            };
        if (selected) color = color.Lightened(0.18f);
        return new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = selected ? new Color("f2e2b0") : color.Lightened(0.28f),
            BorderWidthTop = selected ? 3 : 2,
            BorderWidthRight = selected ? 3 : 2,
            BorderWidthBottom = selected ? 3 : 2,
            BorderWidthLeft = selected ? 3 : 2,
            CornerRadiusTopLeft = 29,
            CornerRadiusTopRight = 29,
            CornerRadiusBottomLeft = 29,
            CornerRadiusBottomRight = 29
        };
    }

    static Color KindColor(AtlasNodeKind kind) => kind switch
    {
        AtlasNodeKind.Combat => new Color("8a3d32"),
        AtlasNodeKind.Boss => new Color("8a6c37"),
        AtlasNodeKind.Merchant => new Color("6d5a2e"),
        AtlasNodeKind.Character => new Color("7a4458"),
        AtlasNodeKind.Rest => new Color("2f6d73"),
        AtlasNodeKind.Mystery => new Color("4a3f6d"),
        AtlasNodeKind.Scene => new Color("3a5470"),
        _ => new Color("4e6744")
    };

    static string Icon(AtlasNodeKind type) => type switch
    {
        AtlasNodeKind.Merchant => "¤",
        AtlasNodeKind.Combat => "⚔",
        AtlasNodeKind.Rest => "☾",
        AtlasNodeKind.Character => "♥",
        AtlasNodeKind.Boss => "✦",
        AtlasNodeKind.Scene => "◆",
        AtlasNodeKind.Mystery => "?",
        _ => "•"
    };

    void BuildDetail()
    {
        var detail = new PanelContainer();
        detail.SetAnchorsPreset(LayoutPreset.BottomRight);
        detail.OffsetLeft = -390;
        detail.OffsetTop = -228;
        detail.OffsetRight = -28;
        detail.OffsetBottom = -24;
        detail.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101925ee"),
            BorderColor = new Color("b9985d88"),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        detail.AddChild(box);
        _detailTitle = Ui.Text("Escolha um lugar", 20);
        box.AddChild(_detailTitle);
        _detailText = Ui.Text("Os caminhos dourados mostram o que está disponível. O acampamento permanece aberto.", 15);
        _detailText.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(_detailText);
        _travel = Ui.Button("Viajar", TravelSelected);
        _travel.Disabled = true;
        box.AddChild(_travel);
        AddChild(detail);
    }

    void Select(AtlasNodeData node)
    {
        if (node.Status == AtlasNodeStatus.Locked && !node.Persistent) return;
        _detailTitle.Text = node.Title;
        _detailText.Text = $"{KindName(node.Kind)}\nRisco {RiskLabel(node.Risk)}\n{node.Description}";
        _travel.Disabled = false;
        _travel.Text = node.Persistent ? "Entrar no acampamento" : "Viajar";
        if (_selectedId == node.Id) NodeSelected?.Invoke(node.Id);
        else _selectedId = node.Id;
        RefreshProgress();
    }

    void TravelSelected()
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        NodeSelected?.Invoke(_selectedId);
    }

    static string KindName(AtlasNodeKind kind) => kind switch
    {
        AtlasNodeKind.Merchant => "Mercador",
        AtlasNodeKind.Combat => "Confronto",
        AtlasNodeKind.Rest => "Descanso",
        AtlasNodeKind.Character => "Encontro",
        AtlasNodeKind.Boss => "Ponto decisivo",
        AtlasNodeKind.Mystery => "Mistério",
        AtlasNodeKind.Scene => "Cena",
        _ => "Evento"
    };

    static string RiskLabel(int risk) => risk switch { <= 0 => "nenhum", 1 => "baixo", 2 => "moderado", 3 => "alto", _ => "severo" };

    sealed partial class AtlasCanvas : Control
    {
        public GameSave Game { get; set; } = null!;
        public override void _Draw()
        {
            foreach (var node in Game.AtlasNodes)
            foreach (var id in node.Connections)
            {
                var other = Game.AtlasNodes.FirstOrDefault(n => n.Id == id);
                if (other == null) continue;
                var available = (node.Status != AtlasNodeStatus.Locked || node.Persistent) && (other.Status != AtlasNodeStatus.Locked || other.Persistent);
                var from = new Vector2(node.X * Size.X, node.Y * Size.Y);
                var to = new Vector2(other.X * Size.X, other.Y * Size.Y);
                DrawDashedLine(from, to, available ? new Color("c4a15ecc") : new Color("2b3544aa"), available ? 2.4f : 1.6f, 7);
            }
        }
        public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
    }
}
