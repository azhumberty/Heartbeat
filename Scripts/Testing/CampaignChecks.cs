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
    sealed class LoreHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var lore=new WorldLore{RegionName=new string('V',100),Premise="Uma chama desapareceu.",Threat="A noite avança.",Atmosphere="sombria",Factions=new(){"Vigília"},Rumors=new(){"Um cavaleiro espera."}};
            var content=JsonSerializer.Serialize(lore);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content}}}}))});
        }
    }

    public override async void _Ready()
    {
        try
        {
            var assets=new ContentLibrary().Load();
            Require(assets.Any(a=>a.Category=="Cenários"&&a.Enabled),"Nenhum cenário ativo.");
            Require(assets.Any(a=>a.Category=="Inimigos"&&a.Enabled),"Nenhum inimigo ativo.");
            var stock=MerchantStock.Parse("g01:12, g05:20; inválida, g01:99");Require(stock.Count==2&&stock[0].Price==12,"Estoque do mercador inválido.");
            var atlas=AtlasGenerator.Create(20260912);
            Require(atlas.Count>=9&&atlas.Count(n=>n.Connections.Count>1)>=1,"Atlas ramificado incompleto.");
            Require(atlas.Count(n=>n.Status==AtlasNodeStatus.Available&&!n.Persistent)==1&&atlas.Count(n=>n.Persistent&&n.Status==AtlasNodeStatus.Available)==1,"Início do Atlas inválido.");
            Require(atlas.All(n=>n.Connections.All(id=>atlas.Any(other=>other.Id==id))),"Atlas contém conexão inexistente.");
            Require(atlas.All(n=>!string.IsNullOrWhiteSpace(n.BackgroundId)),"Nó sem cenário.");
            Require(atlas.All(n=>!string.IsNullOrWhiteSpace(n.TemplateId)),"Nó sem identidade de rota.");
            for(int seed=1;seed<=24;seed++){var generated=AtlasGenerator.Create(seed,seed%4);Require(generated.Count(n=>n.Persistent)==1&&generated.Any(n=>n.Kind==AtlasNodeKind.Merchant)&&generated.Any(n=>n.Kind==AtlasNodeKind.Character)&&generated.Any(n=>n.Kind==AtlasNodeKind.Combat)&&generated.All(n=>n.Connections.All(id=>generated.Any(other=>other.Id==id))),"Variação procedural inválida.");}
            foreach(var combat in atlas.Where(n=>n.Kind is AtlasNodeKind.Combat or AtlasNodeKind.Boss))
                _=EnemyDefinition.Get(string.IsNullOrWhiteSpace(combat.ContentId)?"forest":combat.ContentId);
            var characterRepository=new CharacterRepository();Require(characterRepository.Load("roan") is {CanBuildRelationship:true}&&characterRepository.Load("silas") is {CanBuildRelationship:false},"Personagens do Grok não foram integrados.");
            Require(atlas.Where(n=>n.Kind is AtlasNodeKind.Character or AtlasNodeKind.Merchant).All(n=>characterRepository.Load(n.ContentId)!=null),"Nó social aponta para personagem inexistente.");
            Require(ChromaArt.LoadArt("Backgrounds/atlas_map.png").GetWidth()>0&&ChromaArt.LoadArt(ChromaArt.MinotaurSprite).GetWidth()>0,"Arte do Atlas ou do Minotauro indisponível.");
            var repairedMinotaur=ChromaArt.LoadArt(ChromaArt.MinotaurSprite).GetImage();Require(repairedMinotaur.GetPixel(0,0).A<.05f&&repairedMinotaur.GetPixel(repairedMinotaur.GetWidth()/2,repairedMinotaur.GetHeight()/2).A>.9f,"Recorte transparente do Minotauro não foi importado.");
            var save=new GameSave {WorldSeed=20260912,AtlasNodes=atlas,AtlasBackgroundPath=AtlasGenerator.Backdrop(20260912,0)};
            var resident=new CharacterData{Id="qa_resident",Name="Aren",Age=25,CanBuildRelationship=true};var residentState=new CharacterState{Affection=39};Require(!CampService.Invite(save,resident,residentState),"Convite ignorou afeição mínima.");residentState.Affection=40;Require(CampService.Invite(save,resident,residentState)&&save.CampResidents.Contains(resident.Id),"Morador não persistiu.");
            var previousBackdrop=save.AtlasBackgroundPath;AtlasGenerator.BeginNextExpedition(save);Require(save.ExpeditionIndex==1&&save.AtlasNodes.Where(n=>!n.Persistent).All(n=>n.Id.StartsWith("e1_"))&&save.AtlasNodes.Count(n=>n.Persistent)==1&&save.CampResidents.Contains(resident.Id),"Próxima expedição não preservou o acampamento.");
            Require(!string.IsNullOrWhiteSpace(save.AtlasBackgroundPath)&&!save.AtlasBackgroundPath.Equals(previousBackdrop,StringComparison.OrdinalIgnoreCase),"Nova expedição não trocou o fundo do Atlas.");
            atlas=save.AtlasNodes;
            var routeNode=atlas.First(n=>!n.Persistent);var story=new ProceduralEventService().Create(new EventContext {Game=save,Node=routeNode,Assets=assets});
            Require(story.Choices.Count is >=2 and <=4,"Evento sem escolhas válidas.");
            int ownedBefore=save.Deck.Owned.Count,eventCountBefore=save.RecentEvents.Count;
            _=new ProceduralEventService().Apply(save,story,story.Choices[0]);
            Require(save.RecentEvents.Count==eventCountBefore+1,"Escolha de evento não foi persistida.");
            Require(save.Deck.Owned.Count==ownedBefore,"Evento concedeu carta indevidamente.");
            await CheckGroqEvent(save,routeNode,assets);
            await CheckGroqLore();
            Require(ChromaArt.LoadArt("UI/Frames/menu_background.png")!=null,"Arte do menu indisponível.");
            var creative=new CreativeModeController();AddChild(creative);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(creative.IsInsideTree(),"Criativo não abriu.");creative.QueueFree();
            var companionEditor=new CompanionCardEditor{Data=resident};AddChild(companionEditor);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(companionEditor.IsInsideTree(),"Editor de carta do personagem não abriu.");companionEditor.QueueFree();
            var campView=new CampController{Game=save};AddChild(campView);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(campView.IsInsideTree(),"Acampamento não abriu.");campView.QueueFree();
            var atlasView=new AtlasController{Game=save};AddChild(atlasView);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(atlasView.IsInsideTree(),"Atlas ilustrado não abriu.");atlasView.QueueFree();
            WorldController.InitialSave=save;var world=new WorldController();AddChild(world);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(world.IsInsideTree(),"Fluxo Atlas/acampamento não iniciou.");world.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var newGame=new NewGameController{Settings=new GameSettings{UseOnlineAi=false}};AddChild(newGame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(newGame.IsInsideTree(),"Novo Jogo não abriu.");newGame.QueueFree();
            DeckManager.Migrate(save);var combatManager=CombatManager.Start(save,new CardRepository().Catalog(),"qa_combat","forest","forest",12);var arenaView=new CombatArenaController{Manager=combatManager,Game=save,ReduceMotion=true};AddChild(arenaView);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(arenaView.IsInsideTree(),"Arena de combate não abriu.");arenaView.QueueFree();
            save.Player.Coins=100;var shopView=new CardShopController{Game=save,MerchantId="merchant"};AddChild(shopView);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(shopView.IsInsideTree(),"Mercador não abriu.");
            var buyButton=shopView.FindChildren("*","Button",true,false).OfType<Button>().FirstOrDefault(button=>button.Text.StartsWith("Comprar",StringComparison.Ordinal));Require(buyButton!=null,"Mercador não mostrou uma compra.");var coinsBefore=save.Player.Coins;buyButton!.EmitSignal(Button.SignalName.Pressed);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(save.Player.Coins<coinsBefore,"Compra no mercador não foi processada.");
            var closeButton=shopView.FindChildren("*","Button",true,false).OfType<Button>().FirstOrDefault(button=>button.Text.StartsWith("Fechar",StringComparison.Ordinal));Require(closeButton!=null,"Mercador não mostrou o botão Fechar.");closeButton!.EmitSignal(Button.SignalName.Pressed);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Require(!GodotObject.IsInstanceValid(shopView)||!shopView.IsInsideTree(),"Mercador permaneceu aberto após uma compra.");
            GD.Print($"CAMPAIGN_CHECKS_PASS assets={assets.Count} nodes={atlas.Count}");
            GetTree().Quit();
        }
        catch(Exception e)
        {
            GD.PushError("CAMPAIGN_CHECKS_FAIL "+e.Message);
            GetTree().Quit(1);
        }
    }
    static async Task CheckGroqLore()
    {
        var previous=System.Environment.GetEnvironmentVariable("GROQ_API_KEY");
        try
        {
            System.Environment.SetEnvironmentVariable("GROQ_API_KEY","local-test-placeholder");using var handler=new LoreHandler();var result=await new GroqWorldLoreProvider(handler).CreateAsync(42,new GameSettings());
            Require(result.ProviderStatus.StartsWith("Online")&&result.Lore.RegionName.Length==70&&result.Lore.Rumors.Count==1,"Validação da introdução Groq falhou.");
        }
        finally{System.Environment.SetEnvironmentVariable("GROQ_API_KEY",previous);}
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
