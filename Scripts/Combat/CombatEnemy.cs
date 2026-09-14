namespace Heartbeat;

public enum EnemyRole { Normal, Elite, Boss }

public sealed class EnemyCardDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public IntentKind Kind { get; set; }
    public int Value { get; set; }
    public string Icon { get; set; } = "⚔️";
}

public static class EnemyDecks
{
    public static int MaxMana(EnemyDefinition foe, EnemyRole role) =>
        45 + foe.LootTier * 8 + role switch { EnemyRole.Boss => 28, EnemyRole.Elite => 14, _ => 0 };

    public static int Regen(EnemyRole role) => role switch { EnemyRole.Boss => 28, EnemyRole.Elite => 22, _ => 18 };

    public static float HealthScale(EnemyRole role) => role switch { EnemyRole.Boss => 1.65f, EnemyRole.Elite => 1.35f, _ => 1f };

    public static List<EnemyCardDef> Build(EnemyDefinition foe, EnemyRole role)
    {
        int dmg = Math.Max(6, foe.Damage);
        int guard = 8 + foe.LootTier * 2;
        int heal = 8 + foe.LootTier * 3;
        var deck = new List<EnemyCardDef>
        {
            Card("strike", "Golpe", 14, IntentKind.Attack, dmg, "⚔️"),
            Card("strike2", "Golpe", 14, IntentKind.Attack, dmg, "⚔️"),
            Card("guard", "Guarda", 12, IntentKind.Shield, guard, "🛡️"),
            Card("tonic", "Pocao", 16, IntentKind.Heal, heal, "🧪")
        };
        if (role is EnemyRole.Elite or EnemyRole.Boss)
        {
            deck.Add(Card("heavy", "Peso", 22, IntentKind.Attack, dmg + 6, "⚔️"));
            deck.Add(Card("wall", "Muralha", 18, IntentKind.Shield, guard + 6, "🛡️"));
        }
        if (role == EnemyRole.Boss)
        {
            deck.Add(Card("slam", "Investida", 26, IntentKind.Attack, dmg + 10, "⚔️"));
            deck.Add(Card("drain", "Drenar", 20, IntentKind.Heal, heal + 6, "🧪"));
        }
        return deck;
    }

    static EnemyCardDef Card(string id, string name, int cost, IntentKind kind, int value, string icon) =>
        new() { Id=id, Name=name, Cost=cost, Kind=kind, Value=value, Icon=icon };
}

public static class EnemyAi
{
    public static List<IntentAction> Plan(CombatState state, EnemyDefinition foe, List<EnemyCardDef> deck)
    {
        var planned = new List<IntentAction>();
        if (state.Enemy.Status.GetValueOrDefault(EffectKind.Stunned) > 0)
        {
            planned.Add(new IntentAction(IntentKind.Sleep, 0, "💤", "Atordoado"));
            return planned;
        }
        var role = Enum.TryParse<EnemyRole>(state.EnemyRole, out var parsed) ? parsed : EnemyRole.Normal;
        state.EnemyMaxMana = Math.Max(state.EnemyMaxMana, EnemyDecks.MaxMana(foe, role));
        state.EnemyMana = Math.Clamp(state.EnemyMana + EnemyDecks.Regen(role), 0, state.EnemyMaxMana);
        var rng = new Random(unchecked(state.Seed + state.Turn * 9973));
        var hand = deck.OrderBy(_ => rng.Next()).Take(role == EnemyRole.Boss ? 4 : 3).ToList();
        double hp = state.Enemy.MaxHealth <= 0 ? 1 : (double)state.Enemy.Health / state.Enemy.MaxHealth;
        int plays = 0;
        while (hand.Count > 0 && plays < 3)
        {
            EnemyCardDef? pick = null;
            if (hp < 0.4)
                pick = hand.Where(c => c.Kind is IntentKind.Heal or IntentKind.Potion && c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            pick ??= hand.Where(c => c.Kind == IntentKind.Attack && c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            pick ??= hand.Where(c => c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            if (pick == null) break;
            hand.Remove(pick);
            state.EnemyMana -= pick.Cost;
            planned.Add(new IntentAction(pick.Kind, pick.Value, pick.Icon, $"{pick.Name} · {pick.Value}"));
            plays++;
            if (pick.Kind == IntentKind.Heal) hp = Math.Min(1, hp + 0.2);
        }
        if (planned.Count == 0)
            planned.Add(new IntentAction(IntentKind.Attack, Math.Max(5, foe.Damage / 2), "⚔️", "Empurrão"));
        return planned;
    }
}
