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
    public void SaveUser(List<ContentAssetRecord> all)
    {
        var user=all.Where(a=>!a.BuiltIn).ToList();var path=ProjectSettings.GlobalizePath(Registry);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(user,Json));File.Move(path+".tmp",path,true);
    }
    public string Import(string category,string source,string displayName)
    {
        if(!string.Equals(Path.GetExtension(source),".png",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Escolha um PNG.");
        if(new FileInfo(source).Length>20*1024*1024)throw new ArgumentException("Use um PNG de até 20 MB.");
        using var image=Image.LoadFromFile(source);if(image==null||image.IsEmpty())throw new ArgumentException("Não foi possível abrir a imagem.");
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
            foreach(var file in Directory.EnumerateFiles(root,"*.png",SearchOption.TopDirectoryOnly))output.Add(new(){Id="builtin_"+subfolder.Replace('/','_')+"_"+Path.GetFileNameWithoutExtension(file),DisplayName=Path.GetFileNameWithoutExtension(file).Replace('_',' '),Category=category,Path="res://Assets/ArtKit/"+subfolder+"/"+Path.GetFileName(file),BuiltIn=true});
        }
        Read("Cenários","Backgrounds");Read("Cenários","Interiors");Read("Personagens","Characters/NPCs");Read("Inimigos","Characters/Monsters");
        return output;
    }
    static string Slug(string text)=>new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}

public partial class CreativeModeController : Control
{
    public Action? Closed;
    readonly ContentLibrary _library=new();
    List<ContentAssetRecord> _assets=new();
    OptionButton _category=null!;ItemList _list=null!;TextureRect _preview=null!;LineEdit _name=null!,_tags=null!;Label _info=null!;
    ContentAssetRecord? _selected;string? _source;
    readonly string[] _categories={"Cenários","Personagens","NPCs","Inimigos","Mercadores"};
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);var veil=new ColorRect {Color=new Color("03080bf0")};veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(veil);
        Ui.Panel(this,"CRIATIVO · BIBLIOTECA DE CONTEÚDO",out var body,()=>Closed?.Invoke());
        body.AddChild(Ui.Text("Cadastre imagens e metadados. Desativar remove do catálogo, sem apagar o arquivo original.",15));
        _category=new OptionButton();foreach(var category in _categories)_category.AddItem(category);body.AddChild(_category);_category.ItemSelected+=_=>Refresh();
        var row=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};row.AddThemeConstantOverride("separation",18);body.AddChild(row);
        _list=new ItemList {CustomMinimumSize=new Vector2(310,0),SizeFlagsVertical=SizeFlags.ExpandFill};row.AddChild(_list);_list.ItemSelected+=index=>Select((int)index);
        var detail=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(detail);
        _preview=new TextureRect {CustomMinimumSize=new Vector2(0,220),SizeFlagsVertical=SizeFlags.ExpandFill,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered};detail.AddChild(_preview);
        detail.AddChild(Ui.Text("Nome",14));_name=new LineEdit {MaxLength=48};detail.AddChild(_name);
        detail.AddChild(Ui.Text("Tags",14));_tags=new LineEdit {PlaceholderText="ex.: forest, night, merchant"};detail.AddChild(_tags);
        _info=Ui.Text("Escolha um asset.",14);detail.AddChild(_info);
        var buttons=new HBoxContainer();detail.AddChild(buttons);buttons.AddChild(Ui.Button("Escolher PNG…",Choose));buttons.AddChild(Ui.Button("Salvar metadata",Save));buttons.AddChild(Ui.Button("Desativar",Disable));
        _assets=_library.Load();Refresh();
    }
    string Category=>_category.GetItemText(_category.Selected);
    void Refresh()
    {
        _list.Clear();foreach(var asset in _assets.Where(a=>a.Category==Category))_list.AddItem((asset.Enabled?"":"[inativo] ")+asset.DisplayName);_selected=null;_preview.Texture=null;_name.Text="";_tags.Text="";_info.Text="Escolha um asset ou importe um PNG.";
    }
    void Select(int index)
    {
        _selected=_assets.Where(a=>a.Category==Category).ElementAt(index);_name.Text=_selected.DisplayName;_tags.Text=_selected.Tags;_source=null;_preview.Texture=LoadTexture(_selected.Path);_info.Text=$"ID: {_selected.Id}\nTipo: {_selected.Category}\nCaminho: {_selected.Path}";
    }
    void Choose()
    {
        var picker=new FileDialog {FileMode=FileDialog.FileModeEnum.OpenFile,Access=FileDialog.AccessEnum.Filesystem,Filters=new[]{"*.png ; Imagem PNG"},UseNativeDialog=true};AddChild(picker);
        picker.FileSelected+=path=>{try{using var image=Image.LoadFromFile(path);if(image==null||image.IsEmpty())throw new ArgumentException("Imagem inválida.");_source=path;_preview.Texture=ImageTexture.CreateFromImage(image);_info.Text="Prévia pronta. Salve para registrar na biblioteca.";}catch(Exception e){_info.Text=e.Message;}finally{picker.QueueFree();}};picker.Canceled+=picker.QueueFree;picker.PopupCenteredRatio(.7f);
    }
    void Save()
    {
        try
        {
            var record=_selected??new ContentAssetRecord {Category=Category};record.Category=Category;record.DisplayName=string.IsNullOrWhiteSpace(_name.Text)?"Sem nome":_name.Text.Trim();record.Tags=_tags.Text.Trim();record.Enabled=true;
            if(_source!=null)record.Path=_library.Import(Category,_source,record.DisplayName);
            if(string.IsNullOrWhiteSpace(record.Path))throw new ArgumentException("Escolha um PNG antes de salvar.");
            if(_selected==null)_assets.Add(record);_library.SaveUser(_assets);_info.Text="Salvo na biblioteca.";Refresh();
        }
        catch(Exception e){_info.Text=e.Message;}
    }
    void Disable(){if(_selected==null||_selected.BuiltIn){_info.Text="Assets incluídos no jogo não são removidos; importe e use sua própria cópia para desativá-la.";return;}_selected.Enabled=false;_library.SaveUser(_assets);Refresh();}
    static Texture2D? LoadTexture(string path)
    {
        if(path.StartsWith("res://"))return ChromaArt.LoadArt(path);
        if(!File.Exists(path))return null;using var image=Image.LoadFromFile(path);return image==null||image.IsEmpty()?null:ImageTexture.CreateFromImage(image);
    }
}
