using System.Text;
using System.Text.RegularExpressions;
namespace Heartbeat;

/// <summary>Safe defaults and legacy conversion for provider-independent social state.</summary>
public static class SocialModelMigrator
{
    public static void Migrate(CharacterData c)
    {
        c.PersonalityProfile ??= new(); c.Preferences ??= new();
        c.Preferences.FavoriteTopics ??= new(); c.Preferences.AvoidedTopics ??= new(); c.Preferences.Goals ??= new();
        c.Preferences.Fears ??= new(); c.Preferences.FavoriteActivities ??= new(); c.Preferences.SocialBoundaries ??= new();
        if (c.PersonalityProfile.InitializedFromLegacy) return;
        foreach (var item in c.Interests.Concat(c.Likes).Where(x => !string.IsNullOrWhiteSpace(x)))
            AddUnique(c.Preferences.FavoriteTopics, item);
        foreach (var item in c.Dislikes.Where(x => !string.IsNullOrWhiteSpace(x))) AddUnique(c.Preferences.AvoidedTopics, item);
        foreach (var item in c.Hobbies.Where(x => !string.IsNullOrWhiteSpace(x))) AddUnique(c.Preferences.FavoriteActivities, item);
        string text = (c.Personality + " " + string.Join(' ', c.Traits)).ToLowerInvariant();
        var p = c.PersonalityProfile;
        if (Has(text,"tímid","timid","reservad")) p.Extraversion=25;
        if (Has(text,"sociável","sociavel","extrovert")) p.Extraversion=78;
        if (Has(text,"confiante","corajos")) p.Courage=75;
        if (Has(text,"gentil","empát","empat")) p.Empathy=75;
        if (Has(text,"orgulhos")) p.Pride=75;
        if (Has(text,"competitiv")) p.Competitiveness=75;
        if (Has(text,"românt","romant")) p.Romanticism=72;
        p.InitializedFromLegacy=true;
    }
    public static void Migrate(CharacterState s)
    {
        s.Memories ??= new(); s.Emotions ??= new(); s.MemorySummary ??=""; s.CurrentMood ??="content"; s.WorldContext ??="";
        if (s.Memories.Count == 0)
            foreach (var memory in s.ImportantMemories.Concat(s.RelationshipMemories).Distinct().Where(x => !string.IsNullOrWhiteSpace(x)).Take(12))
                s.Memories.Add(new CharacterMemory { Content=memory, Kind="legacy", Importance=4, Source="legacy_save", Confirmed=true, Tags=new(){"relationship"} });
        s.Clamp();
    }
    static bool Has(string text, params string[] values) => values.Any(text.Contains);
    static void AddUnique(List<string> list,string value) { if(!list.Any(x=>x.Equals(value,StringComparison.OrdinalIgnoreCase))) list.Add(value.Trim()); }
}

public sealed class SocialMemoryService
{
    static readonly Regex FavoriteAnimal = new(@"meu\s+animal\s+favorito\s+(?:é|e)\s+(?:o\s+|a\s+)?([\p{L}-]{2,30})", RegexOptions.IgnoreCase|RegexOptions.Compiled);
    static readonly string[] Insults = { "idiota", "imbecil", "inútil", "inutil", "covarde", "burro", "odeio você", "odeio voce" };
    static readonly string[] Help = { "eu te ajudei", "te ajudei", "eu salvei você", "eu salvei voce", "eu te salvei", "socorri você", "socorri voce" };
    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase) { "você","voce","qual","como","para","uma","que","isso","meu","minha","seu","sua","dos","das","por","com","ele","ela","gosto","lembra" };

    public void PrepareTurn(CharacterData c, CharacterState s, string worldContext, int day, float minutes)
    {
        SocialModelMigrator.Migrate(c); SocialModelMigrator.Migrate(s);
        float now = day * 1440f + minutes;
        if (s.LastEmotionUpdateMinutes >= 0) Decay(s, Math.Max(0, now-s.LastEmotionUpdateMinutes));
        s.LastEmotionUpdateMinutes=now; s.WorldContext=worldContext; s.CurrentMood=DeriveMood(c,s); s.CurrentEmotion=EmotionTag(s.CurrentMood); s.Clamp();
    }

    public CharacterMemory? RecordConfirmedPlayerAction(CharacterData c, CharacterState s, string input, int day, float minutes)
    {
        string clean=Regex.Replace(input.Trim(),@"\s+"," "); if(clean.Length>180) clean=clean[..180];
        CharacterMemory? memory=null;
        var animal=FavoriteAnimal.Match(clean);
        if(animal.Success)
        {
            string value=animal.Groups[1].Value.ToLowerInvariant();
            memory=New("fact",$"O jogador contou que seu animal favorito é {value}.",4,day,minutes,"curious",new(){"player_fact","favorite_animal",value});
            s.Emotions.Excitement+=3;
        }
        else if(Insults.Any(x=>clean.Contains(x,StringComparison.OrdinalIgnoreCase)))
        {
            memory=New("emotional","O jogador insultou este personagem durante uma conversa.",4,day,minutes,"angry",new(){"conflict","insult"});
            s.Emotions.Anger+=50; s.Emotions.Happiness-=15; s.Emotions.Respect-=8; s.Trust-=3; s.Stress+=8;
        }
        else if(Help.Any(x=>clean.Contains(x,StringComparison.OrdinalIgnoreCase)))
        {
            memory=New("relationship","O jogador lembrou que ajudou este personagem.",4,day,minutes,"grateful",new(){"help","relationship"});
            s.Emotions.Gratitude+=25; s.Emotions.Respect+=12; s.Emotions.Happiness+=8; s.Trust+=2;
        }
        else if(clean.Contains("prometo",StringComparison.OrdinalIgnoreCase))
        {
            memory=New("episode",$"O jogador fez uma promessa: {clean}",5,day,minutes,"hopeful",new(){"promise","relationship"});
        }
        else if(clean.Contains("presente",StringComparison.OrdinalIgnoreCase))
        {
            memory=New("episode","O jogador ofereceu um presente durante a conversa.",3,day,minutes,"grateful",new(){"gift","relationship"});
            s.Emotions.Gratitude+=16; s.Emotions.Happiness+=8;
        }
        var named=Regex.Match(clean,@"(?:meu nome (?:é|e)|me chamo)\s+([\p{L}][\p{L}\s]{1,24})",RegexOptions.IgnoreCase);
        if(named.Success)
            memory=New("fact","O jogador disse chamar-se "+named.Groups[1].Value.Trim()+".",5,day,minutes,"curious",new(){"player_fact","player_name"});
        if(memory!=null && !s.Memories.Any(m=>m.Kind==memory.Kind && m.Content==memory.Content)) s.Memories.Add(memory);
        RecordTurn(s,clean,day,minutes);
        Compact(s); s.CurrentMood=DeriveMood(c,s); s.CurrentEmotion=EmotionTag(s.CurrentMood); s.Clamp(); return memory;
    }

    public void RecordTurn(CharacterState s, string playerLine, int day, float minutes)
    {
        SocialModelMigrator.Migrate(s);
        var content="Jogador: "+playerLine;
        if(content.Length>180)content=content[..180];
        if(!s.Memories.Any(m=>m.Content==content))
            s.Memories.Add(New("short_term",content,1,day,minutes,"neutral",new(){"conversation"}));
        Compact(s);
    }

    public IReadOnlyList<CharacterMemory> Retrieve(CharacterState s,string query,int max=5)
    {
        SocialModelMigrator.Migrate(s); var tokens=Tokens(query).ToArray();
        return s.Memories.Where(m=>m.Confirmed).Select((m,index)=>(m,score:Score(m,tokens,index,s.Memories.Count)))
            .OrderByDescending(x=>x.score).ThenByDescending(x=>x.m.Importance).Take(Math.Clamp(max,1,8)).Where(x=>x.score>0).Select(x=>x.m).ToList();
    }
    static int Score(CharacterMemory m,string[] tokens,int index,int count)
    {
        int score=m.Importance*3 + (index>=count-5?4:0);
        string searchable=m.Content+" "+string.Join(' ',m.Tags);
        score+=tokens.Count(t=>searchable.Contains(t,StringComparison.OrdinalIgnoreCase))*8;
        if(m.Kind=="fact")score+=3; return score;
    }
    static IEnumerable<string> Tokens(string text) => Regex.Matches(text.ToLowerInvariant(),@"[\p{L}\d_-]{3,}").Select(m=>m.Value).Where(x=>!Stop.Contains(x)).Distinct();
    static CharacterMemory New(string kind,string content,int importance,int day,float minutes,string emotion,List<string> tags) => new() { Kind=kind,Content=content,Importance=importance,Day=day,WorldMinutes=minutes,Emotion=emotion,Tags=tags,Source="confirmed_player_action",Confirmed=true };
    static string EmotionTag(string mood)=>mood switch { "irritated"=>"angry","sad"=>"sad","shy"=>"shy","in_love"=>"romantic","excited"=>"happy",_=>"neutral" };

    public void Decay(CharacterState s,float elapsedMinutes)
    {
        int steps=Math.Clamp((int)(elapsedMinutes/60f),0,24); if(steps==0)return;
        s.Emotions.Anger=Math.Max(0,s.Emotions.Anger-steps*3); s.Emotions.Anxiety=Math.Max(0,s.Emotions.Anxiety-steps*2);
        s.Emotions.Excitement=Math.Max(10,s.Emotions.Excitement-steps*2); s.Emotions.Embarrassment=Math.Max(0,s.Emotions.Embarrassment-steps*2); s.Stress=Math.Max(0,s.Stress-steps);
    }
    public string DeriveMood(CharacterData c,CharacterState s)
    {
        var e=s.Emotions;
        if(e.Anger>=45)return "irritated"; if(s.Stress>=75 || e.Anxiety>=55)return "nervous"; if(s.Romance>=55&&s.Affection>=55)return "in_love";
        if(e.Embarrassment>=40 || (c.PersonalityProfile.Extraversion<35&&e.Excitement>30))return "shy"; if(e.Excitement>=50)return "excited";
        if(e.Happiness<30)return "sad"; if(s.Curiosity>=65)return "curious"; return "content";
    }
    void Compact(CharacterState s)
    {
        if(s.Memories.Count<=12)return;
        var compact=s.Memories.Where(m=>m.Importance<=2).Take(Math.Min(8,s.Memories.Count-10)).ToList(); if(compact.Count==0)return;
        string addition=string.Join(" ",compact.Select(m=>m.Content));
        string combined=(s.MemorySummary+" "+addition).Trim(); s.MemorySummary=combined[..Math.Min(520,combined.Length)];
        foreach(var item in compact)s.Memories.Remove(item);
    }
}

public static class CharacterPromptBuilder
{
    public static string Build(CharacterData c,CharacterState s,string input,GameSettings settings)
    {
        SocialModelMigrator.Migrate(c); SocialModelMigrator.Migrate(s);
        var p=c.PersonalityProfile; var memory=new SocialMemoryService();
        var relevant=memory.Retrieve(s,input,Math.Clamp(settings.ContextMemorySize,2,6));
        var b=new StringBuilder();
        b.AppendLine($"Você interpreta {c.Name}, homem adulto fictício de {c.Age} anos em um RPG medieval.");
        b.AppendLine($"Persona estável: {c.Personality}. Traços: {string.Join(", ",c.Traits.Take(6))}. Fala: {c.SpeechStyle}.");
        b.AppendLine($"Dimensões 0-100: extroversão {p.Extraversion}, coragem {p.Courage}, paciência {p.Patience}, empatia {p.Empathy}, romantismo {p.Romanticism}, competitividade {p.Competitiveness}, orgulho {p.Pride}.");
        b.AppendLine($"Preferências: {string.Join(", ",c.Preferences.FavoriteTopics.Take(6))}. Evita: {string.Join(", ",c.Preferences.AvoidedTopics.Take(4))}.");
        b.AppendLine($"Relação: {s.Relationship}; afeição {s.Affection}; confiança {s.Trust}; romance {s.Romance}; respeito {s.Emotions.Respect}. Humor simulado: {s.CurrentMood}; energia {s.Energy}; estresse {s.Stress}.");
        b.AppendLine("Contexto atual: "+s.WorldContext);
        b.AppendLine("Viva neste mundo. Use o pedido do jogador, a ameaça e o tom. Não invente outro cenário.");
        b.AppendLine("Fala APENAS como "+c.Name+" ("+c.SpeechStyle+"). Nao imite outro personagem.");
        if(s.Conversation.Count>0)b.AppendLine("Agora ha pouco: "+string.Join(" / ",s.Conversation.TakeLast(4)));
        if(!string.IsNullOrWhiteSpace(s.MemorySummary))b.AppendLine("Resumo persistente: "+s.MemorySummary);
        b.AppendLine("CANON confirmado (somente estes fatos podem ser lembrados como acontecimentos): "+(relevant.Count==0?"nenhum relevante":string.Join(" | ",relevant.Select(m=>m.Content))));
        b.AppendLine("Responda em português como o mesmo personagem, em 1-3 frases. Não invente encontros, promessas ou fatos passados. Fala criativa não vira canon. Mantenha personalidade, humor, relação, local e horário.");
        b.AppendLine("Retorne somente JSON: dialogue, emotion, desire, affectionDelta, trustDelta, romanceDelta, attractionDelta, energyDelta, stressDelta, memory, importantMemory. Use memory vazio: o jogo é a única autoridade de memória. Deltas inteiros -3 a 3.");
        string prompt=b.ToString(); return prompt[..Math.Min(prompt.Length,4800)];
    }
}

public sealed class ConsistentOfflineDialogueProvider : IDialogueProvider
{
    readonly SocialMemoryService _memory=new();
    public DialogueResult Reply(CharacterData c,CharacterState s,string input)
    {
        SocialModelMigrator.Migrate(c); SocialModelMigrator.Migrate(s);
        string lower=input.ToLowerInvariant(); var relevant=_memory.Retrieve(s,input,4);
        string line;
        var animal=relevant.FirstOrDefault(m=>m.Tags.Contains("favorite_animal"));
        var avoided=c.Preferences.AvoidedTopics.FirstOrDefault(topic=>lower.Contains(topic,StringComparison.OrdinalIgnoreCase));
        bool asksBoundary=new[]{"não quero","nao quero","pare","devagar","sem pressa"}.Any(lower.Contains);
        bool flirts=new[]{"flert","beij","bonito","atraente","gosto de você","gosto de voce","namor","intimidade"}.Any(lower.Contains);
        if(asksBoundary)
            return Result(c,s,"Tudo bem. Vou no teu ritmo.","neutral","talk",0,1);
        if(flirts)
        {
            bool comfortable=c.Age>=18&&s.Trust>=30&&s.Affection>=35&&s.Stress<65&&s.Energy>=25;
            line=comfortable
                ? Voice(c,"Ainda me custa admitir, mas tambem sinto isso.","Voce chamou minha atencao. Quero ver aonde isso vai.")
                : Voice(c,"Ainda nao... precisamos de mais tempo.","Ainda nao ha confianca suficiente. Prefiro que nos conheçamos melhor.");
            return Result(c,s,line,comfortable?(c.PersonalityProfile.Extraversion<35?"shy":"flirty"):"shy",comfortable?"flirt":"talk",comfortable?1:0,0,comfortable?1:0);
        }
        if((lower.Contains("lembra")||lower.Contains("recorda"))&&lower.Contains("animal"))
            line=animal!=null?$"Lembro. Voce me contou que seu animal favorito e {animal.Tags.Last()}.":"Ainda nao me lembro de voce ter dito qual e o seu animal favorito.";
        else if(lower.Contains("lembra")||lower.Contains("recorda"))
            line=relevant.Count>0?"Lembro: "+relevant[0].Content:(string.IsNullOrWhiteSpace(s.MemorySummary)?"Ainda estamos no comeco. Conta de novo, se quiser.":"Guardo isto: "+s.MemorySummary[..Math.Min(120,s.MemorySummary.Length)]);
        else if(lower.Contains("desculp")) line=s.Emotions.Anger>20?"Ouvi. Ainda estou magoado, mas podemos reconstruir.":"Esta bem. Prefiro respeito daqui em diante.";
        else if(avoided!=null) line=$"Prefiro nao falar sobre {avoided}.";
        else if(new[]{"idiota","imbecil","inútil","inutil","covarde","burro"}.Any(lower.Contains))
            line=Voice(c,"Isso doeu. Preciso de espaco.","Cuidado com as palavras. Nao vou esquecer.");
        else if(lower.Contains("ajud")) line=s.Emotions.Gratitude>20?"Eu me lembro da sua ajuda. Confianca nao veio do nada.":"Ajuda se mostra. Nao basta dizer.";
        else if(s.CurrentMood=="irritated") line=Voice(c,"Ainda estou aborrecido. Podemos falar baixo.","Nao vou fingir que esta tudo bem.");
        else if(s.CurrentMood=="nervous") line=Voice(c,"Este lugar me deixa tenso. Fica por perto.","Fale baixo. Estou alerta.");
        else line=Opening(c,s);
        return Result(c,s,line,EmotionFor(s.CurrentMood),s.CurrentMood=="irritated"?"be_alone":"talk",s.CurrentMood=="irritated"?0:1,s.CurrentMood=="irritated"?0:1);
    }
    static DialogueResult Result(CharacterData c,CharacterState s,string line,string emotion,string desire,int aff,int trust,int romance=0) =>
        new() { Dialogue=line, Emotion=emotion, Desire=desire, AffectionDelta=c.CanBuildRelationship?aff:0, TrustDelta=c.CanBuildRelationship?trust:0, RomanceDelta=c.CanBuildRelationship?romance:0, AttractionDelta=c.CanBuildRelationship?romance:0, EnergyDelta=-1, Memory="", ProviderStatus="Offline · "+c.Name };
    static string Opening(CharacterData c, CharacterState s)
    {
        if(!c.CanBuildRelationship || c.Tags.Contains("merchant"))
            return "Cartas, rumores, amuletos. O que o caminho te cobrou hoje?";
        if(c.PersonalityProfile.Extraversion<35)
            return "Eu... nao esperava companhia. "+Topic(c)+" tem ocupado a minha cabeca.";
        if(c.PersonalityProfile.Pride>65)
            return "Se veio falar, fale. "+Topic(c)+" eu entendo. O resto, veremos.";
        if(s.Relationship is "CloseFriend" or "Dating" or "Partner")
            return "Fico mais tranquilo quando e voce. Podemos falar de "+Topic(c)+".";
        return "Bom encontrar voce. "+Topic(c)+" serve de conversa.";
    }
    static string Voice(CharacterData c, string shy, string bold) => c.PersonalityProfile.Extraversion<35 ? shy : bold;
    static string Topic(CharacterData c)=>c.Preferences.FavoriteTopics.FirstOrDefault()??c.Interests.FirstOrDefault()??"o caminho adiante";
    static string EmotionFor(string mood)=>mood switch{"irritated"=>"angry","nervous"=>"shy","sad"=>"sad","shy"=>"shy","in_love"=>"romantic",_=>"neutral"};
    public Task<DialogueResult> ReplyAsync(CharacterData c,CharacterState s,string input,GameSettings settings,CancellationToken token=default)=>Task.FromResult(Reply(c,s,input));
}
