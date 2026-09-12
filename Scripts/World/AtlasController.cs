using Godot;
using System;

namespace Heartbeat;

public partial class AtlasController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action<string>? NodeSelected;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        // Dark background
        var bg = new ColorRect { Color = new Color("0a0a0f") };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var title = new Label
        {
            Text = "ATLAS DE MEMÃ“RIAS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.SetAnchorsPreset(LayoutPreset.TopWide);
        title.OffsetTop = 40;
        AddChild(title);

        var container = new CenterContainer();
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 100);
        grid.AddThemeConstantOverride("v_separation", 80);
        container.AddChild(grid);

        // Define our fixed Nodes for the demo
        grid.AddChild(CreateNode("O Mercador", "merchant_tent", Colors.Gold));
        grid.AddChild(CreateNode("Floresta Sombria", "forest_dark", Colors.DarkGreen));
        grid.AddChild(CreateNode("Acampamento", "camp", Colors.SaddleBrown));
    }

    Control CreateNode(string title, string id, Color color)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 15);
        
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(120, 120),
            Text = " ", // Empty, we will style it
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        
        var style = new StyleBoxFlat
        {
            BgColor = color * 0.5f,
            BorderColor = color,
            BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
            CornerRadiusBottomLeft = 60, CornerRadiusBottomRight = 60, CornerRadiusTopLeft = 60, CornerRadiusTopRight = 60
        };
        btn.AddThemeStyleboxOverride("normal", style);
        
        var hoverStyle = (StyleBoxFlat)style.Duplicate();
        hoverStyle.BgColor = color * 0.8f;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.Pressed += () => NodeSelected?.Invoke(id);
        
        var label = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        
        box.AddChild(btn);
        box.AddChild(label);
        return box;
    }
}
