namespace Heartbeat;

public enum EnemyRole { Normal, Elite, Boss }

public sealed class EnemyCardDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public IntentKind Kind { get; set; }
    public int Value { get; set; }
    public string Icon { get; set; } = "Atk";
}

public static class EnemyDecks
{
    public static int MaxMana(EnemyDefinition foe, EnemyRole role) =>
        70 + foe.LootTier * 6 + role switch { EnemyRole.Boss => 20, EnemyRole.Elite => 10, _ => 0 };

    public static int Regen(EnemyRole role) => role switch { EnemyRole.Boss => 26, EnemyRole.Elite => 22, _ => 18 };

    public static float HealthScale(EnemyRole role) => role switch { EnemyRole.Boss => 1.45f, EnemyRole.Elite => 1.22f, _ => 1f };

    public static List<EnemyCardDef> Build(EnemyDefinition foe, EnemyRole role)
    {
        int dmg = Math.Max(8, foe.Damage);
        int guard = 10 + foe.LootTier;
        int heal = 10 + foe.LootTier * 2;
        var deck = new List<EnemyCardDef>
        {
            Card("s1", "Golpe", 8, IntentKind.Attack, dmg, "Atk"),
            Card("s2", "Golpe", 8, IntentKind.Attack, dmg, "Atk"),
            Card("s3", "Corte", 12, IntentKind.Attack, dmg + 4, "Atk"),
            Card("g1", "Guarda", 8, IntentKind.Shield, guard, "Def"),
            Card("g2", "Guarda", 8, IntentKind.Shield, guard, "Def"),
            Card("h1", "Pocao", 10, IntentKind.Heal, heal, "Cura"),
            Card("s4", "Investida", 14, IntentKind.Attack, dmg + 6, "Atk"),
            Card("g3", "Muralha", 12, IntentKind.Shield, guard + 6, "Def")
        };
        if (role is EnemyRole.Elite or EnemyRole.Boss)
        {
            deck.Add(Card("e1", "Peso", 16, IntentKind.Attack, dmg + 8, "Atk"));
            deck.Add(Card("e2", "Couraca", 14, IntentKind.Shield, guard + 8, "Def"));
            deck.Add(Card("e3", "Pocao+", 12, IntentKind.Heal, heal + 6, "Cura"));
        }
        if (role == EnemyRole.Boss)
        {
            deck.Add(Card("b1", "Devastar", 18, IntentKind.Attack, dmg + 12, "Atk"));
            deck.Add(Card("b2", "Drenar", 14, IntentKind.Heal, heal + 10, "Cura"));
            deck.Add(Card("b3", "Barreira", 16, IntentKind.Shield, guard + 12, "Def"));
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
            planned.Add(new IntentAction(IntentKind.Sleep, 0, "Zz", "Atordoado"));
            return planned;
        }
        var role = Enum.TryParse<EnemyRole>(state.EnemyRole, out var parsed) ? parsed : EnemyRole.Normal;
        state.EnemyMaxMana = Math.Max(state.EnemyMaxMana, EnemyDecks.MaxMana(foe, role));
        if (state.Turn <= 1 && state.EnemyMana < state.EnemyMaxMana / 2)
            state.EnemyMana = state.EnemyMaxMana;
        else
            state.EnemyMana = Math.Clamp(state.EnemyMana + EnemyDecks.Regen(role), 0, state.EnemyMaxMana);
        var rng = new Random(unchecked(state.Seed + state.Turn * 9973));
        var hand = deck.OrderBy(_ => rng.Next()).Take(role == EnemyRole.Boss ? 5 : 4).ToList();
        double hp = state.Enemy.MaxHealth <= 0 ? 1 : (double)state.Enemy.Health / state.Enemy.MaxHealth;
        int limit = role == EnemyRole.Boss ? 3 : 2;
        int plays = 0;
        while (hand.Count > 0 && plays < limit)
        {
            EnemyCardDef? pick = null;
            if (hp < 0.45)
                pick = hand.Where(c => c.Kind is IntentKind.Heal or IntentKind.Potion && c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            if (plays == 1 && state.Enemy.Shield < 8)
                pick ??= hand.Where(c => c.Kind == IntentKind.Shield && c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            pick ??= hand.Where(c => c.Kind == IntentKind.Attack && c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            pick ??= hand.Where(c => c.Cost <= state.EnemyMana).OrderByDescending(c => c.Value).FirstOrDefault();
            if (pick == null) break;
            hand.Remove(pick);
            state.EnemyMana -= pick.Cost;
            planned.Add(new IntentAction(pick.Kind, pick.Value, pick.Icon, pick.Name + " " + pick.Value));
            plays++;
            if (pick.Kind == IntentKind.Heal) hp = Math.Min(1, hp + 0.2);
        }
        if (planned.Count == 0)
            planned.Add(new IntentAction(IntentKind.Attack, Math.Max(6, foe.Damage / 2), "Atk", "Empurrao " + Math.Max(6, foe.Damage / 2)));
        return planned;
    }
}
