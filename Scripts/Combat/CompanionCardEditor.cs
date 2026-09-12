using Godot;
namespace Heartbeat;

public partial class CompanionCardEditor : Control
{
    public CharacterData Data {get;set;}=new();
    public Action? Closed;
    CompanionCardData _draft=new();
    VBoxContainer _body=null!,_cards=null!;
    Label _status=null!;
    Control? _child;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_draft=CardRules.Copy(Data.CardData??new());
        CardUi.Screen(this,"CARTA DE COMPANHEIRO · "+Data.Name,out _body,()=>Closed?.Invoke());
        var enabled=new CheckButton {Text="Pode virar carta",ButtonPressed=_draft.Enabled};_body.AddChild(enabled);enabled.Toggled+=v=>_draft.Enabled=v;
        _body.AddChild(Ui.Text("Conhecido: confiança 10 · Amigo: afeto 30/confiança 25 · Confiança: 50 · Romance: 35 ou encontro especial.",14));
        var row=new HBoxContainer();_body.AddChild(row);
        row.AddChild(Ui.Button("Design e habilidades do companheiro",()=>Edit(_draft.Companion,true,-1)));
        row.AddChild(Ui.Button("Criar golpe especial",()=>{if(_draft.Specials.Count>=4){_status.Text="Máximo de 4 golpes.";return;}Edit(new(){Name="Golpe de "+Data.Name,Requirement="Amigo"},false,-1);}));
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill};_body.AddChild(scroll);_cards=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_cards);
        _status=Ui.Text("As mudanças são gravadas ao salvar o personagem.",14);_body.AddChild(_status);
        _body.AddChild(Ui.Button("Aplicar design e golpes",()=>{Data.CardData=CardRules.Copy(_draft);Closed?.Invoke();}));Refresh();
    }
    void Edit(CardDefinition card,bool companion,int index)
    {
        var editor=new CardEditor {Initial=card,Character=Data,Companion=companion};_child=editor;
        editor.Applied=c=>{if(companion)_draft.Companion=c;else if(index>=0)_draft.Specials[index]=c;else _draft.Specials.Add(c);Refresh();};
        editor.Closed=()=>{editor.QueueFree();_child=null;};AddChild(editor);
    }
    void Refresh()
    {
        CardUi.Clear(_cards);
        _cards.AddChild(Ui.Text(_draft.Companion.Name+" · "+CardRules.Describe(_draft.Companion),18));
        for(int i=0;i<_draft.Specials.Count;i++){int index=i;var c=_draft.Specials[i];var row=new HBoxContainer();_cards.AddChild(row);row.AddChild(Ui.Text(c.Name+" · "+c.Requirement,16));row.AddChild(Ui.Button("Editar",()=>Edit(c,false,index)));row.AddChild(Ui.Button("Remover",()=>{_draft.Specials.RemoveAt(index);Refresh();}));}
    }
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed&&k.Keycode==Key.Escape&&_child!=null){_child.QueueFree();_child=null;GetViewport().SetInputAsHandled();}}
}
