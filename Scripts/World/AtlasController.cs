using Godot;
using System;
using System.Linq;

namespace Heartbeat;

/// <summary>2D campaign map. Its node positions and progress live in GameSave.</summary>
public partial class AtlasController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action<string>? NodeSelected;
    Label _detailTitle = null!, _detailText = null!;
    AtlasCanvas _paths = null!;
    readonly Dictionary<string,Button> _buttons=new();
    readonly Dictionary<string,Label> _labels=new();
    string _selectedId = "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = new Color("091019") };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);

        var title = Ui.Text("ATLAS DE MEMÓRIAS", 34);
        title.Position = new Vector2(44, 30); title.AddThemeColorOverride("font_color", new Color("e5c276")); AddChild(title);
        var subtitle = Ui.Text($"EXPEDIÇÃO {Game.ExpeditionIndex+1} · {Game.WorldLore.RegionName} · {Game.WorldLore.Threat}", 16);
        subtitle.Position = new Vector2(46, 76); subtitle.AddThemeColorOverride("font_color", new Color("aab8c9")); AddChild(subtitle);

        _paths = new AtlasCanvas { Game = Game, MouseFilter = MouseFilterEnum.Ignore };
        _paths.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(_paths);
        BuildNodes(); BuildDetail();
    }

    void BuildNodes()
    {
        foreach (var node in Game.AtlasNodes)
        {
            var button = Ui.Button(Icon(node.Kind), () => Select(node));
            button.TooltipText = node.Title; button.CustomMinimumSize = new Vector2(78, 78);
            button.Position = new Vector2(node.X * Size.X - 39, node.Y * Size.Y - 39);
            button.AddThemeStyleboxOverride("normal", NodeStyle(node.Status));
            button.Disabled = node.Status == AtlasNodeStatus.Locked; AddChild(button);_buttons[node.Id]=button;

            var label = Ui.Text(node.Title, 13); label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(150, 0); label.Position = button.Position + new Vector2(-36, 84);
            label.AddThemeColorOverride("font_color", node.Status == AtlasNodeStatus.Locked ? new Color("657080") : new Color("e4e9ef")); AddChild(label);_labels[node.Id]=label;
        }
    }

    public void RefreshProgress()
    {
        foreach(var node in Game.AtlasNodes)
        {
            if(_buttons.TryGetValue(node.Id,out var button)){button.Disabled=node.Status==AtlasNodeStatus.Locked;button.AddThemeStyleboxOverride("normal",NodeStyle(node.Status));}
            if(_labels.TryGetValue(node.Id,out var label))label.AddThemeColorOverride("font_color",node.Status==AtlasNodeStatus.Locked?new Color("657080"):new Color("e4e9ef"));
        }
        _paths.QueueRedraw();
    }

    static StyleBoxFlat NodeStyle(AtlasNodeStatus status)
    {
        var color = status switch { AtlasNodeStatus.Completed => new Color("315f55"), AtlasNodeStatus.Available => new Color("8a6c37"), _ => new Color("313846") };
        return new StyleBoxFlat { BgColor = color, BorderColor = color.Lightened(.28f), BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, CornerRadiusTopLeft = 39, CornerRadiusTopRight = 39, CornerRadiusBottomLeft = 39, CornerRadiusBottomRight = 39 };
    }
    static string Icon(AtlasNodeKind type) => type switch { AtlasNodeKind.Merchant => "¤", AtlasNodeKind.Combat => "⚔", AtlasNodeKind.Rest => "☾", AtlasNodeKind.Character => "♥", AtlasNodeKind.Boss => "✦", AtlasNodeKind.Scene => "◆", AtlasNodeKind.Mystery => "?", _ => "•" };

    void BuildDetail()
    {
        var detail = new PanelContainer(); detail.SetAnchorsPreset(LayoutPreset.BottomRight); detail.OffsetLeft = -370; detail.OffsetTop = -185; detail.OffsetRight = -34; detail.OffsetBottom = -34;
        detail.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("101925e8"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14 });
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); detail.AddChild(box);
        _detailTitle = Ui.Text("Escolha um lugar", 20); box.AddChild(_detailTitle);
        _detailText = Ui.Text("Os caminhos dourados mostram o que está disponível.", 15); _detailText.SizeFlagsVertical = SizeFlags.ExpandFill; box.AddChild(_detailText); AddChild(detail);
    }

    void Select(AtlasNodeData node)
    {
        _detailTitle.Text = node.Title;
        _detailText.Text = $"{KindName(node.Kind)}\n{node.Description}\n\nClique novamente para viajar.";
        if (_selectedId == node.Id) NodeSelected?.Invoke(node.Id);
        else _selectedId = node.Id;
    }
    static string KindName(AtlasNodeKind kind) => kind switch { AtlasNodeKind.Merchant => "Mercador", AtlasNodeKind.Combat => "Confronto", AtlasNodeKind.Rest => "Descanso", AtlasNodeKind.Character => "Encontro", AtlasNodeKind.Boss => "Ponto decisivo", _ => "Evento" };

    sealed partial class AtlasCanvas : Control
    {
        public GameSave Game { get; set; } = null!;
        public override void _Draw()
        {
            foreach (var node in Game.AtlasNodes)
            foreach (var id in node.Connections)
            {
                var other = Game.AtlasNodes.FirstOrDefault(n => n.Id == id); if (other == null) continue;
                var available = node.Status != AtlasNodeStatus.Locked && other.Status != AtlasNodeStatus.Locked;
                DrawLine(new Vector2(node.X * Size.X, node.Y * Size.Y), new Vector2(other.X * Size.X, other.Y * Size.Y), available ? new Color("92753e") : new Color("2b3544"), available ? 3 : 2);
            }
        }
        public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
    }
}
