using Godot;
namespace Heartbeat;

/// <summary>Shared data-only editor for custom, companion and special cards.</summary>
public partial class CardEditor : Control
{
    public CardDefinition? Initial {get;set;}
    public CharacterData? Character {get;set;}
    public bool Companion {get;set;}
    public Action<CardDefinition>? Applied;
    public Action? Closed;
    public CardRepository Repository {get;set;}=new();
    CardDefinition _draft=new();
    VBoxContainer _form=null!,_preview=null!;
    Label _status=null!;
    OptionButton _existing=null!,_category=null!,_rarity=null!,_animation=null!,_condition=null!,_requirement=null!,_passive=null!;
    LineEdit _name=null!,_title=null!,_tags=null!,_phrase=null!;
    TextEdit _description=null!;
    SpinBox _cost=null!,_passiveValue=null!;
    ColorPickerButton _color=null!;
    readonly List<(OptionButton Kind,OptionButton Target,SpinBox Value,SpinBox Duration,CheckButton Enabled)> _effects=new();
    List<CardDefinition> _custom=new();
    bool _loading;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        CardUi.Screen(this,Companion?"DESIGN DA CARTA DE COMPANHEIRO":Character!=null?"GOLPE ESPECIAL":"CRIAR CARTAS",out var body,()=>Closed?.Invoke());
        var row=new HBoxContainer();body.AddChild(row);
        if(Character==null)
        {
            _existing=new OptionButton {SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(_existing);ReloadList();
            _existing.ItemSelected+=i=>LoadCard(_custom[(int)i]);
            row.AddChild(Ui.Button("Nova",()=>LoadCard(new())));
            row.AddChild(Ui.Button("Duplicar",()=>{var c=Read();c.Id="custom_"+Guid.NewGuid().ToString("N");c.Name+=" (cópia)";LoadCard(c);}));
            row.AddChild(Ui.Button("Excluir",()=>{try{Repository.DeleteCustom(_draft.Id);LoadCard(new());ReloadList();_status.Text="Carta customizada excluída. Baralhos que a usavam precisam de ajuste.";}catch(Exception e){_status.Text=e.Message;}}));
        }
        var layout=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};layout.AddThemeConstantOverride("separation",24);body.AddChild(layout);
        var scroll=new ScrollContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};layout.AddChild(scroll);
        _form=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_form);
        LineEdit TextField(string label){_form.AddChild(Ui.Text(label,13));var f=new LineEdit();_form.AddChild(f);f.TextChanged+=_=>Preview();return f;}
        _name=TextField("Nome");_title=TextField("Título");_phrase=TextField("Fala curta");_tags=TextField("Tags, separadas por vírgula");
        _form.AddChild(Ui.Text("Descrição",13));_description=new TextEdit {CustomMinimumSize=new(0,70),WrapMode=TextEdit.LineWrappingMode.Boundary};_form.AddChild(_description);_description.TextChanged+=Preview;
        _category=CardUi.Options(_form,"Categoria",Enum.GetValues<CardCategory>().Select(CardRules.CategoryName));
        _rarity=CardUi.Options(_form,"Raridade",new[]{"Comum","Rara","Épica"});
        _form.AddChild(Ui.Text("Custo de Mana (0–60)",13));_cost=new SpinBox {MinValue=0,MaxValue=60};_form.AddChild(_cost);_cost.ValueChanged+=_=>Preview();
        _animation=CardUi.Options(_form,"Animação",new[]{"Impacto","Luz","Pulso"});
        _condition=CardUi.Options(_form,"Condição de uso",new[]{"Sempre","Inimigo ferido"});
        _requirement=CardUi.Options(_form,"Vínculo necessário (cartas de personagem)",new[]{"Conhecido","Amigo","Confiança","Romance"});_requirement.Disabled=Character==null;
        for(int i=0;i<3;i++)
        {
            var enabled=new CheckButton {Text=$"Efeito {i+1}",ButtonPressed=i==0};_form.AddChild(enabled);
            var line=new HBoxContainer();_form.AddChild(line);
            var kind=new OptionButton();foreach(var e in Enum.GetValues<EffectKind>())kind.AddItem(CardRules.EffectName(e));line.AddChild(kind);
            var target=new OptionButton();target.AddItem("Inimigo");target.AddItem("Você");line.AddChild(target);
            var value=new SpinBox {MinValue=1,MaxValue=35,Value=6,TooltipText="Valor do efeito"};line.AddChild(value);
            var duration=new SpinBox {MinValue=1,MaxValue=3,Value=1,TooltipText="Duração em turnos"};line.AddChild(duration);
            _effects.Add((kind,target,value,duration,enabled));
            kind.ItemSelected+=_=>Preview();target.ItemSelected+=_=>Preview();value.ValueChanged+=_=>Preview();duration.ValueChanged+=_=>Preview();enabled.Toggled+=_=>Preview();
        }
        _passive=CardUi.Options(_form,"Passiva a cada turno (companheiro)",new[]{"Nenhuma","Escudo","Cura","Mana"});_passive.Disabled=!Companion;
        _passiveValue=new SpinBox {MinValue=1,MaxValue=5,Value=2};_form.AddChild(_passiveValue);_passiveValue.ValueChanged+=_=>Preview();
        _form.AddChild(Ui.Text("Cor da moldura",13));_color=new ColorPickerButton {CustomMinimumSize=new(0,32)};_form.AddChild(_color);_color.ColorChanged+=_=>Preview();
        _form.AddChild(Ui.Button("Importar imagem",ImportImage));
        _form.AddChild(Ui.Button("Usar imagem do personagem / símbolo",()=>{_draft.ImagePath="";Preview();}));
        if(Character!=null)
        {
            var art=CardUi.Options(_form,"Reutilizar retrato importado",new[]{"Roupa atual / automático"}.Concat(Character.Images.Select((_,i)=>"Retrato "+(i+1))));
            art.ItemSelected+=i=>{_draft.ImagePath=i==0?"":Character.Images[(int)i-1].ProcessedPath;Preview();};
        }
        var previewScroll=new ScrollContainer {CustomMinimumSize=new(286,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};layout.AddChild(previewScroll);
        _preview=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};previewScroll.AddChild(_preview);
        _status=Ui.Text("",14);body.AddChild(_status);
        body.AddChild(Ui.Button(Character==null?"Salvar carta customizada":"Aplicar ao personagem",SaveCard));
        foreach(var b in new[]{_category,_rarity,_animation,_condition,_requirement,_passive})b.ItemSelected+=_=>Preview();
        LoadCard(Initial??new());
    }
    void ReloadList(){_existing.Clear();_custom=Repository.Custom();foreach(var c in _custom)_existing.AddItem(c.Name);}
    static void Choose(OptionButton b,string value){for(int i=0;i<b.ItemCount;i++)if(b.GetItemText(i)==value){b.Select(i);return;}b.Select(0);}
    void LoadCard(CardDefinition c)
    {
        _loading=true;_draft=CardRules.Copy(c);
        _name.Text=c.Name;_title.Text=c.Title;_phrase.Text=c.Phrase;_tags.Text=string.Join(", ",c.Tags);_description.Text=c.Description;
        _category.Select((int)(Companion?CardCategory.Companion:c.Category));_category.Disabled=Companion;
        Choose(_rarity,c.Rarity);Choose(_animation,c.Animation);Choose(_condition,c.Condition);Choose(_requirement,c.Requirement);
        _cost.Value=c.Cost;_color.Color=Color.FromString(c.FrameColor,new Color("b39a64"));
        for(int i=0;i<3;i++){var row=_effects[i];row.Enabled.ButtonPressed=i<c.Effects.Count;if(i<c.Effects.Count){var e=c.Effects[i];row.Kind.Select((int)e.Kind);row.Target.Select((int)e.Target);row.Value.Value=e.Value;row.Duration.Value=e.Duration;}}
        _passive.Select(c.Passive==null?0:c.Passive.Kind==EffectKind.Shield?1:c.Passive.Kind==EffectKind.Heal?2:3);_passiveValue.Value=c.Passive?.Value??2;
        _loading=false;Preview();
    }
    CardDefinition Read()
    {
        var c=CardRules.Copy(_draft);c.Name=_name.Text;c.Title=_title.Text;c.Phrase=_phrase.Text;c.Tags=_tags.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
        c.Description=_description.Text;c.Cost=(int)_cost.Value;c.Category=(CardCategory)_category.Selected;c.Rarity=CardUi.Selected(_rarity);c.Animation=CardUi.Selected(_animation);c.Condition=CardUi.Selected(_condition);c.Requirement=CardUi.Selected(_requirement);c.FrameColor=_color.Color.ToHtml(false);
        c.Effects=_effects.Where(r=>r.Enabled.ButtonPressed).Select(r=>new CardEffect {Kind=(EffectKind)r.Kind.Selected,Target=(CardTarget)r.Target.Selected,Value=(int)r.Value.Value,Duration=(int)r.Duration.Value}).ToList();
        c.Passive=Companion&&_passive.Selected>0?new CardEffect {Kind=_passive.Selected==1?EffectKind.Shield:_passive.Selected==2?EffectKind.Heal:EffectKind.Mana,Target=CardTarget.Self,Value=(int)_passiveValue.Value}:null;
        return c;
    }
    void Preview()
    {
        if(_loading||_preview==null)return;
        var c=Read();CardUi.Clear(_preview);
        if(c.ImagePath==""&&Character!=null)c.ImagePath=CardArt.PathFor(Character,null);
        _preview.AddChild(new CardView {Card=c});
        _preview.AddChild(Ui.Text(CardRules.BalanceWarning(c),14));
        _preview.AddChild(Ui.Text("Efeitos têm limites: dano/cura/escudo até 35; compra até 3; status até 3 turnos; atordoamento 1 turno.",13));
    }
    void SaveCard()
    {
        try {var c=CardRules.Validate(Read());if(Character==null){Repository.SaveCustom(c);_draft=c;ReloadList();_status.Text="Carta salva. Ela já aparece na coleção do baralho.";}else{Applied?.Invoke(c);Closed?.Invoke();}}
        catch(Exception e){_status.Text=e.Message;}
    }
    void ImportImage()
    {
        var dialog=new FileDialog {Access=FileDialog.AccessEnum.Filesystem,FileMode=FileDialog.FileModeEnum.OpenFile,Filters=new[]{"*.png,*.jpg,*.webp ; Imagens"}};
        AddChild(dialog);dialog.FileSelected+=path=>
        {
            try
            {
                if(new FileInfo(path).Length>40_000_000)throw new ArgumentException("Use uma imagem menor que 40 MB.");
                using var image=Image.LoadFromFile(path);
                if(image==null||image.IsEmpty())throw new ArgumentException("Imagem inválida.");
                int longest=Math.Max(image.GetWidth(),image.GetHeight());if(longest>1024)image.Resize(Math.Max(1,image.GetWidth()*1024/longest),Math.Max(1,image.GetHeight()*1024/longest));
                string folder=Character==null?"user://Cards/art":$"user://Characters/{Character.Id}/cards";
                Directory.CreateDirectory(ProjectSettings.GlobalizePath(folder));string destination=folder+"/"+Guid.NewGuid().ToString("N")+".png";
                if(image.SavePng(destination)!=Error.Ok)throw new IOException("Não foi possível salvar imagem.");
                _draft.ImagePath=destination;Preview();
            }catch(Exception e){_status.Text=e.Message;}
            dialog.QueueFree();
        };dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered(new(850,550));
    }
}
public static class CardArt
{
    public static string PathFor(CharacterData data,CharacterState? state)
    {
        var outfit=Wardrobe.Resolve(data,state?.CurrentOutfitId??data.DefaultOutfitId);
        if(outfit is {UseLegacy:false}&&outfit.Idle.Cutout.Length>0)return Wardrobe.PathFor(data,outfit.Idle.Cutout);
        return data.MainImagePath.Length>0?data.MainImagePath:data.Images.FirstOrDefault()?.ProcessedPath??"";
    }
    public static Texture2D? TextureFor(CardDefinition card,GameSave game)
    {
        var cache=new PortraitCache();
        if(card.ImagePath.Length>0)return cache.LoadRaw(ProjectSettings.GlobalizePath(card.ImagePath));
        if(card.CharacterId.Length==0)return null;
        var person=new CharacterRepository().Load(card.CharacterId);
        return person==null?null:cache.Get(person,game.CharacterStates.GetValueOrDefault(person.Id)??new());
    }
}
