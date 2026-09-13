using Godot;

namespace Heartbeat;

public partial class EventController : Control
{
    public GameSave Game { get; set; } = null!;
    public EventResult Story { get; set; } = null!;
    public Action? Completed;
    VBoxContainer _body=null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var spacer=new Control {MouseFilter=MouseFilterEnum.Ignore};spacer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(spacer);
        var panel=new PanelContainer();panel.SetAnchorsPreset(LayoutPreset.BottomWide);panel.OffsetLeft=70;panel.OffsetRight=-70;panel.OffsetTop=-230;panel.OffsetBottom=-24;AddChild(panel);
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("081019e8"),BorderColor=new Color("b9985d99"),BorderWidthTop=1,BorderWidthBottom=1,BorderWidthLeft=1,BorderWidthRight=1,CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=18,ContentMarginBottom=18});
        _body=new VBoxContainer();_body.AddThemeConstantOverride("separation",9);panel.AddChild(_body);ShowChoices();
    }

    void ShowChoices()
    {
        _body.AddChild(Ui.Text(Story.Title,27));_body.AddChild(Ui.Text(Story.Text,18));
        var status=Ui.Text(Story.ProviderStatus,13);status.AddThemeColorOverride("font_color",new Color("8595a8"));_body.AddChild(status);
        var choices=new VBoxContainer();choices.AddThemeConstantOverride("separation",7);_body.AddChild(choices);
        foreach(var choice in Story.Choices){var selected=choice;var button=Ui.Button(choice.Text,()=>Choose(selected));button.SizeFlagsHorizontal=SizeFlags.ExpandFill;choices.AddChild(button);}
    }
    void Choose(EventChoice choice)
    {
        foreach(var child in _body.GetChildren()){_body.RemoveChild(child);child.QueueFree();}
        var result=new ProceduralEventService().Apply(Game,Story,choice);
        _body.AddChild(Ui.Text(Story.Title,27));_body.AddChild(Ui.Text(result,19));_body.AddChild(new Control {SizeFlagsVertical=SizeFlags.ExpandFill});_body.AddChild(Ui.Button("Continuar para o Atlas",()=>Completed?.Invoke()));
    }
}
