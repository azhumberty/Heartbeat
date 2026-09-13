using Godot;
using System.Text.Json;

namespace Heartbeat;

public sealed class ContentAssetRecord
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string DisplayName {get;set;}="Novo asset";
    public string Category {get;set;}="Cenários";
    public string Path {get;set;}="";
    public string Tags {get;set;}="";
    public string Description {get;set;}="";
    public string Biome {get;set;}="forest";
    public string Period {get;set;}="Any";
    public string CompatibleEvents {get;set;}="event,scene";
    public float Weight {get;set;}=1f;
    public bool Procedural {get;set;}=true;
    public bool CanBuildRelationship {get;set;}
    public int Age {get;set;}=25;
    public string Personality {get;set;}="misterioso e atento";
    public string SpeechStyle {get;set;}="natural";
    public int Health {get;set;}=80;
    public int Damage {get;set;}=12;
    public int RewardXp {get;set;}=24;
    public int CoinMin {get;set;}=10;
    public int CoinMax {get;set;}=22;
    public bool Enabled {get;set;}=true;
    public bool BuiltIn {get;set;}
}

/// <summary>Small content manager. It registers user art; it never destroys a source file when an entry is disabled.</summary>
public sealed class ContentLibrary
{
    const string Registry="user://ContentLibrary/assets.json";
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public List<ContentAssetRecord> Load()
    {
        var all=BuiltIns();var file=ProjectSettings.GlobalizePath(Registry);
        if(!File.Exists(file))return all;
        try {all.AddRange(JsonSerializer.Deserialize<List<ContentAssetRecord>>(File.ReadAllText(file),Json)??new());}
        catch(Exception e){GD.PushWarning("[Creative] Registro ignorado: "+e.GetType().Name);}
        return all;
    }
    public List<ContentAssetRecord> Enabled(string category)=>Load().Where(a=>a.Enabled&&a.Procedural&&a.Category==category).ToList();
    public ContentAssetRecord? Find(string id)=>Load().FirstOrDefault(a=>a.Enabled&&a.Id==id);
    public static Texture2D? LoadTexture(string path)
    {
        if(path.StartsWith("res://"))return ChromaArt.LoadArt(path);
        var full=ProjectSettings.GlobalizePath(path);if(!File.Exists(full))return null;using var image=ImageManager.LoadImage(full);return image==null||image.IsEmpty()?null:ImageTexture.CreateFromImage(image);
    }
    public void SaveUser(List<ContentAssetRecord> all)
    {
        var user=all.Where(a=>!a.BuiltIn).ToList();var path=ProjectSettings.GlobalizePath(Registry);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(user,Json));File.Move(path+".tmp",path,true);
    }
    public void SyncCharacter(ContentAssetRecord record)
    {
        if(record.BuiltIn||record.Category is not ("Personagens" or "NPCs" or "Mercadores"))return;
        var repository=new CharacterRepository();var character=repository.Load(record.Id)??repository.Create(record.DisplayName);
        character.Id=record.Id;character.Name=record.DisplayName;character.Age=Math.Max(18,record.Age);character.Description=string.IsNullOrWhiteSpace(record.Description)?$"{record.DisplayName}, adulto fictício do mundo de Heartbeat.":record.Description;
        character.Personality=record.Personality;character.SpeechStyle=record.SpeechStyle;character.CanBuildRelationship=record.Category=="Personagens"&&record.CanBuildRelationship;
        character.MainImagePath=record.Path;character.Images.RemoveAll(i=>i.Tags.Contains("creative-main"));character.Images.Add(new CharacterImage{OriginalPath=record.Path,ProcessedPath=record.Path,Tags=new(){"neutral","creative-main"}});
        character.Tags.RemoveAll(t=>t is "creative-managed" or "creative-disabled" or "npc" or "merchant");character.Tags.Add("creative-managed");
        if(!record.Enabled)character.Tags.Add("creative-disabled");if(record.Category=="NPCs")character.Tags.Add("npc");if(record.Category=="Mercadores")character.Tags.Add("merchant");
        repository.Save(character);
    }
    public string Import(string category,string source,string displayName)
    {
        var extension=Path.GetExtension(source).ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))throw new ArgumentException("Escolha PNG, JPG ou WebP.");
        if(new FileInfo(source).Length>20*1024*1024)throw new ArgumentException("Use uma imagem de até 20 MB.");
        using var image=ImageManager.LoadImage(source);if(image==null||image.IsEmpty())throw new ArgumentException("Não foi possível abrir a imagem.");
        string id=Guid.NewGuid().ToString("N"),folder=ProjectSettings.GlobalizePath("user://Content/"+Slug(category));Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,id+".png");if(image.SavePng(target)!=Error.Ok)throw new IOException("Não foi possível copiar o PNG.");
        return target;
    }
    static List<ContentAssetRecord> BuiltIns()
    {
        var output=new List<ContentAssetRecord>();
        void Read(string category,string subfolder)
        {
            string root=ProjectSettings.GlobalizePath("res://Assets/ArtKit/"+subfolder);if(!Directory.Exists(root))return;
            foreach(var file in Directory.EnumerateFiles(root,"*.png",SearchOption.TopDirectoryOnly))
            {
                var name=Path.GetFileNameWithoutExtension(file);
                var actualCategory=category=="Personagens"&&name.Contains("merchant",StringComparison.OrdinalIgnoreCase)?"Mercadores":category;
                output.Add(new(){Id="builtin_"+subfolder.Replace('/','_')+"_"+name,DisplayName=name.Replace('_',' '),Category=actualCategory,Path="res://Assets/ArtKit/"+subfolder+"/"+Path.GetFileName(file),BuiltIn=true,CanBuildRelationship=actualCategory=="Personagens",Procedural=actualCategory!="Inimigos"||name.Contains("_chroma",StringComparison.OrdinalIgnoreCase)});
            }
        }
        Read("Cenários","Backgrounds");Read("Cenários","Interiors");Read("Personagens","Characters/NPCs");Read("Inimigos","Characters/Monsters");Read("Cartas","UI/Cards/Faces");
        return output;
    }
    static string Slug(string text)=>new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}

public partial class CreativeModeController : Control
{
    public Action? Closed;
    readonly ContentLibrary _library=new();
    List<ContentAssetRecord> _assets=new();
    OptionButton _category=null!;ItemList _list=null!;TextureRect _preview=null!;LineEdit _id=null!,_name=null!,_tags=null!,_biome=null!,_period=null!;TextEdit _description=null!;SpinBox _weight=null!,_health=null!,_damage=null!,_xp=null!,_coinMin=null!,_coinMax=null!;CheckButton _procedural=null!;Label _info=null!;
    SpinBox _age=null!;LineEdit _personality=null!,_speechStyle=null!;CheckButton _relationship=null!;
    ContentAssetRecord? _selected;string? _source;
    readonly string[] _categories={"Cenários","Personagens","NPCs","Inimigos","Mercadores","Cartas"};
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);var veil=new ColorRect {Color=new Color("03080bf0")};veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(veil);
        Ui.Panel(this,"CRIATIVO · BIBLIOTECA DE CONTEÚDO",out var body,()=>Closed?.Invoke());
        body.AddChild(Ui.Text("Cadastre imagens e metadados. Desativar remove do catálogo, sem apagar o arquivo original.",15));
        _category=new OptionButton();foreach(var category in _categories)_category.AddItem(category);body.AddChild(_category);_category.ItemSelected+=_=>Refresh();
        var row=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};row.AddThemeConstantOverride("separation",18);body.AddChild(row);
        _list=new ItemList {CustomMinimumSize=new Vector2(310,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(_list);_list.ItemSelected+=index=>Select((int)index);
        var editorScroll=new ScrollContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(editorScroll);
        var detail=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};editorScroll.AddChild(detail);
        _preview=new TextureRect {CustomMinimumSize=new Vector2(0,150),SizeFlagsVertical=SizeFlags.ExpandFill,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered};detail.AddChild(_preview);
        var form=new GridContainer {Columns=2};form.AddThemeConstantOverride("h_separation",10);form.AddThemeConstantOverride("v_separation",6);detail.AddChild(form);
        void Field(string label,Control input){form.AddChild(Ui.Text(label,13));input.SizeFlagsHorizontal=SizeFlags.ExpandFill;form.AddChild(input);}
        _id=new LineEdit {MaxLength=64,PlaceholderText="id_unico"};Field("ID",_id);
        _name=new LineEdit {MaxLength=48};Field("Nome",_name);
        _tags=new LineEdit {PlaceholderText="forest, night, merchant"};Field("Tags",_tags);
        _biome=new LineEdit {PlaceholderText="forest"};Field("Bioma",_biome);
        _period=new LineEdit {PlaceholderText="Any, Night..."};Field("Período",_period);
        _weight=new SpinBox {MinValue=.1,MaxValue=20,Step=.1,Value=1};Field("Peso procedural",_weight);
        _procedural=new CheckButton {Text="Pode aparecer na campanha",ButtonPressed=true};form.AddChild(new Control());form.AddChild(_procedural);
        _description=new TextEdit {CustomMinimumSize=new Vector2(0,60),PlaceholderText="Descrição usada em eventos"};Field("Descrição",_description);
        _age=new SpinBox {MinValue=18,MaxValue=99,Value=25};Field("Idade adulta",_age);
        _personality=new LineEdit {PlaceholderText="misterioso, gentil..."};Field("Personalidade",_personality);
        _speechStyle=new LineEdit {PlaceholderText="natural, formal..."};Field("Estilo de fala",_speechStyle);
        _relationship=new CheckButton {Text="Pode criar relacionamento"};form.AddChild(new Control());form.AddChild(_relationship);
        _health=new SpinBox {MinValue=1,MaxValue=999,Value=80};Field("Vida (inimigo)",_health);
        _damage=new SpinBox {MinValue=1,MaxValue=99,Value=12};Field("Dano",_damage);
        _xp=new SpinBox {MinValue=0,MaxValue=999,Value=24};Field("XP",_xp);
        _coinMin=new SpinBox {MinValue=0,MaxValue=999,Value=10};Field("Reais mín.",_coinMin);
        _coinMax=new SpinBox {MinValue=0,MaxValue=999,Value=22};Field("Reais máx.",_coinMax);
        _info=Ui.Text("Escolha um asset.",14);detail.AddChild(_info);
        var buttons=new HBoxContainer();detail.AddChild(buttons);buttons.AddChild(Ui.Button("Escolher imagem…",Choose));buttons.AddChild(Ui.Button("Salvar",Save));buttons.AddChild(Ui.Button("Duplicar",Duplicate));buttons.AddChild(Ui.Button("Ativar/Desativar",Toggle));buttons.AddChild(Ui.Button("Editor de cartas",OpenCardEditor));
        _assets=_library.Load();Refresh();
    }
    string Category=>_category.GetItemText(_category.Selected);
    void Refresh()
    {
        _list.Clear();foreach(var asset in _assets.Where(a=>a.Category==Category))_list.AddItem((asset.Enabled?"":"[inativo] ")+asset.DisplayName);_selected=null;_preview.Texture=null;_id.Editable=true;_id.Text="";_name.Text="";_tags.Text="";_description.Text="";_biome.Text="forest";_period.Text="Any";_weight.Value=1;_procedural.ButtonPressed=true;_age.Value=25;_personality.Text="misterioso e atento";_speechStyle.Text="natural";_relationship.ButtonPressed=Category=="Personagens";_health.Value=80;_damage.Value=12;_xp.Value=24;_coinMin.Value=10;_coinMax.Value=22;_info.Text="Escolha um asset ou importe uma imagem.";
    }
    void Select(int index)
    {
        _selected=_assets.Where(a=>a.Category==Category).ElementAt(index);_id.Text=_selected.Id;_id.Editable=!_selected.BuiltIn;_name.Text=_selected.DisplayName;_tags.Text=_selected.Tags;_description.Text=_selected.Description;_biome.Text=_selected.Biome;_period.Text=_selected.Period;_weight.Value=_selected.Weight;_procedural.ButtonPressed=_selected.Procedural;_age.Value=_selected.Age;_personality.Text=_selected.Personality;_speechStyle.Text=_selected.SpeechStyle;_relationship.ButtonPressed=_selected.CanBuildRelationship;_health.Value=_selected.Health;_damage.Value=_selected.Damage;_xp.Value=_selected.RewardXp;_coinMin.Value=_selected.CoinMin;_coinMax.Value=_selected.CoinMax;_source=null;_preview.Texture=LoadTexture(_selected.Path);_info.Text=$"Tipo: {_selected.Category}\nCaminho: {_selected.Path}";
    }
    void Choose()
    {
        var picker=new FileDialog {FileMode=FileDialog.FileModeEnum.OpenFile,Access=FileDialog.AccessEnum.Filesystem,Filters=new[]{"*.png,*.jpg,*.jpeg,*.webp ; Imagens"},UseNativeDialog=true};AddChild(picker);
        picker.FileSelected+=path=>{try{using var image=ImageManager.LoadImage(path);if(image==null||image.IsEmpty())throw new ArgumentException("Imagem inválida.");_source=path;_preview.Texture=ImageTexture.CreateFromImage(image);_info.Text="Prévia pronta. Salve para registrar na biblioteca.";}catch(Exception e){_info.Text=e.Message;}finally{picker.QueueFree();}};picker.Canceled+=picker.QueueFree;picker.PopupCenteredRatio(.7f);
    }
    void Save()
    {
        try
        {
            var record=_selected??new ContentAssetRecord {Category=Category};var wantedId=_id.Text.Trim();if(!record.BuiltIn){if(wantedId.Length<2||wantedId.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))throw new ArgumentException("Use um ID com letras, números, _ ou -.");if(_assets.Any(a=>a!=record&&a.Id.Equals(wantedId,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Este ID já existe.");record.Id=wantedId;}record.Category=Category;record.DisplayName=string.IsNullOrWhiteSpace(_name.Text)?"Sem nome":_name.Text.Trim();record.Tags=_tags.Text.Trim();record.Description=_description.Text.Trim();record.Biome=_biome.Text.Trim();record.Period=_period.Text.Trim();record.Weight=(float)_weight.Value;record.Procedural=_procedural.ButtonPressed;record.Age=(int)_age.Value;record.Personality=_personality.Text.Trim();record.SpeechStyle=_speechStyle.Text.Trim();record.Health=(int)_health.Value;record.Damage=(int)_damage.Value;record.RewardXp=(int)_xp.Value;record.CoinMin=(int)_coinMin.Value;record.CoinMax=Math.Max(record.CoinMin,(int)_coinMax.Value);record.CanBuildRelationship=Category=="Personagens"&&_relationship.ButtonPressed;record.Enabled=true;
            if(_source!=null)record.Path=_library.Import(Category,_source,record.DisplayName);
            if(string.IsNullOrWhiteSpace(record.Path))throw new ArgumentException("Escolha uma imagem antes de salvar.");
            if(_selected==null)_assets.Add(record);_library.SaveUser(_assets);_library.SyncCharacter(record);_info.Text="Salvo na biblioteca e integrado à campanha.";Refresh();
        }
        catch(Exception e){_info.Text=e.Message;}
    }
    void Duplicate(){if(_selected==null)return;var copy=JsonSerializer.Deserialize<ContentAssetRecord>(JsonSerializer.Serialize(_selected))!;copy.Id="custom_"+Guid.NewGuid().ToString("N")[..10];copy.DisplayName+=" (cópia)";copy.BuiltIn=false;_assets.Add(copy);_library.SaveUser(_assets);_library.SyncCharacter(copy);Refresh();}
    void Toggle(){if(_selected==null||_selected.BuiltIn){_info.Text="Assets incluídos no jogo permanecem ativos.";return;}_selected.Enabled=!_selected.Enabled;_library.SaveUser(_assets);_library.SyncCharacter(_selected);Refresh();}
    void OpenCardEditor(){if(Category!="Cartas"){_info.Text="Selecione a categoria Cartas para abrir o editor completo.";return;}var editor=new CardEditor();editor.Closed=editor.QueueFree;AddChild(editor);}
    static Texture2D? LoadTexture(string path)
    {
        return ContentLibrary.LoadTexture(path);
    }
}
