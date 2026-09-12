using Godot;
using System.Text.Json;
namespace Heartbeat;

public sealed class CardRepository
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented=true };
    static List<CardDefinition>? _generic;
    public IReadOnlyList<CardDefinition> Generic => _generic ??= JsonSerializer.Deserialize<List<CardDefinition>>(Godot.FileAccess.GetFileAsString("res://Assets/Cards/generic.json"),Json) ?? new();
    readonly string _customPath;
    public CardRepository(string customPath="user://Cards/custom.json") => _customPath=ProjectSettings.GlobalizePath(customPath);
    public List<CardDefinition> Custom()
    {
        if(!File.Exists(_customPath))return new();
        try { return (JsonSerializer.Deserialize<List<CardDefinition>>(File.ReadAllText(_customPath),Json)??new()).Where(c=>c.Id.StartsWith("custom_")).Select(CardRules.Validate).ToList(); }
        catch(Exception e){GD.PushWarning("[Cards] Coleção inválida preservada: "+e.GetType().Name);return new();}
    }
    public void SaveCustom(CardDefinition card)
    {
        if(!card.Id.StartsWith("custom_") || card.CharacterId.Length>0)throw new ArgumentException("Só cartas customizadas podem ser editadas aqui.");
        var c=CardRules.Validate(card); var list=Custom(); list.RemoveAll(x=>x.Id==c.Id); list.Add(c); Write(list);
    }
    public void DeleteCustom(string id){if(!id.StartsWith("custom_"))throw new ArgumentException("Carta oficial protegida.");var list=Custom();list.RemoveAll(c=>c.Id==id);Write(list);}
    void Write(List<CardDefinition> list){Directory.CreateDirectory(Path.GetDirectoryName(_customPath)!);File.WriteAllText(_customPath+".tmp",JsonSerializer.Serialize(list,Json));File.Move(_customPath+".tmp",_customPath,true);}
    public Dictionary<string,CardDefinition> Catalog(IEnumerable<CharacterData>? cast=null)
    {
        var all=Generic.Concat(Custom()).ToDictionary(c=>c.Id,c=>CardRules.Copy(c));
        foreach(var person in cast??new CharacterRepository().List())
        {
            if(person.Age<18 || person.CardData is not {Enabled:true} config)continue;
            void Add(CardDefinition source,string id,bool companion)
            {
                var c=CardRules.Validate(source); c.Id=id;c.CharacterId=person.Id;
                if(companion)c.Category=CardCategory.Companion;
                if(string.IsNullOrWhiteSpace(c.Name)||c.Name=="Nova carta")c.Name=person.Name;
                all[id]=c;
            }
            Add(config.Companion,"comp_"+person.Id,true);
            foreach(var c in config.Specials.Take(4))Add(c,"special_"+person.Id+"_"+c.Id,false);
        }
        return all;
    }
}
public static class DeckManager
{
    static readonly string[] Starter={"g01","g01","g02","g02","g03","g05","g07","g09","g13","g13","g14","g16","g21","g21","g22","g27","g29","g33","g37","g38"};
    public static void Migrate(GameSave game)
    {
        game.Deck??=new();game.Encounters??=new();game.CombatHistory??=new();
        var d=game.Deck;d.Owned??=new();d.Cards??=new();d.Upgrades??=new();d.MaxCopies=Math.Clamp(d.MaxCopies,1,5);
        if(!d.Initialized){foreach(var c in new CardRepository().Generic)d.Owned[c.Id]=3;d.Cards=Starter.ToList();d.Initialized=true;}
    }
    public static void SyncUnlocks(GameSave game,Dictionary<string,CardDefinition> catalog)
    {
        Migrate(game);
        foreach(var c in catalog.Values)
        {
            bool unlocked=c.CharacterId.Length==0 ? c.Id.StartsWith("custom_") : game.CharacterStates.TryGetValue(c.CharacterId,out var s)&&Meets(s,c.Requirement);
            if(unlocked)game.Deck.Owned[c.Id]=Math.Max(game.Deck.Owned.GetValueOrDefault(c.Id),c.CharacterId.Length>0?1:3);
        }
    }
    public static bool Meets(CharacterState s,string requirement)=>requirement switch
    {"Amigo"=>s.Affection>=30&&s.Trust>=25,"Confiança"=>s.Trust>=50,"Romance"=>s.Romance>=35||s.Flags.Contains("park_first_date"),_=>s.Trust>=10};
    public static string Validate(PlayerDeck d,Dictionary<string,CardDefinition> catalog)
    {
        if(d.Cards.Count<12||d.Cards.Count>30)return "Use entre 12 e 30 cartas.";
        foreach(var g in d.Cards.GroupBy(x=>x))
        {
            if(!catalog.TryGetValue(g.Key,out var c))return "Há uma carta ausente. Remova-a do baralho.";
            if(c.Category==CardCategory.Companion)return "Escolha companheiros no campo próprio.";
            if(g.Count()>Math.Min(d.MaxCopies,d.Owned.GetValueOrDefault(g.Key)))return "Limite de cópias excedido: "+c.Name;
        }
        if(d.CompanionId.Length>0&&(!catalog.TryGetValue(d.CompanionId,out var companion)||companion.Category!=CardCategory.Companion||d.Owned.GetValueOrDefault(d.CompanionId)<1))return "Companheiro indisponível.";
        return "";
    }
    public static bool Add(PlayerDeck d,string id,Dictionary<string,CardDefinition> catalog)
    {
        if(!catalog.TryGetValue(id,out var c)||c.Category==CardCategory.Companion||d.Cards.Count>=30||d.Cards.Count(x=>x==id)>=Math.Min(d.MaxCopies,d.Owned.GetValueOrDefault(id)))return false;
        d.Cards.Add(id);return true;
    }
    public static bool Remove(PlayerDeck d,string id)=>d.Cards.Remove(id);
    public static void Restore(PlayerDeck d){d.Cards=Starter.ToList();d.MaxCopies=Math.Max(2,d.MaxCopies);foreach(var g in d.Cards.GroupBy(x=>x))d.Owned[g.Key]=Math.Max(d.Owned.GetValueOrDefault(g.Key),g.Count());}
    public static CardDefinition Upgraded(CardDefinition c,int level)
    {
        c=CardRules.Copy(c); if(!c.CanUpgrade)return c;
        foreach(var e in c.Effects)if(e.Kind is EffectKind.Damage or EffectKind.Shield or EffectKind.Heal)e.Value+=Math.Clamp(level,0,2)*2;
        return c;
    }
}
