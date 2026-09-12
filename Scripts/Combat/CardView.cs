using Godot;
using System;
using System.Collections.Generic;

namespace Heartbeat;

public partial class CardView : PanelContainer
{
    public CardDefinition Card { get; set; } = new();
    public Action? Clicked;
    public bool Compact { get; set; }
    static readonly PortraitCache Images = new();

    public override void _Ready()
    {
        CustomMinimumSize = Compact ? new(160, 230) : new(240, 350);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        
        var color = Color.FromString(Card.FrameColor, new Color("b39a64"));
        var bgColor = new Color("0a0e12"); // Dark fantasy base
        
        // Base Panel (Drop shadow, rounded corners)
        var styleBase = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = color,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ShadowColor = new Color(0, 0, 0, 0.4f), ShadowSize = 4, ShadowOffset = new Vector2(0, 4)
        };
        AddThemeStyleboxOverride("panel", styleBase);

        // Content Wrapper to ensure margins
        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", Compact ? 8 : 12);
        contentMargin.AddThemeConstantOverride("margin_right", Compact ? 8 : 12);
        contentMargin.AddThemeConstantOverride("margin_top", Compact ? 8 : 12);
        contentMargin.AddThemeConstantOverride("margin_bottom", Compact ? 8 : 12);
        contentMargin.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(contentMargin);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        contentMargin.AddChild(box);

        // Top Row (Title & Cost)
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(top);

        var titleLabel = new Label
        {
            Text = Card.Name,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimWordEllipsis,
            ClipText = true,
            LabelSettings = new LabelSettings { FontSize = Compact ? 14 : 18, FontColor = new Color("e8e0d4"), ShadowColor = Colors.Black, ShadowSize = 1 }
        };
        top.AddChild(titleLabel);

        var costCircle = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        costCircle.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("1a242d"), CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color("93c9ee") });
        var costLabel = new Label
        {
            Text = Card.Cost.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = Compact ? new Vector2(24, 24) : new Vector2(32, 32),
            LabelSettings = new LabelSettings { FontSize = Compact ? 16 : 22, FontColor = new Color("93c9ee"), OutlineSize = 2, OutlineColor = Colors.Black }
        };
        costCircle.AddChild(costLabel);
        top.AddChild(costCircle);

        // Art Frame with ClipChildren (Masking)
        var artFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, Compact ? 80 : 130),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipChildren = ClipChildrenMode.Only // This masks the child image!
        };
        // The mask shape
        artFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Colors.White, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 });
        box.AddChild(artFrame);

        var texture = Card.ImagePath.Length > 0 ? Images.LoadRaw(ProjectSettings.GlobalizePath(Card.ImagePath)) : null;
        if (texture != null)
        {
            var picture = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered // Fills the frame cleanly
            };
            artFrame.AddChild(picture);
            picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        else
        {
            // Fallback generic art based on Category
            var fallbackBg = new ColorRect { Color = new Color(0.1f, 0.12f, 0.15f) };
            artFrame.AddChild(fallbackBg);
            var glyph = new CardGlyph { Category = Card.Category, Tint = color, MouseFilter = MouseFilterEnum.Ignore };
            artFrame.AddChild(glyph);
            glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        // Category/Rarity Ribbon
        var ribbon = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        ribbon.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = color.Darkened(0.6f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        var categoryLabel = new Label
        {
            Text = CardRules.CategoryName(Card.Category).ToUpperInvariant() + " · " + Card.Rarity,
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = Compact ? 10 : 12, FontColor = color.Lightened(0.5f) }
        };
        ribbon.AddChild(categoryLabel);
        box.AddChild(ribbon);

        // Description Box
        var descBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(descBox);

        if (!Compact && Card.Title.Length > 0)
        {
            descBox.AddChild(new Label { Text = Card.Title, AutowrapMode = TextServer.AutowrapMode.Word, LabelSettings = new LabelSettings { FontSize = 13, FontColor = Colors.White } });
        }

        var effectsLabel = new Label
        {
            Text = CardRules.Describe(Card),
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            LabelSettings = new LabelSettings { FontSize = Compact ? 11 : 14, FontColor = new Color("e8e0d4") }
        };
        descBox.AddChild(effectsLabel);

        if (!Compact && Card.Description.Length > 0)
        {
            descBox.AddChild(new Label { Text = Card.Description, AutowrapMode = TextServer.AutowrapMode.Word, LabelSettings = new LabelSettings { FontSize = 12, FontColor = new Color(0.7f, 0.7f, 0.7f) } });
        }

        if (!Compact && Card.Condition != "Sempre")
        {
            descBox.AddChild(new Label { Text = "Requer: " + Card.Condition, AutowrapMode = TextServer.AutowrapMode.Word, LabelSettings = new LabelSettings { FontSize = 12, FontColor = new Color("ff9999") } });
        }

        IgnoreChildren(this);

        // Hover animations (store base position relative)
        MouseEntered += () => {
            PivotOffset = Size / 2;
            var t = CreateTween().SetParallel(true);
            t.TweenProperty(this, "scale", Vector2.One * 1.05f, 0.15f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(this, "position:y", Position.Y - 12f, 0.15f);
            
            // Glow effect
            var s = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
            s.ShadowColor = color;
            s.ShadowSize = 12;
            AddThemeStyleboxOverride("panel", s);
        };

        MouseExited += () => {
            var t = CreateTween().SetParallel(true);
            t.TweenProperty(this, "scale", Vector2.One, 0.15f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(this, "position:y", Position.Y + 12f, 0.15f); // Restore position
            AddThemeStyleboxOverride("panel", styleBase); // Remove glow
        };

        GuiInput += e => {
            if (e is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left)
            {
                AcceptEvent();
                Clicked?.Invoke();
            }
        };
    }

    static void IgnoreChildren(Node root)
    {
        foreach (var n in root.GetChildren())
        {
            if (n is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
            IgnoreChildren(n);
        }
    }
}

public partial class CardGlyph : Control
{
    public CardCategory Category;
    public Color Tint;

    public override void _Draw()
    {
        Vector2 center = Size / 2;
        float r = Math.Min(Size.X, Size.Y) * 0.38f;
        
        DrawArc(center, r, 0, Mathf.Tau, 40, new Color(Tint, 0.45f), 2, true);
        DrawArc(center, r * 0.76f, 0, Mathf.Tau, 6, Tint, 2, true);
        
        if (Category == CardCategory.Attack)
        {
            DrawLine(center + new Vector2(-r * 0.5f, r * 0.55f), center + new Vector2(r * 0.5f, -r * 0.55f), Tint, 6, true);
            DrawLine(center + new Vector2(-r * 0.55f, 0), center + new Vector2(0, r * 0.55f), Tint, 3, true);
        }
        else if (Category == CardCategory.Heal)
        {
            DrawLine(center + new Vector2(-r * 0.5f, 0), center + new Vector2(r * 0.5f, 0), Tint, 5);
            DrawLine(center + new Vector2(0, -r * 0.5f), center + new Vector2(0, r * 0.5f), Tint, 5);
        }
        else
        {
            DrawCircle(center, r * 0.23f, Tint);
            for (int i = 0; i < 4; i++)
            {
                var d = Vector2.FromAngle(i * Mathf.Pi / 2);
                DrawLine(center + d * r * 0.4f, center + d * r * 0.6f, Tint, 2);
            }
        }
    }
}

public static class CardUi
{
    public static PanelContainer Screen(Control parent, string title, out VBoxContainer body, Action close)
    {
        var p = Ui.Panel(parent, title, out body, close);
        
        // Dark fantasy elegant panels
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color("0a0e12f0"), // Darker, slightly transparent
            BorderColor = new Color("b39a64"), // Gold border
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18,
            ShadowColor = new Color(0,0,0,0.5f), ShadowSize = 10
        };
        p.AddThemeStyleboxOverride("panel", panelStyle);
        return p;
    }
    
    public static void Clear(Node n)
    {
        foreach (var child in n.GetChildren())
        {
            n.RemoveChild(child);
            child.QueueFree();
        }
    }
    
    public static OptionButton Options(Control parent, string label, IEnumerable<string> items)
    {
        parent.AddChild(Ui.Text(label, 13));
        var b = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var item in items) b.AddItem(item);
        parent.AddChild(b);
        return b;
    }
    
    public static string Selected(OptionButton b) => b.GetItemText(b.Selected);
}
