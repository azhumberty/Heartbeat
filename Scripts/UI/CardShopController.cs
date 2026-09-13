using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Heartbeat;

public partial class CardShopController : Control
{
    public GameSave Game { get; set; } = new();
    public string MerchantId {get;set;}="merchant";
    public Action? Closed;

    Label _coinsLabel = null!;
    HBoxContainer _resultsContainer = null!;
    VBoxContainer _shopOptions = null!;

    readonly CardRepository _repo = new();

    // Costs tuned so ~2 forest wins ≈ pacote básico; 1 ruin/night ≈ épico.
    const int BasicPackCost = 35;
    const int EpicPackCost = 70;
    const int PotionPackCost = 45;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var merchant=new ContentLibrary().Find(MerchantId);
        CardUi.Screen(this, (merchant?.DisplayName??"MERCADOR DE CARTAS").ToUpperInvariant()+" · gaste seus reais", out var body, () => Closed?.Invoke());

        var header = new HBoxContainer();
        body.AddChild(header);

        // Autowrap + ExpandFill spacer was crushing "BEM-VINDO..." vertically.
        var welcome = Ui.Text("BEM-VINDO AO BAZAR DAS MEMÓRIAS", 18);
        welcome.AutowrapMode = TextServer.AutowrapMode.Off;
        welcome.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        welcome.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(welcome);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        _coinsLabel = Ui.Text($"💰 {Game.Player.Coins} Reais", 20);
        _coinsLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _coinsLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _coinsLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _coinsLabel.AddThemeColorOverride("font_color", new Color("f4d160"));
        header.AddChild(_coinsLabel);

        _shopOptions = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };

        var catalog=_repo.Catalog();var offers=MerchantStock.Parse(merchant?.ShopStock);
        foreach(var offer in offers)
            if(catalog.TryGetValue(offer.CardId,out var card)&&card.CharacterId.Length==0)
                _shopOptions.AddChild(Ui.Button($"{card.Name} · {offer.Price} Reais",()=>BuyCard(card,offer.Price)));
        if(_shopOptions.GetChildCount()==0)
        {
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote Básico ({BasicPackCost} Reais) - 3 Cartas", () => BuyPack(BasicPackCost, 3, false, false)));
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote de Poções ({PotionPackCost} Reais) - 2 Curas", () => BuyPack(PotionPackCost, 2, false, true)));
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote Épico ({EpicPackCost} Reais) - 2 Cartas Raras+", () => BuyPack(EpicPackCost, 2, true, false)));
        }

        body.AddChild(_shopOptions);

        var resultsScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 300) };
        body.AddChild(resultsScroll);

        _resultsContainer = new HBoxContainer();
        _resultsContainer.AddThemeConstantOverride("separation", 16);
        resultsScroll.AddChild(_resultsContainer);
    }

    void BuyCard(CardDefinition card,int cost)
    {
        if(!EconomyService.TrySpend(Game,cost)){ShowMessage("Você não tem reais suficientes!");return;}
        Game.Deck.Owned[card.Id]=Game.Deck.Owned.GetValueOrDefault(card.Id)+1;_coinsLabel.Text=$"💰 {Game.Player.Coins} Reais";CardUi.Clear(_resultsContainer);
        var wrapper=new VBoxContainer();wrapper.AddChild(Ui.Text("ADQUIRIDA!",16));wrapper.AddChild(new CardView{Card=card,Compact=false});_resultsContainer.AddChild(wrapper);
    }
    void ShowMessage(string text){CardUi.Clear(_resultsContainer);_resultsContainer.AddChild(Ui.Text(text,16));}

    void BuyPack(int cost, int count, bool forceRare, bool potionsOnly)
    {
        if (!EconomyService.TrySpend(Game, cost))
        {
            CardUi.Clear(_resultsContainer);
            _resultsContainer.AddChild(Ui.Text("Você não tem reais suficientes!", 16));
            return;
        }

        _coinsLabel.Text = $"💰 {Game.Player.Coins} Reais";

        CardUi.Clear(_resultsContainer);
        var rng = new Random();

        var pool = _repo.Generic.ToList();

        for (int i = 0; i < count; i++)
        {
            List<CardDefinition> validPool;
            if (potionsOnly)
                validPool = pool.Where(c => c.Category == CardCategory.Heal).ToList();
            else if (forceRare)
                validPool = pool.Where(c => c.Rarity == "Rara" || c.Rarity == "Épica").ToList();
            else
            {
                validPool = rng.NextDouble() < 0.18
                    ? pool.Where(c => c.Rarity == "Rara" || c.Rarity == "Épica").ToList()
                    : pool.Where(c => c.Rarity == "Comum" || string.IsNullOrEmpty(c.Rarity)).ToList();
            }
            if (validPool.Count == 0) validPool = pool;

            var card = validPool[rng.Next(validPool.Count)];

            Game.Deck.Owned[card.Id] = Game.Deck.Owned.GetValueOrDefault(card.Id) + 1;

            var wrapper = new VBoxContainer();
            wrapper.AddChild(Ui.Text("NOVA!", 16));
            var cardView = new CardView { Card = card, Compact = false };
            wrapper.AddChild(cardView);

            _resultsContainer.AddChild(wrapper);
        }
    }
}

public readonly record struct MerchantOffer(string CardId,int Price);
public static class MerchantStock
{
    public static List<MerchantOffer> Parse(string? source)
    {
        var result=new List<MerchantOffer>();
        foreach(var entry in (source??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var pair=entry.Split(':',2,StringSplitOptions.TrimEntries);
            if(pair.Length!=2||string.IsNullOrWhiteSpace(pair[0])||!int.TryParse(pair[1],out var price)||price<1||price>999)continue;
            if(result.All(item=>!item.CardId.Equals(pair[0],StringComparison.OrdinalIgnoreCase)))result.Add(new MerchantOffer(pair[0],price));
        }
        return result.Take(24).ToList();
    }
    public static string Normalize(string? source)
    {
        if(string.IsNullOrWhiteSpace(source))return "";var parsed=Parse(source);
        if(parsed.Count==0)throw new ArgumentException("Use o estoque no formato Carta:preço, por exemplo g01:12.");
        return string.Join(", ",parsed.Select(item=>$"{item.CardId}:{item.Price}"));
    }
}
