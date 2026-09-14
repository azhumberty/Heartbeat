namespace Heartbeat;

public sealed partial class CombatManager
{
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
            if (_planned.Count == 0) PlanIntents();
            foreach (var act in _planned)
            {
                switch (act.Kind)
                {
                    case IntentKind.Attack: Hurt(State.Player, act.Value, State.Enemy); break;
                    case IntentKind.Shield: State.Enemy.Shield = Math.Clamp(State.Enemy.Shield + act.Value, 0, 100); break;
                    case IntentKind.Heal:
                    case IntentKind.Potion:
                        State.Enemy.Health = Math.Min(State.Enemy.MaxHealth, State.Enemy.Health + act.Value);
                        break;
                }
            }
        }
        Decay(State.Enemy);
        Resolve();
        _planned.Clear();
        if (State.Result.Length == 0) BeginTurn();
    }

    void BeginTurn()
    {
        State.Turn++;
        State.Player.Shield = 0;
        Tick(State.Player);
        Resolve();
        if (State.Result.Length > 0) return;
        State.Mana = Math.Clamp(State.Mana + 30 + _game.ManaRegenBonus, 0, State.MaxMana);
        foreach (var e in State.Passives) Apply(e);
        Draw(5);
        Decay(State.Player);
        PlanIntents();
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

}
