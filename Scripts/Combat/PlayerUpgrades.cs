namespace Heartbeat;

public sealed class PlayerUpgradeDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Stat { get; set; } = "";
    public int Value { get; set; }
}

public static class PlayerUpgrades
{
    public static readonly PlayerUpgradeDefinition[] All =
    {
        new() { Id="vital", Name="Vitalidade", Description="+12 Vida maxima e cura 12.", Stat="maxhp", Value=12 },
        new() { Id="channel", Name="Canal", Description="+15 Mana maxima.", Stat="maxmana", Value=15 },
        new() { Id="edge", Name="Gume", Description="+3 Dano nas tuas cartas.", Stat="damage", Value=3 },
        new() { Id="iron", Name="Ferro", Description="+2 Defesa. Sofrer menos.", Stat="defense", Value=2 },
        new() { Id="purse", Name="Bolsa", Description="+30 Reais agora.", Stat="coins", Value=30 },
        new() { Id="spark", Name="Faisca", Description="+8 Mana no inicio de cada turno.", Stat="manaregen", Value=8 }
    };

    public static List<PlayerUpgradeDefinition> Offer(GameSave game, int seed, int count=3)
    {
        game.UnlockedUpgrades ??= new();
        var pool = All.Where(u => !game.UnlockedUpgrades.Contains(u.Id) || u.Stat is "coins" or "maxhp" or "maxmana").ToList();
        if (pool.Count == 0) pool = All.ToList();
        var rng = new Random(seed);
        return pool.OrderBy(_ => rng.Next()).Take(Math.Clamp(count, 1, 3)).ToList();
    }

    public static void Apply(GameSave game, string id)
    {
        var u = All.FirstOrDefault(x => x.Id == id);
        if (u == null) return;
        game.UnlockedUpgrades ??= new();
        if (!game.UnlockedUpgrades.Contains(id)) game.UnlockedUpgrades.Add(id);
        switch (u.Stat)
        {
            case "maxhp":
                game.Player.MaxHealth += u.Value;
                game.Player.Health = Math.Min(game.Player.MaxHealth, game.Player.Health + u.Value);
                break;
            case "maxmana":
                game.Player.MaxMana += u.Value;
                game.Player.Mana = Math.Min(game.Player.MaxMana, game.Player.Mana + u.Value);
                break;
            case "damage": game.Player.Damage += u.Value; break;
            case "defense": game.Player.Defense += u.Value; break;
            case "coins": EconomyService.Grant(game, u.Value); break;
            case "manaregen": game.ManaRegenBonus += u.Value; break;
        }
        game.Player.Clamp();
    }

    public static int PicksFor(string role, bool duel, Random rng)
    {
        if (duel) return 0;
        if (string.Equals(role, "Boss", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(role, "Elite", StringComparison.OrdinalIgnoreCase)) return 1;
        return 1;
    }
}
