namespace Heartbeat;

public sealed class EventContext
{
    public GameSave Game { get; init; } = null!;
    public AtlasNodeData Node { get; init; } = null!;
    public IReadOnlyList<ContentAssetRecord> Assets { get; init; } = Array.Empty<ContentAssetRecord>();
}

public sealed class EventChoice
{
    public string Id { get; set; } = "continue";
    public string Text { get; set; } = "Continuar";
    public string ResultText { get; set; } = "Você segue adiante.";
    public int CoinsDelta { get; set; }
    public int HealthDelta { get; set; }
    public int EnergyDelta { get; set; }
    public bool Gamble { get; set; }
}

public sealed class EventResult
{
    [System.Text.Json.Serialization.JsonIgnore] public string ProviderStatus { get; set; } = "Offline · evento procedural";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Um encontro na estrada";
    public string Text { get; set; } = "O caminho guarda uma história.";
    public string BackgroundPath { get; set; } = "";
    /// <summary>Short English description of the scene, used by Pollinations.ai to generate a dynamic background.</summary>
    public string ImagePrompt { get; set; } = "";
    public List<EventChoice> Choices { get; set; } = new();
}

/// <summary>Deterministic offline events. Provider-generated events can reuse the same validated result model.</summary>
public sealed class ProceduralEventService
{
    public EventResult Create(EventContext context)
    {
        var seed=StableSeed(context.Game.WorldSeed,context.Node.Id,context.Game.RecentEvents.Count);
        var rng=new Random(seed);var node=context.Node;
        var eventResult=node.Kind switch
        {
            AtlasNodeKind.Rest=>Rest(context,rng),
            AtlasNodeKind.Scene=>Scene(context,rng),
            AtlasNodeKind.Mystery=>Mystery(context,rng),
            _=>Road(context,rng)
        };
        if(node.Kind==AtlasNodeKind.Rest&&context.Game.CampResidents.Count>0)
        {
            var names=new CharacterRepository().List().Where(c=>context.Game.CampResidents.Contains(c.Id)).Select(c=>c.Name).ToList();
            eventResult.Text+="\n\nMoradores presentes: "+(names.Count>0?string.Join(", ",names):string.Join(", ",context.Game.CampResidents));
        }
        eventResult.BackgroundPath=node.BackgroundId;
        Sanitize(eventResult);
        return eventResult;
    }

    public string Apply(GameSave game,EventResult story,EventChoice choice)
    {
        var coins=Math.Clamp(choice.CoinsDelta,-50,50);
        var health=Math.Clamp(choice.HealthDelta,-25,25);
        var resultText=choice.ResultText;
        if(choice.Gamble)
        {
            var win=new Random(StableSeed(game.WorldSeed,story.Id,game.RecentEvents.Count)).Next(2)==0;
            if(win){resultText="A lanterna segura. "+choice.ResultText;coins=Math.Max(coins,12);health=Math.Max(health,0);}
            else{resultText="A lanterna apaga. O risco cobra o seu preço.";coins=Math.Min(coins,0);health=Math.Min(health,-8);}
        }
        if(coins>=0)EconomyService.Grant(game,coins);else EconomyService.TrySpend(game,-coins);
        game.Player.Health=Math.Clamp(game.Player.Health+health,1,game.Player.MaxHealth);
        game.Player.Energy=Math.Clamp(game.Player.Energy+Math.Clamp(choice.EnergyDelta,-25,25),0,100);
        var summary=$"{story.Id}:{choice.Id}:{resultText}";
        game.RecentEvents.Add(summary[..Math.Min(summary.Length,220)]);
        while(game.RecentEvents.Count>16)game.RecentEvents.RemoveAt(0);
        return resultText;
    }

    static string Flavor(EventContext context)
    {
        var lore=context.Game.WorldLore??new WorldLore();
        var prompt=context.Game.WorldPrompt??"";
        if(prompt.Length>90)prompt=prompt[..90].Trim()+"...";
        return (lore.RegionName+" · "+lore.Threat+(prompt.Length>0?" · "+prompt:"")).Trim();
    }

    static EventResult Road(EventContext context,Random rng)
    {
        var node=context.Node; var flavor=Flavor(context);
        return new()
        {
            Id="road_"+node.Id+"_"+rng.Next(3),Title=node.Title,
            ImagePrompt="dark medieval path in "+(context.Game.WorldLore.RegionName)+", fog, lantern, atmospheric",
            Text=flavor+". "+(rng.Next(3) switch {0=>"Uma lanterna apagada balança ao lado da estrada. Pegadas recentes somem na lama.",1=>"Um sino toca ao longe, embora nenhuma torre seja visível.",_=>"Você encontra uma bolsa abandonada e um símbolo riscado numa pedra."}),
            Choices=new(){
                new(){Id="investigate",Text="Investigar com cuidado",ResultText="A atenção revela moedas e uma pista sobre "+context.Game.WorldLore.Threat+".",CoinsDelta=8,EnergyDelta=-4},
                new(){Id="move_on",Text="Seguir pelo caminho",ResultText="Você preserva as forças e deixa o mistério para trás.",EnergyDelta=3},
                new(){Id="lantern",Text="Arriscar a lanterna",ResultText="A aposta ilumina um atalho escondido.",CoinsDelta=18,HealthDelta=-4,Gamble=true}
            }
        };
    }
    static EventResult Rest(EventContext context,Random rng)
    {
        var node=context.Node;
        return new()
        {
            Id="rest_"+node.Id+"_"+rng.Next(3),Title=node.Title,
            ImagePrompt="campfire in "+context.Game.WorldLore.RegionName+", night, medieval fantasy",
            Text="As brasas aquecem o acampamento em "+context.Game.WorldLore.RegionName+". Por um momento, "+context.Game.WorldLore.Threat+" parece distante.",
            Choices=new(){new(){Id="sleep",Text="Descansar junto ao fogo",ResultText="O descanso devolve força ao corpo.",HealthDelta=18,EnergyDelta=20},new(){Id="search",Text="Examinar o acampamento",ResultText="Entre as cinzas você encontra moedas esquecidas.",CoinsDelta=12,EnergyDelta=-6}}
        };
    }
    static EventResult Scene(EventContext context,Random rng)
    {
        var node=context.Node;
        var rumor=context.Game.WorldLore.Rumors.FirstOrDefault()??context.Game.WorldLore.Threat;
        return new()
        {
            Id="scene_"+node.Id+"_"+rng.Next(3),Title=node.Title,
            ImagePrompt="medieval interior in "+context.Game.WorldLore.RegionName+", candlelight, dark fantasy",
            Text="Conversas baixas em "+context.Game.WorldLore.RegionName+". Alguém murmura: "+rumor,
            Choices=new(){new(){Id="listen",Text="Ouvir os rumores",ResultText="Você descobre mais sobre "+context.Game.WorldLore.Threat+"."},new(){Id="meal",Text="Pedir uma refeição",ResultText="Uma refeição quente melhora o ânimo.",CoinsDelta=-5,HealthDelta=8,EnergyDelta=8}}
        };
    }
    static EventResult Mystery(EventContext context,Random rng)
    {
        var node=context.Node;
        return new()
        {
            Id="mystery_"+node.Id+"_"+rng.Next(3),Title=node.Title,
            ImagePrompt="ancient ruins, glowing runes, "+context.Game.WorldLore.Atmosphere+", eerie",
            Text="Uma presença em "+context.Game.WorldLore.RegionName+" parece reconhecer o teu nome. "+context.Game.WorldLore.Threat,
            Choices=new(){
                new(){Id="answer",Text="Responder ao chamado",ResultText="A voz grava uma lembrança ligada a "+context.Game.WorldLore.Threat+".",EnergyDelta=-5},
                new(){Id="resist",Text="Resistir e partir",ResultText="Você fecha a mente e retorna ao caminho.",EnergyDelta=2},
                new(){Id="lantern",Text="Arriscar a lanterna",ResultText="A luz compra um segredo.",CoinsDelta=16,HealthDelta=-6,Gamble=true}
            }
        };
    }
    public static EventResult Sanitize(EventResult result)
    {
        result.Id=Limit(result.Id,80);result.Title=Limit(string.IsNullOrWhiteSpace(result.Title)?"Evento":result.Title,80);result.Text=Limit(result.Text,900);
        result.Choices=(result.Choices??new()).Take(4).Where(c=>!string.IsNullOrWhiteSpace(c.Text)).ToList();
        if(result.Choices.Count<2)throw new InvalidOperationException("Evento precisa de pelo menos duas escolhas.");
        foreach(var choice in result.Choices)
        {
            choice.Id=Limit(choice.Id,60);choice.Text=Limit(choice.Text,120);choice.ResultText=Limit(choice.ResultText,500);
            choice.CoinsDelta=Math.Clamp(choice.CoinsDelta,-50,50);choice.HealthDelta=Math.Clamp(choice.HealthDelta,-25,25);choice.EnergyDelta=Math.Clamp(choice.EnergyDelta,-25,25);
        }
        return result;
    }
    static string Limit(string? value,int length){var text=value??"";return text[..Math.Min(text.Length,length)];}
    static int StableSeed(long worldSeed,string nodeId,int eventCount)
    {
        unchecked
        {
            var hash=(int)(worldSeed^(worldSeed>>32));
            foreach(var character in nodeId)hash=hash*31+character;
            return hash*31+eventCount;
        }
    }
}
