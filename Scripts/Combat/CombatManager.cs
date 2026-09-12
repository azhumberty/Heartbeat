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
        new("camp", "Saqueador da fogueira", 84, 16, 30, "ac684d", 16, 28, 2),
        new("night", "Predador do breu", 96, 18, 38, "725584", 24, 40, 3),
        new("ruin", "Vigia esquecido", 115, 17, 48, "a58c65", 32, 52, 4)
    };

    public static EnemyDefinition Get(string id) => All.FirstOrDefault(e => e.Id == id) ?? All[0];
}

public sealed class CombatManager
{
    public CombatState State { get; }
    readonly GameSave _game;
    public CombatManager(GameSave game, CombatState state) { _game = game; State = state; }

    public static CombatManager Start(GameSave game, Dictionary<string, CardDefinition> catalog, string encounter, string enemy, string arena, float hour)
    {
        DeckManager.SyncUnlocks(game, catalog);
        string problem = DeckManager.Validate(game.Deck, catalog);
        if (problem.Length > 0) throw new ArgumentException(problem);
        var foe = EnemyDefinition.Get(enemy);
        var state = new CombatState
        {
            EncounterId = encounter,
            EnemyId = enemy,
            Arena = arena,
            Hour = hour,
            Seed = Random.Shared.Next(),
            RewardXp = foe.RewardXp,
            Player = new() { Health = Math.Max(1, game.Player.Health), MaxHealth = game.Player.MaxHealth },
            Enemy = new() { Health = foe.Health, MaxHealth = foe.Health },
            Mana = game.Player.Mana,
            MaxMana = game.Player.MaxMana,
            ReturnX = game.PlayerX,
            ReturnZ = game.PlayerZ,
            ReturnYaw = game.PlayerRotationY,
            ClockWasPaused = game.Settings.WorldTimePaused
        };
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

    public string Intent => State.Enemy.Status.GetValueOrDefault(EffectKind.Stunned) > 0 ? "Atordoado · não agirá" :
        State.Turn % 3 == 0 ? "Preparar guarda · 12 de escudo" : $"Atacar · {EnemyDamage} de dano";
    public int EnemyDamage => EnemyDefinition.Get(State.EnemyId).Damage + Math.Min(8, State.Turn / 3);

    public string CanPlay(int index)
    {
        if (State.Result.Length > 0) return "Batalha encerrada.";
        if (State.Player.Status.GetValueOrDefault(EffectKind.Stunned) > 0) return "Você está atordoado. Encerre o turno.";
        if (index < 0 || index >= State.Hand.Count) return "Escolha uma carta.";
        var c = State.Cards[State.Hand[index]];
        if (State.Mana < c.Cost) return "Mana insuficiente.";
        if (c.Category == CardCategory.Companion && State.Summoned.Contains(c.Id)) return "Este companheiro já ajudou nesta batalha.";
        if (c.Condition == "Inimigo ferido" && State.Enemy.Health > State.Enemy.MaxHealth / 2) return "Requer inimigo com metade da Vida ou menos.";
        return "";
    }

    public string Play(int index)
    {
        string error = CanPlay(index);
        if (error.Length > 0) return error;
        string id = State.Hand[index];
        var c = State.Cards[id];
        State.Mana -= c.Cost;
        State.Hand.RemoveAt(index);
        if (c.Category == CardCategory.Companion)
        {
            State.Summoned.Add(c.Id);
            if (c.Passive != null) State.Passives.Add(CardRules.Copy(c.Passive));
        }
        else State.Discard.Add(id);
        foreach (var e in c.Effects) Apply(e);
        Resolve();
        return "";
    }

    public void EndTurn()
    {
        if (State.Result.Length > 0) return;
        var retained = State.Hand.Where(id => State.Cards[id].Category == CardCategory.Companion).ToList();
        State.Discard.AddRange(State.Hand.Where(id => State.Cards[id].Category != CardCategory.Companion));
        State.Hand = retained;
        Tick(State.Enemy);
        Resolve();
        if (State.Result.Length > 0) return;
        State.Enemy.Shield = 0;
        if (State.Enemy.Status.GetValueOrDefault(EffectKind.Stunned) == 0)
        {
            if (State.Turn % 3 == 0) State.Enemy.Shield = 12;
            else Hurt(State.Player, EnemyDamage, State.Enemy);
        }
        Decay(State.Enemy);
        Resolve();
        if (State.Result.Length == 0) BeginTurn();
    }

    void BeginTurn()
    {
        State.Turn++;
        State.Player.Shield = 0;
        Tick(State.Player);
        Resolve();
        if (State.Result.Length > 0) return;
        State.Mana = Math.Clamp(State.Mana + 30, 0, State.MaxMana);
        foreach (var e in State.Passives) Apply(e);
        Draw(5);
        Decay(State.Player);
    }

    public void Draw(int count)
    {
        for (int i = 0; i < Math.Clamp(count, 0, 10) && State.Hand.Count < 10; i++)
        {
            if (State.DrawPile.Count == 0) { State.DrawPile.AddRange(State.Discard); State.Discard.Clear(); Shuffle(); }
            if (State.DrawPile.Count == 0) return;
            string id = State.DrawPile[^1];
            State.DrawPile.RemoveAt(State.DrawPile.Count - 1);
            State.Hand.Add(id);
        }
    }

    void Shuffle()
    {
        var rng = new Random(unchecked(State.Seed + State.ShuffleCount++ * 7919));
        for (int i = State.DrawPile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (State.DrawPile[i], State.DrawPile[j]) = (State.DrawPile[j], State.DrawPile[i]);
        }
    }

    void Apply(CardEffect e)
    {
        var target = e.Target == CardTarget.Self ? State.Player : State.Enemy;
        switch (e.Kind)
        {
            case EffectKind.Damage: Hurt(target, e.Value, State.Player); break;
            case EffectKind.Shield: target.Shield = Math.Clamp(target.Shield + e.Value, 0, 100); break;
            case EffectKind.Heal: target.Health = Math.Min(target.MaxHealth, target.Health + e.Value); break;
            case EffectKind.Mana: State.Mana = Math.Clamp(State.Mana + e.Value, 0, State.MaxMana); break;
            case EffectKind.Draw: Draw(e.Value); break;
            default:
                target.Status[e.Kind] = Math.Min(3, Math.Max(target.Status.GetValueOrDefault(e.Kind), e.Duration));
                target.Potency[e.Kind] = Math.Clamp(e.Value, 1, 4);
                break;
        }
    }

    static void Hurt(CombatantState target, int amount, CombatantState source)
    {
        if (source.Status.GetValueOrDefault(EffectKind.Strengthened) > 0)
            amount += source.Potency.GetValueOrDefault(EffectKind.Strengthened, 3);
        if (target.Status.GetValueOrDefault(EffectKind.Vulnerable) > 0)
            amount = (int)Math.Ceiling(amount * 1.5);
        int absorbed = Math.Min(target.Shield, amount);
        target.Shield -= absorbed;
        target.Health = Math.Max(0, target.Health - amount + absorbed);
    }

    static void Tick(CombatantState target)
    {
        if (target.Status.GetValueOrDefault(EffectKind.Bleeding) > 0)
            target.Health = Math.Max(0, target.Health - target.Potency.GetValueOrDefault(EffectKind.Bleeding, 3));
    }

    static void Decay(CombatantState target)
    {
        foreach (var key in target.Status.Keys.ToArray())
            target.Status[key] = Math.Max(0, target.Status[key] - 1);
    }

    void Resolve()
    {
        if (State.Enemy.Health <= 0) State.Result = "Victory";
        else if (State.Player.Health <= 0) State.Result = "Defeat";
    }

    public void Flee() { if (State.Result.Length == 0) State.Result = "Fled"; }

    public void Settle()
    {
        if (State.Settled || State.Result.Length == 0) return;
        State.Settled = true;
        var p = _game.Encounters.GetValueOrDefault(State.EncounterId) ?? new();
        double now = (_game.Day - 1) * 1440d + _game.WorldMinutes;
        p.AvailableAt = now + (State.Result == "Victory" ? State.Cooldown : 120);

        var rng = new Random(State.Seed);
        var foe = EnemyDefinition.Get(State.EnemyId);

        if (State.Result == "Victory")
        {
            p.Victories++;
            p.Resolved = !State.Repeat;
            int xp = Math.Max(State.RewardXp, foe.RewardXp);
            _game.Player.Experience += xp;
            _game.Player.Level = 1 + _game.Player.Experience / 100;

            var generic = new CardRepository().Generic.ToList();
            var reward = PickLootCard(generic, foe.LootTier, rng);
            State.RewardId = reward.Id;
            _game.Deck.Owned[State.RewardId] = _game.Deck.Owned.GetValueOrDefault(State.RewardId) + 1;

            int coins = rng.Next(foe.CoinMin, foe.CoinMax + 1);
            if (State.IsDuel) coins = Math.Max(8, coins / 2);
            _game.Player.Coins += coins;

            string bonus = "";
            if (foe.LootTier >= 3 && rng.NextDouble() < 0.35)
            {
                var potion = PickPotionCard(generic, rng);
                if (potion != null && potion.Id != reward.Id)
                {
                    _game.Deck.Owned[potion.Id] = _game.Deck.Owned.GetValueOrDefault(potion.Id) + 1;
                    bonus = $" · Poção: {potion.Name}";
                }
            }

            State.RewardText = $"+{xp} XP · +{coins} Reais · Carta: {reward.Name}{bonus}";
            _game.Player.Health = State.Player.Health;
        }
        else if (State.IsDuel)
        {
            int coins = rng.Next(5, 12);
            _game.Player.Coins += coins;
            _game.Player.Health = Math.Max(1, State.Player.Health);
            State.RewardText = $"Você perdeu o duelo, mas ganhou experiência. +{coins} Reais.";
        }
        else
        {
            _game.Player.Health = State.Result == "Defeat"
                ? Math.Max(1, _game.Player.MaxHealth / 2)
                : Math.Max(1, State.Player.Health - 5);
            if (State.Result == "Defeat")
            {
                State.ReturnX = 0;
                State.ReturnZ = 5;
                _game.Player.Experience = Math.Max(0, _game.Player.Experience - 5);
            }
            State.RewardText = State.Result == "Defeat"
                ? "Você desperta perto da cabana · metade da Vida · −5 experiência"
                : "Retirada · −5 Vida · território em alerta";
        }

        _game.Player.Mana = Math.Min(_game.Player.MaxMana, Math.Max(20, State.Mana));
        _game.Player.Clamp();
        _game.Encounters[State.EncounterId] = p;
        _game.CombatHistory.Add($"{State.EncounterId}:{State.Result}:dia{_game.Day}");
        if (_game.CombatHistory.Count > 40) _game.CombatHistory.RemoveAt(0);
        _game.PlayerX = State.ReturnX;
        _game.PlayerZ = State.ReturnZ;
        _game.PlayerRotationY = State.ReturnYaw;
    }

    /// <summary>
    /// Weighted loot by enemy tier: higher tiers prefer Rara/Heal and avoid dumping only commons.
    /// </summary>
    static CardDefinition PickLootCard(List<CardDefinition> pool, int tier, Random rng)
    {
        if (pool.Count == 0) throw new InvalidOperationException("Catálogo genérico vazio.");
        double roll = rng.NextDouble();
        string want = tier switch
        {
            >= 4 when roll < 0.55 => "Rara",
            >= 4 when roll < 0.80 => "Heal",
            >= 3 when roll < 0.40 => "Rara",
            >= 3 when roll < 0.65 => "Heal",
            >= 2 when roll < 0.25 => "Rara",
            >= 2 when roll < 0.40 => "Heal",
            _ when roll < 0.12 => "Rara",
            _ => "Comum"
        };

        IEnumerable<CardDefinition> filtered = want switch
        {
            "Rara" => pool.Where(c => c.Rarity is "Rara" or "Épica"),
            "Heal" => pool.Where(c => c.Category == CardCategory.Heal),
            _ => pool.Where(c => c.Rarity == "Comum" || string.IsNullOrEmpty(c.Rarity))
        };
        var list = filtered.ToList();
        if (list.Count == 0) list = pool;
        return list[rng.Next(list.Count)];
    }

    static CardDefinition? PickPotionCard(List<CardDefinition> pool, Random rng)
    {
        var potions = pool.Where(c => c.Category == CardCategory.Heal).ToList();
        if (potions.Count == 0) return null;
        return potions[rng.Next(potions.Count)];
    }
}
