namespace Heartbeat;

public sealed record EnemyDefinition(
    string Id,
    string Name,
    int Health,
    int Damage,
    int RewardXp,
    string Color,
    int CoinMin,
    int CoinMax,
    int LootTier)
{
    public static readonly EnemyDefinition[] All = {
        new("forest", "Guardião de raízes", 72, 14, 24, "64856b", 12, 22, 1),
        new("camp", "Saqueador da fogueira", 90, 16, 30, "ac684d", 16, 28, 2),
        // Night HP strongly increased; ruin slightly tougher
        new("night", "Predador do breu", 168, 20, 42, "725584", 24, 40, 3),
        new("ruin", "Vigia esquecido", 135, 18, 52, "a58c65", 32, 52, 4),
        new("minotaur", "Minotauro da muralha", 155, 22, 48, "6b4a32", 28, 46, 3)
    };

    public static EnemyDefinition Get(string id)
    {
        if(new ContentLibrary().Find(id) is {Category:"Inimigos"} custom)
            return new(custom.Id,custom.DisplayName,Math.Clamp(custom.Health,1,999),Math.Clamp(custom.Damage,1,99),Math.Clamp(custom.RewardXp,0,999),"765478",Math.Clamp(custom.CoinMin,0,999),Math.Clamp(custom.CoinMax,Math.Clamp(custom.CoinMin,0,999),999),Math.Clamp((custom.Health+custom.Damage)/55,1,4));
        return All.FirstOrDefault(e => e.Id == id) ?? All[0];
    }
}

public enum IntentKind { Attack, Shield, Heal, Sleep, Potion }

public sealed record IntentAction(IntentKind Kind, int Value, string Icon, string Label);

public sealed partial class CombatManager
{

    public CombatState State { get; }
    readonly GameSave _game;
    List<IntentAction> _planned = new();
    public CombatManager(GameSave game, CombatState state) { _game = game; State = state; }

    public static CombatManager Start(GameSave game, Dictionary<string, CardDefinition> catalog, string encounter, string enemy, string arena, float hour, EnemyRole role = EnemyRole.Normal)
    {
        DeckManager.SyncUnlocks(game, catalog);
        string problem = DeckManager.Validate(game.Deck, catalog);
        if (problem.Length > 0) throw new ArgumentException(problem);
        var foe = EnemyDefinition.Get(enemy);
        int hp = Math.Max(1, (int)(foe.Health * EnemyDecks.HealthScale(role)));
        var state = new CombatState
        {
            EncounterId = encounter,
            EnemyId = enemy,
            Arena = arena,
            Hour = hour,
            Seed = Random.Shared.Next(),
            RewardXp = foe.RewardXp + (role==EnemyRole.Elite?12:role==EnemyRole.Boss?24:0),
            Player = new() { Health = Math.Max(1, game.Player.Health), MaxHealth = game.Player.MaxHealth },
            Enemy = new() { Health = hp, MaxHealth = hp },
            Mana = game.Player.Mana,
            MaxMana = game.Player.MaxMana,
            EnemyRole = role.ToString(),
            EnemyMaxMana = EnemyDecks.MaxMana(foe, role),
            ReturnX = game.PlayerX,
            ReturnZ = game.PlayerZ,
            ReturnYaw = game.PlayerRotationY,
            ClockWasPaused = game.Settings.WorldTimePaused
        };
        state.EnemyMana = state.EnemyMaxMana / 2;
        foreach (var id in game.Deck.Cards.Append(game.Deck.CompanionId).Where(x => x.Length > 0).Distinct())
            state.Cards[id] = DeckManager.Upgraded(catalog[id], game.Deck.Upgrades.GetValueOrDefault(id));
        state.DrawPile = game.Deck.Cards.ToList();
        game.ActiveCombat = state;
        var manager = new CombatManager(game, state);
        manager.Shuffle();
        manager.BeginTurn();
        if (game.Deck.CompanionId.Length > 0) state.Hand.Add(game.Deck.CompanionId);
        return manager;
    }

    /// <summary>Legacy single-line intent (UI may still read this).</summary>
    public string Intent => string.Join(" · ", Intents.Select(i => i.Label));

    /// <summary>Stacked intent icons for the arena UI (⚔️🛡️💤🧪).</summary>
    public IReadOnlyList<IntentAction> Intents
    {
        get
        {
            if (_planned.Count == 0) PlanIntents();
            return _planned;
        }
    }

    public int EnemyDamage => EnemyDefinition.Get(State.EnemyId).Damage + Math.Min(8, State.Turn / 3);

    void PlanIntents()
    {
        var foe = EnemyDefinition.Get(State.EnemyId);
        _planned = EnemyAi.Plan(State, foe, EnemyDecks.Build(foe, Enum.TryParse<EnemyRole>(State.EnemyRole, out var role) ? role : EnemyRole.Normal));
    }

}
