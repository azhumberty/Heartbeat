namespace Heartbeat;

public sealed partial class CombatManager
{
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

            State.RewardId = "";
            int coins = State.IsDuel ? 8 : EconomyService.GrantCombatReward(_game, foe, rng);
            if (State.IsDuel) EconomyService.Grant(_game, coins);

            State.RewardText = $"+{xp} XP · +{coins} Reais";
            _game.Player.Health = State.Player.Health;
        }
        else if (State.IsDuel)
        {
            int coins = rng.Next(5, 12);
            EconomyService.Grant(_game, coins);
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
