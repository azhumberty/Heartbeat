using Godot;

namespace Heartbeat;

public sealed class SocialBeat
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Hint { get; init; } = "";
    public string Text { get; init; } = "";
}

public static class SocialBeatService
{
    public static string Key(string npcId, string beat) => npcId + ":" + beat;

    public static IReadOnlyList<SocialBeat> BeatsFor(CharacterData c)
    {
        if (!c.CanBuildRelationship) return Array.Empty<SocialBeat>();
        return new[]
        {
            new SocialBeat { Id="first_talk", Title="Primeira conversa", Hint="Fale com "+c.Name+".", Text="A primeira troca de palavras ficou gravada." },
            new SocialBeat { Id="campfire", Title="Fogueira", Hint="Convide para o acampamento (afeto 40).", Text=c.Name+" aceitou um lugar junto ao fogo." },
            new SocialBeat { Id="first_date", Title="Um instante a sos", Hint="Afeto 40 e confianca 30. Peca um tempo no parque.", Text="Passaram um momento a sos. O ar mudou entre voces." },
            new SocialBeat { Id="partner", Title="Laco", Hint="Romance 65.", Text="O vinculo deixou de ser so conversa." }
        };
    }

    public static List<string> TryUnlock(GameSave game, CharacterData c, CharacterState s, string intent="")
    {
        var unlocked=new List<string>();
        if (!c.CanBuildRelationship) return unlocked;
        game.UnlockedCinematics ??= new();
        game.MomentDetails ??= new();
        foreach (var beat in BeatsFor(c))
        {
            var key=Key(c.Id, beat.Id);
            if (game.UnlockedCinematics.Contains(key)) continue;
            var ready = beat.Id switch
            {
                "first_talk" => s.Conversation.Count>=2 || s.Memories.Count>0,
                "campfire" => game.CampResidents.Contains(c.Id),
                "first_date" => s.Affection>=40 && s.Trust>=30 && (intent=="park" || s.Romance>=20 || s.Flags.Contains("park_first_date")),
                "partner" => s.Romance>=65,
                _ => false
            };
            if (!ready) continue;
            game.UnlockedCinematics.Add(key);
            game.MomentDetails[key]=beat.Text;
            if (beat.Id=="first_date" && !s.Flags.Contains("park_first_date")) s.Flags.Add("park_first_date");
            unlocked.Add(beat.Title);
        }
        return unlocked;
    }
}

public static class CompanionProgress
{
    public static CompanionCardData For(CharacterData person)
    {
        if (person.CardData is { Enabled:true } existing) return existing;
        bool stout = person.PersonalityProfile.Courage>=60;
        return new CompanionCardData
        {
            Enabled=true,
            Companion=new CardDefinition
            {
                Name=person.Name, Title=person.Profession, Phrase="Estou com voce.",
                Category=CardCategory.Companion, Requirement="Conhecido", CharacterId=person.Id, Cost=0,
                Effects=new(){ new(){ Kind=EffectKind.Shield, Target=CardTarget.Self, Value=stout?14:9 } },
                Passive=new(){ Kind=stout?EffectKind.Shield:EffectKind.Heal, Target=CardTarget.Self, Value=3 }
            },
            Specials=new()
            {
                new(){ Id="side", Name="Lado a lado", Requirement="Amigo", CharacterId=person.Id, Cost=12, Category=CardCategory.Utility, Effects=new(){ new(){ Kind=EffectKind.Strengthened, Target=CardTarget.Self, Value=2, Duration=2 } } },
                new(){ Id="trust", Name="Confianca", Requirement="Confianca", CharacterId=person.Id, Cost=14, Category=CardCategory.Heal, Effects=new(){ new(){ Kind=EffectKind.Heal, Target=CardTarget.Self, Value=10 } } },
                new(){ Id="vow", Name="Promessa", Requirement="Romance", CharacterId=person.Id, Cost=16, Category=CardCategory.Defense, Effects=new(){ new(){ Kind=EffectKind.Shield, Target=CardTarget.Self, Value=12 } } }
            }
        };
    }

    public static int UpgradeLevel(CharacterState s, bool inCamp) =>
        s.Romance>=65 ? 2 : (inCamp || s.Affection>=40) ? 1 : 0;

    public static string Label(GameSave game, CharacterData c, CharacterState s)
    {
        var id="comp_"+c.Id;
        if (!game.Deck.Owned.ContainsKey(id) || game.Deck.Owned[id]<1) return "Carta: ainda nao";
        var lv=UpgradeLevel(s, game.CampResidents.Contains(c.Id));
        return lv>=2 ? "Carta: laco" : lv==1 ? "Carta: companheiro" : "Carta: conhecido";
    }
}

public static class PortraitMotion
{
    public static void Breath(Control node)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        node.PivotOffset = node.Size / 2f;
        if (node.Size == Vector2.Zero) node.PivotOffset = new Vector2(80, 120);
        var tw = node.CreateTween().SetLoops();
        tw.TweenProperty(node, "scale", new Vector2(1.02f, 1.03f), 1.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(node, "scale", Vector2.One, 1.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }
}
