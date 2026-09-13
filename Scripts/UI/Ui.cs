using Godot;
namespace Heartbeat;

public static class Ui
{
    public static Label Text(string text, int size = 18)
    {
        var n = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        n.AddThemeFontSizeOverride("font_size", size);
        return n;
    }

    public static Label Body(string text, int size = 16)
    {
        var n = Text(text, size);
        n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        n.TextOverrunBehavior = TextServer.OverrunBehavior.NoEllipsis;
        return n;
    }

    public static void FitScrollChild(ScrollContainer scroll, Control child)
    {
        void Fit()
        {
            if (!GodotObject.IsInstanceValid(scroll) || !GodotObject.IsInstanceValid(child)) return;
            var width = scroll.Size.X;
            if (width > 8f) child.CustomMinimumSize = new Vector2(width, 0);
        }
        scroll.Resized += Fit;
        scroll.VisibilityChanged += Fit;
        Fit();
    }

    public static Button Button(string text, Action action)
    {
        var n = new Button { Text = text, CustomMinimumSize = new(0, 42), ClipText = true };
        foreach(var state in new[]{"normal","hover","pressed"}) n.AddThemeStyleboxOverride(state,new StyleBoxFlat { BgColor=new Color(state=="normal"?"263449":state=="hover"?"405671":"566c86"),CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7,ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=9,ContentMarginBottom=9 });
        n.MouseEntered += () => n.CreateTween().TweenProperty(n, "modulate", new Color("eef7ff"), .1);
        n.MouseExited += () => n.CreateTween().TweenProperty(n, "modulate", Colors.White, .14);
        n.Pressed += action; return n;
    }
    public static PanelContainer Panel(Control parent, string title, out VBoxContainer content, Action close)
    {
        var panel = new PanelContainer(); parent.AddChild(panel); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft = 70; panel.OffsetRight = -70; panel.OffsetTop = 35; panel.OffsetBottom = -35;
        panel.ClipContents = true;
        var style = new StyleBoxFlat { BgColor = new Color("131d2ff5"), ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14 };
        panel.AddThemeStyleboxOverride("panel", style);
        content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 10); panel.AddChild(content);
        var header = new HBoxContainer(); content.AddChild(header); var label = Text(title, 28); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(label);
        var closeBtn = Button("Fechar - Esc", close);
        var shortcut = new Shortcut();
        var ev = new InputEventKey { Keycode = Key.Escape };
        shortcut.Events.Add(ev);
        closeBtn.Shortcut = shortcut;
        header.AddChild(closeBtn);
        panel.Modulate = new Color(1,1,1,0); panel.CreateTween().TweenProperty(panel, "modulate:a", 1f, .2);
        return panel;
    }
}
