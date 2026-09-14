namespace Heartbeat;

public sealed partial class CombatManager
{
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

    void Hurt(CombatantState target, int amount, CombatantState source)
    {
        if (ReferenceEquals(source, State.Player)) amount += Math.Max(0, _game.Player.Damage / 3);
        if (ReferenceEquals(target, State.Player)) amount = Math.Max(1, amount - _game.Player.Defense);
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
        if (target.Status.GetValueOrDefault(EffectKind.Poison) > 0)
            target.Health = Math.Max(0, target.Health - target.Potency.GetValueOrDefault(EffectKind.Poison, 3));
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

}
