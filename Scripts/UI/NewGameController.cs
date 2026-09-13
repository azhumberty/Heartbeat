using Godot;

namespace Heartbeat;

/// <summary>Short offline-first campaign prologue: lore, then a narrative-only player name.</summary>
public partial class NewGameController : Control
{
    public Action<GameSave>? Completed;
    public Action? Cancelled;
    GameSave _save=null!;
    VBoxContainer _body=null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _save=new GameSave {WorldSeed=Random.Shared.NextInt64(),ActionsLeft=6,FirstPerson=false};
        _save.WorldLore=WorldLoreManager.CreateOffline(_save.WorldSeed);_save.AtlasNodes=AtlasGenerator.Create(_save.WorldSeed);
        var characters=new CharacterRepository().List().Where(c=>c.CanBuildRelationship).ToList();
        if(characters.Count==0)characters.Add(new CharacterRepository().LoadDemo());
        _save.CharacterIds=characters.Select(c=>c.Id).Distinct().ToList();
        _save.CharacterStates=characters.ToDictionary(c=>c.Id,_=>new CharacterState {CurrentLocation="road"});
        DeckManager.Migrate(_save);
        var shade=new ColorRect {Color=new Color("03070add")};shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(shade);
        Ui.Panel(this,"UMA NOVA CAMPANHA",out _body,()=>Cancelled?.Invoke());ShowLore();
    }
    void Clear(){foreach(var child in _body.GetChildren().Skip(1).ToArray()){_body.RemoveChild(child);child.QueueFree();}}
    void ShowLore()
    {
        Clear();var lore=_save.WorldLore;
        _body.AddChild(Ui.Text(lore.RegionName.ToUpperInvariant(),31));
        _body.AddChild(Ui.Text(lore.Premise,20));
        _body.AddChild(Ui.Text("Ameaça: "+lore.Threat,16));
        _body.AddChild(Ui.Text("Rumor: "+lore.Rumors.FirstOrDefault(),16));
        _body.AddChild(new Control {SizeFlagsVertical=SizeFlags.ExpandFill});
        _body.AddChild(Ui.Button("Continuar",ShowName));
    }
    void ShowName()
    {
        Clear();_body.AddChild(Ui.Text("Como devemos chamá-lo?",28));_body.AddChild(Ui.Text("Seu nome será usado apenas na narrativa. Você não terá avatar ou aparência visual.",16));
        var name=new LineEdit {PlaceholderText="Nome",MaxLength=32,CustomMinimumSize=new Vector2(0,48)};_body.AddChild(name);
        var status=Ui.Text("",15);status.AddThemeColorOverride("font_color",new Color("e5b18b"));_body.AddChild(status);
        void Confirm(){string value=name.Text.Trim();if(value.Length<2){status.Text="Escolha um nome com pelo menos 2 letras.";return;}_save.PlayerName=value;Completed?.Invoke(_save);}
        name.TextSubmitted+=_=>Confirm();_body.AddChild(Ui.Button("Entrar no atlas",Confirm));name.GrabFocus();
    }
}
