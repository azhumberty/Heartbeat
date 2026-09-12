using Godot;
using System;

namespace Heartbeat;

public partial class CharacterCreatorController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        // Dark background
        var bg = new ColorRect { Color = new Color("0a0a0f") };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var title = new Label
        {
            Text = "CRIAÇÃO DE PERSONAGEM",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.SetAnchorsPreset(LayoutPreset.TopWide);
        title.OffsetTop = 40;
        AddChild(title);

        var container = new CenterContainer();
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);

        var btn = new Button { Text = "Começar (Demo)" };
        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.Pressed += () => {
            Game.Character = new CharacterData { Id = "player", Name = "Viajante" };
            Closed?.Invoke();
        };
        container.AddChild(btn);
    }
}
