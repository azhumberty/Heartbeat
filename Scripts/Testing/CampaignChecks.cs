using Godot;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Heartbeat;

/// <summary>Headless integration check for the creative library and procedural Atlas.</summary>
public partial class CampaignChecks : Node
{
    sealed class EventHandler : HttpMessageHandler
    {
        public int Calls;public bool Invalid;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;
            if(request.RequestUri?.Host!="api.groq.com"||request.Headers.Authorization?.Scheme!="Bearer")throw new InvalidOperationException("Requisição de evento inválida.");
            if(Calls==1)return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429));
            var generated=new EventResult {Id="groq_test",Title="Eco na estrada",Text="Uma voz chama.",Choices=new(){new(){Id="a",Text="Ouvir",ResultText="Você escuta.",CoinsDelta=500},new(){Id="b",Text="Partir",ResultText="Você parte."}}};
            var content=Invalid?"not json":JsonSerializer.Serialize(generated);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content}}}}))});
        }
    }

    public override async void _Ready()
    {
        try
        {
            var assets=new ContentLibrary().Load();
            Require(assets.Any(a=>a.Category=="Cenários"&&a.Enabled),"Nenhum cenário ativo.");
            Require(assets.Any(a=>a.Category=="Inimigos"&&a.Enabled),"Nenhum inimigo ativo.");
            var atlas=AtlasGenerator.Create(20260912);
            Require(atlas.Count>=7,"Atlas incompleto.");
            Require(atlas.Count(n=>n.Status==AtlasNodeStatus.Available)==1,"Início do Atlas inválido.");
            Require(atlas.All(n=>n.Connections.All(id=>atlas.Any(other=>other.Id==id))),"Atlas contém conexão inexistente.");
            Require(atlas.All(n=>!string.IsNullOrWhiteSpace(n.BackgroundId)),"Nó sem cenário.");
            foreach(var combat in atlas.Where(n=>n.Kind is AtlasNodeKind.Combat or AtlasNodeKind.Boss))
                _=EnemyDefinition.Get(string.IsNullOrWhiteSpace(combat.ContentId)?"forest":combat.ContentId);
            var save=new GameSave {WorldSeed=20260912,AtlasNodes=atlas};
            var story=new ProceduralEventService().Create(new EventContext {Game=save,Node=atlas[0],Assets=assets});
            Require(story.Choices.Count is >=2 and <=4,"Evento sem escolhas válidas.");
            int ownedBefore=save.Deck.Owned.Count;
            _=new ProceduralEventService().Apply(save,story,story.Choices[0]);
            Require(save.RecentEvents.Count==1,"Escolha de evento não foi persistida.");
            Require(save.Deck.Owned.Count==ownedBefore,"Evento concedeu carta indevidamente.");
            await CheckGroqEvent(save,atlas[0],assets);
            Require(ChromaArt.LoadArt("UI/Frames/menu_background.png")!=null,"Arte do menu indisponível.");
            GD.Print($"CAMPAIGN_CHECKS_PASS assets={assets.Count} nodes={atlas.Count}");
            GetTree().Quit();
        }
        catch(Exception e)
        {
            GD.PushError("CAMPAIGN_CHECKS_FAIL "+e.Message);
            GetTree().Quit(1);
        }
    }
    static async Task CheckGroqEvent(GameSave save,AtlasNodeData node,IReadOnlyList<ContentAssetRecord> assets)
    {
        var previous=System.Environment.GetEnvironmentVariable("GROQ_API_KEY");
        try
        {
            System.Environment.SetEnvironmentVariable("GROQ_API_KEY","local-test-placeholder");
            using var handler=new EventHandler();var result=await new GroqEventProvider(handler).CreateAsync(new EventContext{Game=save,Node=node,Assets=assets},new GameSettings());
            Require(handler.Calls==2&&result.Choices[0].CoinsDelta==50&&result.ProviderStatus.StartsWith("Online"),"Retry ou validação do evento Groq falhou.");
            using var invalid=new EventHandler{Invalid=true};result=await new GroqEventProvider(invalid).CreateAsync(new EventContext{Game=save,Node=node,Assets=assets},new GameSettings());
            Require(!result.ProviderStatus.StartsWith("Online")&&result.Choices.Count>=2,"Fallback do evento Groq falhou.");
        }
        finally{System.Environment.SetEnvironmentVariable("GROQ_API_KEY",previous);}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
