using System.Text.Json;
using System.Text.Json.Serialization;
namespace Heartbeat;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardCategory { Attack, Defense, Mana, Control, Heal, Utility, Companion }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EffectKind { Damage, Shield, Heal, Mana, Draw, Vulnerable, Strengthened, Bleeding, Stunned, Poison }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardTarget { Enemy, Self }
public sealed class CardEffect
{
    public EffectKind Kind { get; set; }
    public int Value { get; set; } = 6;
    public int Duration { get; set; } = 1;
    public CardTarget Target { get; set; } = CardTarget.Enemy;
}
public sealed class CardDefinition
{
    public string Id { get; set; } = "custom_" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nova carta";
    public string Title { get; set; } = "";
    public CardCategory Category { get; set; }
    public string Rarity { get; set; } = "Comum";
    public int Cost { get; set; } = 15;
    public string Description { get; set; } = "";
    public List<CardEffect> Effects { get; set; } = new() { new() };
    public List<string> Tags { get; set; } = new();
    public string ImagePath { get; set; } = "";
    public string FrameColor { get; set; } = "b39a64";
    public string Animation { get; set; } = "Impacto";
    public string Phrase { get; set; } = "Estou com você.";
    public string CharacterId { get; set; } = "";
    public string Requirement { get; set; } = "Conhecido";
    public string Condition { get; set; } = "Sempre";
    public bool CanUpgrade { get; set; } = true;
    public CardEffect? Passive { get; set; }
}
public sealed class CompanionCardData
{
    public bool Enabled { get; set; }
    public CardDefinition Companion { get; set; } = new() { Category = CardCategory.Companion, Effects = new() { new() { Kind=EffectKind.Shield, Target=CardTarget.Self, Value=12 } } };
    public List<CardDefinition> Specials { get; set; } = new();
}
public sealed class PlayerDeck
{
    public bool Initialized { get; set; }
    public int MaxCopies { get; set; } = 3;
    public Dictionary<string,int> Owned { get; set; } = new();
    public List<string> Cards { get; set; } = new();
    public Dictionary<string,int> Upgrades { get; set; } = new();
    public string CompanionId { get; set; } = "";
}
public sealed class EncounterProgress
{
    public bool Resolved { get; set; }
    public double AvailableAt { get; set; }
    public int Victories { get; set; }
}
public sealed class CombatantState
{
    public Dictionary<EffectKind,int> Potency { get; set; } = new();
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Shield { get; set; }
    public Dictionary<EffectKind,int> Status { get; set; } = new();
}
public sealed class CombatState
{
    public int RewardXp {get;set;}=24;
    public double Cooldown {get;set;}=360;
    public bool Repeat {get;set;}=true;
    public bool IsDuel {get;set;} = false;
    public string EncounterId { get; set; } = "";
    public string EnemyId { get; set; } = "forest";
    public string Arena { get; set; } = "Forest";
    public float Hour { get; set; } = 12;
    public CombatantState Player { get; set; } = new();
    public CombatantState Enemy { get; set; } = new();
    public int Mana { get; set; }
    public int MaxMana { get; set; } = 80;
    public int Turn { get; set; }
    public int Seed { get; set; }
    public int ShuffleCount { get; set; }
    public List<string> DrawPile { get; set; } = new();
    public List<string> Hand { get; set; } = new();
    public List<string> Discard { get; set; } = new();
    public Dictionary<string,CardDefinition> Cards { get; set; } = new();
    public List<CardEffect> Passives { get; set; } = new();
    public List<string> Summoned { get; set; } = new();
    public string Result { get; set; } = "";
    public bool Settled { get; set; }
    public string RewardId { get; set; } = "";
    public string RewardText { get; set; } = "";
    public float ReturnX { get; set; }
    public float ReturnY { get; set; }
    public float ReturnZ { get; set; }
    public float ReturnYaw { get; set; }
    public bool ClockWasPaused { get; set; }
}
public static class CardRules
{
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static string CategoryName(CardCategory c) => c switch { CardCategory.Attack=>"Ataque",CardCategory.Defense=>"Defesa",CardCategory.Mana=>"Mana",CardCategory.Control=>"Controle",CardCategory.Heal=>"Cura",CardCategory.Utility=>"Utilidade",_=>"Companheiro" };
    public static string EffectName(EffectKind e) => e switch { EffectKind.Damage=>"Dano",EffectKind.Shield=>"Escudo",EffectKind.Heal=>"Cura",EffectKind.Mana=>"Mana",EffectKind.Draw=>"Comprar",EffectKind.Vulnerable=>"Vulnerável",EffectKind.Strengthened=>"Fortalecido",EffectKind.Bleeding=>"Sangramento",EffectKind.Poison=>"Veneno",_=>"Atordoado" };
    public static string Describe(CardDefinition c) => string.Join(" · ",c.Effects.Select(e=>$"{EffectName(e.Kind)} {e.Value}{(e.Kind>=EffectKind.Vulnerable?$" ({e.Duration}t):"":"")} {(e.Target==CardTarget.Self?"em você":"no inimigo")}"));
    public static string BalanceWarning(CardDefinition c) => c.Effects.Sum(e=>e.Value) > c.Cost*1.5+12 ? "Carta muito forte para o custo. Considere aumentar a Mana." : "Valores dentro da faixa sugerida.";
    public static CardDefinition Validate(CardDefinition input)
    {
        var c=Copy(input);
        c.Name=(c.Name??"").Trim(); if(c.Name.Length==0)throw new ArgumentException("Digite o nome da carta.");
        c.Name=c.Name[..Math.Min(48,c.Name.Length)];
        c.Cost=Math.Clamp(c.Cost,0,60); c.Effects ??=new(); c.Tags ??=new();
        c.Effects=c.Effects.Take(3).ToList(); if(c.Effects.Count==0)throw new ArgumentException("Escolha pelo menos um efeito.");
        foreach(var e in c.Effects.Append(c.Passive).OfType<CardEffect>())
        {
            if(!Enum.IsDefined(e.Kind)||!Enum.IsDefined(e.Target))throw new ArgumentException("Efeito inválido.");
            e.Value=Math.Clamp(e.Value,1,e.Kind==EffectKind.Draw?3:e.Kind>=EffectKind.Vulnerable?4:35);
            e.Duration=Math.Clamp(e.Duration,1,e.Kind==EffectKind.Stunned?1:3);
        }
        if(c.Passive!=null) { c.Passive.Target=CardTarget.Self; c.Passive.Value=Math.Min(5,c.Passive.Value); if(c.Passive.Kind is not (EffectKind.Shield or EffectKind.Heal or EffectKind.Mana))c.Passive=null; }
        return c;
    }
}
