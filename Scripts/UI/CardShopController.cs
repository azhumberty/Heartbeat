using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Heartbeat;

public partial class CardShopController : Control
{
    public GameSave Game { get; set; } = new();
    public string MerchantId { get; set; } = "merchant";
    public Action? Closed;

    Label _coinsLabel = null!;
    VBoxContainer _resultsContainer = null!;
    VBoxContainer _shopOptions = null!;
    bool _closing;

    readonly CardRepository _repo = new();

    const int BasicPackCost = 35;
    const int EpicPackCost = 70;
    const int PotionPackCost = 45;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var merchant = new ContentLibrary().Find(MerchantId);
        var title = (merchant?.DisplayName ?? "MERCADOR DE CARTAS").ToUpperInvariant() + " - gaste seus reais";
        CardUi.Screen(this, title, out var body, CloseShop);

        var header = new HBoxContainer();
        body.AddChild(header);

        var welcome = Ui.Text("BEM-VINDO AO BAZAR DAS MEMORIAS", 18);
        welcome.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        welcome.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(welcome);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(spacer);

        _coinsLabel = Ui.Text($"{Game.Player.Coins} Reais", 20);
        _coinsLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _coinsLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _coinsLabel.AddThemeColorOverride("font_color", new Color("f4d160"));
        header.AddChild(_coinsLabel);

        _shopOptions = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };

        var catalog = _repo.Catalog();
        var offers = MerchantStock.Parse(merchant?.ShopStock);
        foreach (var offer in offers)
            if (catalog.TryGetValue(offer.CardId, out var card) && card.CharacterId.Length == 0)
                _shopOptions.AddChild(Ui.Button($"{card.Name} - {offer.Price} Reais", () => BuyCard(card, offer.Price)));
        if (_shopOptions.GetChildCount() == 0)
        {
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote Basico ({BasicPackCost} Reais) - 3 Cartas", () => BuyPack(BasicPackCost, 3, false, false)));
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote de Pocoes ({PotionPackCost} Reais) - 2 Curas", () => BuyPack(PotionPackCost, 2, false, true)));
            _shopOptions.AddChild(Ui.Button($"Comprar Pacote Epico ({EpicPackCost} Reais) - 2 Cartas Raras+", () => BuyPack(EpicPackCost, 2, true, false)));
        }

        body.AddChild(_shopOptions);

        var resultsScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 300)
        };
        body.AddChild(resultsScroll);

        _resultsContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resultsContainer.AddThemeConstantOverride("separation", 16);
        resultsScroll.AddChild(_resultsContainer);
        Ui.FitScrollChild(resultsScroll, _resultsContainer);
    }

    public void CloseShop()
    {
        if (_closing) return;
        _closing = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var callback = Closed;
        Closed = null;
        callback?.Invoke();
        if (GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion()) QueueFree();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            CloseShop();
        }
    }

    void BuyCard(CardDefinition card, int cost)
    {
        if (!EconomyService.TrySpend(Game, cost))
        {
            ShowMessage("Voce nao tem reais suficientes!");
            return;
        }
        Game.Deck.Owned[card.Id] = Game.Deck.Owned.GetValueOrDefault(card.Id) + 1;
        _coinsLabel.Text = $"{Game.Player.Coins} Reais";
        CardUi.Clear(_resultsContainer);
        var cards = new HBoxContainer();
        var wrapper = new VBoxContainer();
        wrapper.AddChild(Ui.Text("ADQUIRIDA!", 16));
        wrapper.AddChild(new CardView { Card = card, Compact = false });
        cards.AddChild(wrapper);
        _resultsContainer.AddChild(cards);
    }

    void ShowMessage(string text)
    {
        CardUi.Clear(_resultsContainer);
        var message = Ui.Body(text, 16);
        message.CustomMinimumSize = new Vector2(500, 0);
        _resultsContainer.AddChild(message);
    }

    static bool IsRareOrEpic(CardDefinition card) =>
        card.Rarity is "Rara" or "Epica" or "\u00c9pica";

    void BuyPack(int cost, int count, bool forceRare, bool potionsOnly)
    {
        if (!EconomyService.TrySpend(Game, cost))
        {
            ShowMessage("Voce nao tem reais suficientes!");
            return;
        }

        _coinsLabel.Text = $"{Game.Player.Coins} Reais";
        CardUi.Clear(_resultsContainer);
        var rng = new Random();
        var pool = _repo.Generic.ToList();
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 16);
        _resultsContainer.AddChild(cards);
        for (int i = 0; i < count; i++)
        {
            List<CardDefinition> validPool;
            if (potionsOnly)
                validPool = pool.Where(c => c.Category == CardCategory.Heal).ToList();
            else if (forceRare)
                validPool = pool.Where(IsRareOrEpic).ToList();
            else
            {
                validPool = rng.NextDouble() < 0.18
                    ? pool.Where(IsRareOrEpic).ToList()
                    : pool.Where(c => c.Rarity == "Comum" || string.IsNullOrEmpty(c.Rarity)).ToList();
            }
            if (validPool.Count == 0) validPool = pool;

            var card = validPool[rng.Next(validPool.Count)];
            Game.Deck.Owned[card.Id] = Game.Deck.Owned.GetValueOrDefault(card.Id) + 1;

            var wrapper = new VBoxContainer();
            wrapper.AddChild(Ui.Text("NOVA!", 16));
            wrapper.AddChild(new CardView { Card = card, Compact = false });
            cards.AddChild(wrapper);
        }
    }
}

public readonly record struct MerchantOffer(string CardId, int Price);
public static class MerchantStock
{
    public static List<MerchantOffer> Parse(string? source)
    {
        var result = new List<MerchantOffer>();
        foreach (var entry in (source ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || !int.TryParse(pair[1], out var price) || price < 1 || price > 999) continue;
            if (result.All(item => !item.CardId.Equals(pair[0], StringComparison.OrdinalIgnoreCase))) result.Add(new MerchantOffer(pair[0], price));
        }
        return result.Take(24).ToList();
    }

    public static string Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        var parsed = Parse(source);
        if (parsed.Count == 0) throw new ArgumentException("Use o estoque no formato Carta:preco, por exemplo g01:12.");
        return string.Join(", ", parsed.Select(item => $"{item.CardId}:{item.Price}"));
    }
}
