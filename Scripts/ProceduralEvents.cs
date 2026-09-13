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
}

public sealed class EventResult
{
    [System.Text.Json.Serialization.JsonIgnore] public string ProviderStatus { get; set; } = "Offline · evento procedural";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Um encontro na estrada";
    public string Text { get; set; } = "O caminho guarda uma história.";
    public string BackgroundPath { get; set; } = "";
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
            AtlasNodeKind.Rest=>Rest(node,rng),
            AtlasNodeKind.Scene=>Scene(node,rng),
            AtlasNodeKind.Mystery=>Mystery(node,rng),
            _=>Road(node,rng)
        };
        eventResult.BackgroundPath=node.BackgroundId;
        Sanitize(eventResult);
        return eventResult;
    }

    public string Apply(GameSave game,EventResult story,EventChoice choice)
    {
        var coins=Math.Clamp(choice.CoinsDelta,-50,50);
        if(coins>=0)EconomyService.Grant(game,coins);else EconomyService.TrySpend(game,-coins);
        game.Player.Health=Math.Clamp(game.Player.Health+Math.Clamp(choice.HealthDelta,-25,25),1,game.Player.MaxHealth);
        game.Player.Energy=Math.Clamp(game.Player.Energy+Math.Clamp(choice.EnergyDelta,-25,25),0,100);
        var summary=$"{story.Id}:{choice.Id}:{choice.ResultText}";
        game.RecentEvents.Add(summary[..Math.Min(summary.Length,220)]);
        while(game.RecentEvents.Count>16)game.RecentEvents.RemoveAt(0);
        return choice.ResultText;
    }

    static EventResult Road(AtlasNodeData node,Random rng)=>new()
    {
        Id="road_"+node.Id+"_"+rng.Next(3),Title=node.Title,
        Text=rng.Next(3) switch {0=>"Uma lanterna apagada balança ao lado da estrada. Pegadas recentes somem na lama.",1=>"Um sino toca ao longe, embora nenhuma torre seja visível entre as árvores.",_=>"Você encontra uma bolsa abandonada e um símbolo riscado numa pedra."},
        Choices=new(){new(){Id="investigate",Text="Investigar com cuidado",ResultText="A atenção revela algumas moedas e uma pista sobre as ruínas.",CoinsDelta=8,EnergyDelta=-4},new(){Id="move_on",Text="Seguir pelo caminho",ResultText="Você preserva suas forças e deixa o mistério para trás.",EnergyDelta=3}}
    };
    static EventResult Rest(AtlasNodeData node,Random rng)=>new()
    {
        Id="rest_"+node.Id+"_"+rng.Next(3),Title=node.Title,Text="As brasas ainda aquecem o acampamento. Por alguns minutos, o mundo parece silencioso.",
        Choices=new(){new(){Id="sleep",Text="Descansar junto ao fogo",ResultText="O descanso devolve força ao corpo.",HealthDelta=18,EnergyDelta=20},new(){Id="search",Text="Examinar o acampamento",ResultText="Entre as cinzas você encontra moedas esquecidas.",CoinsDelta=12,EnergyDelta=-6}}
    };
    static EventResult Scene(AtlasNodeData node,Random rng)=>new()
    {
        Id="scene_"+node.Id+"_"+rng.Next(3),Title=node.Title,Text="Conversas baixas, música distante e olhares discretos transformam o lugar em um abrigo temporário.",
        Choices=new(){new(){Id="listen",Text="Ouvir os rumores",ResultText="Você descobre que alguém o espera perto das ruínas."},new(){Id="meal",Text="Pedir uma refeição",ResultText="Uma refeição quente melhora seu ânimo.",CoinsDelta=-5,HealthDelta=8,EnergyDelta=8}}
    };
    static EventResult Mystery(AtlasNodeData node,Random rng)=>new()
    {
        Id="mystery_"+node.Id+"_"+rng.Next(3),Title=node.Title,Text="Uma presença invisível parece reconhecer o seu nome.",
        Choices=new(){new(){Id="answer",Text="Responder ao chamado",ResultText="A voz grava uma lembrança que ainda não faz sentido.",EnergyDelta=-5},new(){Id="resist",Text="Resistir e partir",ResultText="Você fecha a mente e retorna ao caminho.",EnergyDelta=2}}
    };
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
