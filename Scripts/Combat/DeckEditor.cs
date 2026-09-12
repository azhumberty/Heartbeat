using Godot;
namespace Heartbeat;

public partial class DeckEditor : Control
{
    public GameSave Game {get;set;}=new();
    public Action? Closed;
    public Action? Saved;
    public Dictionary<string,CardDefinition>? CatalogOverride {get;set;}
    Dictionary<string,CardDefinition> _catalog=new();
    PlayerDeck _draft=new();
    ScrollContainer _collectionScroll=null!;
    GridContainer _collectionGrid=null!;
    ItemList _deck=null!;
    VBoxContainer _preview=null!;
    Label _status=null!,_count=null!;
    LineEdit _search=null!;
    OptionButton _category=null!,_cost=null!,_rarity=null!,_person=null!,_companion=null!;
    List<string> _visible=new(),_equipped=new(),_companions=new();
    string _selected="";
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _catalog=CatalogOverride??new CardRepository().Catalog();DeckManager.SyncUnlocks(Game,_catalog);_draft=CardRules.Copy(Game.Deck);
        CardUi.Screen(this,"BARALHO · vínculos e estratégia",out var body,()=>Closed?.Invoke());
        var filters=new HBoxContainer();body.AddChild(filters);
        var column=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};filters.AddChild(column);
        column.AddChild(Ui.Text("Buscar",13));_search=new LineEdit {PlaceholderText="Nome da carta"};column.AddChild(_search);
        OptionButton Filter(string label,string[] values){var col=new VBoxContainer();filters.AddChild(col);return CardUi.Options(col,label,values);}
        _category=Filter("Categoria",new[]{"Todas"}.Concat(Enum.GetValues<CardCategory>().Select(CardRules.CategoryName)).ToArray());
        _cost=Filter("Mana",new[]{"Todos","0–15","16–30","31–60"});
        _rarity=Filter("Raridade",new[]{"Todas","Comum","Rara","Épica"});
        _person=Filter("Origem",new[]{"Todas","Genéricas","Customizadas","Personagens"});
        foreach(var b in new[]{_category,_cost,_rarity,_person})b.ItemSelected+=_=>Refresh();
        _search.TextChanged+=_=>Refresh();
        var layout=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};layout.AddThemeConstantOverride("separation",16);body.AddChild(layout);
        var left=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};layout.AddChild(left);left.AddChild(Ui.Text("COLEÇÃO",16));
        _collectionScroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(550,200),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        _collectionGrid=new GridContainer {Columns=3};_collectionGrid.AddThemeConstantOverride("h_separation",12);_collectionGrid.AddThemeConstantOverride("v_separation",12);
        _collectionScroll.AddChild(_collectionGrid);left.AddChild(_collectionScroll);
        left.AddChild(Ui.Button("Adicionar ao baralho",()=>{if(DeckManager.Add(_draft,_selected,_catalog))Refresh();else _status.Text="Limite atingido ou carta não disponível."; }));
        var middle=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};layout.AddChild(middle);_count=Ui.Text("",16);middle.AddChild(_count);
        _deck=new ItemList {SizeFlagsVertical=SizeFlags.ExpandFill,CustomMinimumSize=new(270,200)};middle.AddChild(_deck);_deck.ItemSelected+=i=>Select(_equipped[(int)i]);
        middle.AddChild(Ui.Button("Remover uma cópia",()=>{DeckManager.Remove(_draft,_selected);Refresh();}));
        var scroll=new ScrollContainer {CustomMinimumSize=new(270,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};layout.AddChild(scroll);
        _preview=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_preview);
        var bottom=new HBoxContainer();body.AddChild(bottom);
        _companion=new OptionButton {SizeFlagsHorizontal=SizeFlags.ExpandFill};bottom.AddChild(_companion);
        _companions.Add("");_companion.AddItem("Sem companheiro");
        foreach(var c in _catalog.Values.Where(c=>c.Category==CardCategory.Companion&&_draft.Owned.GetValueOrDefault(c.Id)>0)){_companions.Add(c.Id);_companion.AddItem(c.Name);}
        _companion.Select(Math.Max(0,_companions.IndexOf(_draft.CompanionId)));
        _companion.ItemSelected+=i=>{_draft.CompanionId=_companions[(int)i];if((int)i>0)Select(_draft.CompanionId);};
        var copies=new SpinBox {MinValue=1,MaxValue=5,Value=_draft.MaxCopies,TooltipText="Máximo de cópias por carta"};bottom.AddChild(Ui.Text("Cópias",14));bottom.AddChild(copies);copies.ValueChanged+=v=>{_draft.MaxCopies=(int)v;Refresh();};
        bottom.AddChild(Ui.Button("Restaurar inicial",()=>{DeckManager.Restore(_draft);Refresh();}));
        bottom.AddChild(Ui.Button("Salvar baralho",()=>SaveDraft()));
        _status=Ui.Text("",14);body.AddChild(_status);Refresh();Select(_catalog.Values.First().Id);
    }
    public bool SaveDraft()
    {
        string error=DeckManager.Validate(_draft,_catalog);
        if(error.Length>0){_status.Text=error;return false;}
        Game.Deck=CardRules.Copy(_draft);Saved?.Invoke();_status.Text="Baralho salvo.";return true;
    }
    void Select(string id)
    {
        _selected=id;CardUi.Clear(_preview);
        if(_catalog.TryGetValue(id,out var c))
        {
            _preview.AddChild(new CardView {Card=DeckManager.Upgraded(c,_draft.Upgrades.GetValueOrDefault(id))});
            if(c.CanUpgrade)_preview.AddChild(Ui.Text("Aprimoramento: "+_draft.Upgrades.GetValueOrDefault(id)+"/2",13));
        }
        else _preview.AddChild(Ui.Text("Carta ausente. Remova esta entrada.",15));
    }
    void Refresh()
    {
        _visible.Clear();CardUi.Clear(_collectionGrid);_equipped=_draft.Cards.Distinct().ToList();_deck.Clear();
        foreach(var c in _catalog.Values.OrderBy(c=>c.Category).ThenBy(c=>c.Cost))
        {
            if(c.Category==CardCategory.Companion||_draft.Owned.GetValueOrDefault(c.Id)<1)continue;
            if(!c.Name.Contains(_search.Text,StringComparison.OrdinalIgnoreCase))continue;
            if(_category.Selected>0&&CardRules.CategoryName(c.Category)!=CardUi.Selected(_category))continue;
            if(_cost.Selected==1&&c.Cost>15||_cost.Selected==2&&(c.Cost<16||c.Cost>30)||_cost.Selected==3&&c.Cost<31)continue;
            if(_rarity.Selected>0&&c.Rarity!=CardUi.Selected(_rarity))continue;
            if(_person.Selected==1&&!c.Id.StartsWith("g")||_person.Selected==2&&!c.Id.StartsWith("custom_")||_person.Selected==3&&c.CharacterId.Length==0)continue;
            
            _visible.Add(c.Id);
            
            // Create miniature CardView for the grid
            var cardNode = new CardView { Card = c, Compact = true };
            // Let's scale them down a bit for the grid
            cardNode.CustomMinimumSize = new Vector2(120, 180);
            
            // Container to handle click and layout
            var wrapper = new MarginContainer { CustomMinimumSize = new Vector2(130, 200) };
            var btn = new Button { Flat = true }; btn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            string idCopy = c.Id;
            btn.Pressed += () => Select(idCopy);
            
            var quantity = Ui.Text($"x{_draft.Owned[c.Id]}", 16);
            quantity.AddThemeColorOverride("font_color", new Color("f4d160"));
            quantity.AddThemeColorOverride("font_shadow_color", Colors.Black);
            quantity.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            quantity.Position = new Vector2(90, 5); // position top right

            wrapper.AddChild(cardNode);
            wrapper.AddChild(btn);
            wrapper.AddChild(quantity);
            _collectionGrid.AddChild(wrapper);
        }
        foreach(var id in _equipped)_deck.AddItem($"{_draft.Cards.Count(x=>x==id)}×  {(_catalog.TryGetValue(id,out var c)?c.Name:"[ausente] "+id)}");
        _count.Text=$"SEU BARALHO · {_draft.Cards.Count}/30";
        _status.Text=DeckManager.Validate(_draft,_catalog);if(_status.Text=="")_status.Text="Baralho válido · 12–30 cartas · alterações só são gravadas ao salvar.";
    }
}
