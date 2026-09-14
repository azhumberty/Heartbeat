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
            var characterRepository=new CharacterRepository();Require(characterRepository.Load("roan") is {CanBuildRelationship:true}&&characterRepository.Load("silas") is {CanBuildRelationship:false},"Personagens do Grok nao foram integrados.");
            var kael=characterRepository.Load("kael");
            Require(kael is {CanBuildRelationship:true} && kael.PersonalityProfile.Extraversion<35 && characterRepository.Load("roan")!.PersonalityProfile.Pride>65,"P2: Roan e Kael precisam de personalidades distintas.");
            var helloRoan=new ConsistentOfflineDialogueProvider().Reply(characterRepository.Load("roan")!,new CharacterState(),"Ola");
            var helloKael=new ConsistentOfflineDialogueProvider().Reply(kael!,new CharacterState(),"Ola");
            Require(helloRoan.Dialogue!=helloKael.Dialogue,"P2: Roan e Kael responderam igual.");
            var farm=new CharacterState();
            for(int i=0;i<12;i++) new RelationshipSystem().Apply(farm,3,3,0,0,1);
            Require(farm.DailyAffectionGained<=RelationshipSystem.DailyCap && farm.Affection<=RelationshipSystem.DailyCap+18,"P2: anti-farm social nao limitou afeicao do dia.");
            var memState=new CharacterState();
            var social=new SocialMemoryService();
            for(int i=0;i<16;i++) social.RecordTurn(memState,"frase "+i,1,i);
            Require(memState.MemorySummary.Length>0 && memState.Memories.Count<=14,"P2: memoria longa nao foi resumida.");
            var named=new CharacterState();
            social.RecordConfirmedPlayerAction(kael!,named,"Me chamo humb",1,10);
            Require(named.Memories.Any(m=>m.Tags.Contains("player_name")),"P2: o nome do jogador nao virou memoria.");
            var prompt=CharacterPromptBuilder.Build(kael!,named,"lembra o meu nome?",new GameSettings());
            Require(prompt.Contains("Kael",StringComparison.OrdinalIgnoreCase)&&prompt.Contains("humb",StringComparison.OrdinalIgnoreCase),"P2: o prompt nao leva memoria/personalidade.");
            var catalog=new CardRepository().Catalog();
            Require(catalog.ContainsKey("comp_roan")&&catalog["comp_roan"].CharacterId=="roan"&&catalog.ContainsKey("comp_kael")&&catalog.ContainsKey("special_roan_side"),"P3: carta de companheiro nao vinculada ao NpcId.");
            var p3=new GameSave{CharacterStates=new(){["roan"]=new CharacterState{Trust=12,Affection=12},["kael"]=new CharacterState{Trust=5}}};
            DeckManager.SyncUnlocks(p3,catalog);
            Require(p3.Deck.Owned.GetValueOrDefault("comp_roan")>=1,"P3: Roan conhecido nao desbloqueou carta.");
            Require(p3.Deck.Owned.GetValueOrDefault("comp_kael")<1,"P3: Kael sem confianca nao devia ter carta.");
            var roanData=characterRepository.Load("roan")!;
            p3.CharacterStates["roan"].Affection=40; p3.CharacterStates["roan"].Trust=30;
            p3.CharacterStates["roan"].Conversation.Add("Jogador: ola"); p3.CharacterStates["roan"].Conversation.Add("Roan: ok");
            Require(CampService.Invite(p3,roanData,p3.CharacterStates["roan"])&&p3.UnlockedCinematics.Contains("roan:campfire"),"P3: fogueira nao desbloqueou momento.");
            SocialBeatService.TryUnlock(p3,roanData,p3.CharacterStates["roan"],"park");
            Require(p3.UnlockedCinematics.Contains("roan:first_date")&&p3.UnlockedCinematics.Contains("roan:first_talk"),"P3: encontro ou primeira conversa faltou.");
            Require(DeckManager.Meets(p3.CharacterStates["roan"],"Amigo",true)&&CompanionProgress.UpgradeLevel(p3.CharacterStates["roan"],true)>=1,"P3: progressao da carta por relacao falhou.");
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
            var elite=CombatManager.Start(save,new CardRepository().Catalog(),"qa_elite","forest","forest",12,EnemyRole.Elite);
            Require(elite.State.EnemyRole=="Elite"&&elite.State.EnemyMaxMana>=EnemyDecks.MaxMana(EnemyDefinition.Get("forest"),EnemyRole.Normal)&&elite.State.Enemy.MaxHealth>EnemyDefinition.Get("forest").Health,"P4: elite sem mana ou vida extra.");
            Require(elite.Intents.Count>=1,"P4: IA do inimigo nao jogou o deck.");
            var boss=CombatManager.Start(save,new CardRepository().Catalog(),"qa_boss","ruin","ruin",12,EnemyRole.Boss);
            Require(boss.State.EnemyRole=="Boss"&&boss.State.EnemyMaxMana>elite.State.EnemyMaxMana,"P4: boss nao e mais forte que elite.");
            var hpBefore=save.Player.MaxHealth;PlayerUpgrades.Apply(save,"vital");
            Require(save.Player.MaxHealth==hpBefore+12&&save.UnlockedUpgrades.Contains("vital"),"P4: upgrade do jogador nao aplicou.");
            var offer=PlayerUpgrades.Offer(save,7,3);Require(offer.Count==3&&offer.Select(u=>u.Id).Distinct().Count()==3,"P4: tela de progressao sem 3 escolhas.");
            var p5def=WorldGenerationService.CreateOffline("Uma ilha amaldiçoada onde o minotauro guarda um farol",20260913);
            var p5=WorldGenerationService.BuildSave(p5def,new GameSettings{UseOnlineAi=false});
            Require(p5.GeneratedCast.Count>=2&&p5.GeneratedEnemies.Count>=2,"P5: StoryDirector nao gerou elenco e inimigos.");
            Require(p5.GeneratedCast.Any(c=>c.Tags.Contains("merchant"))&&p5.GeneratedCast.Any(c=>c.CanBuildRelationship),"P5: falta mercador ou NPC gerado.");
            var p5boss=p5.AtlasNodes.First(n=>n.Kind==AtlasNodeKind.Boss);
            Require(p5.GeneratedEnemies.Any(e=>e.Id==p5boss.ContentId)&&p5boss.Title.Contains(p5.WorldLore.RegionName,StringComparison.OrdinalIgnoreCase),"P5: boss do Atlas nao veio do pedido do mundo.");
            Require(EnemyDefinition.Get(p5boss.ContentId,p5).Name==p5.GeneratedEnemies.First(e=>e.Id==p5boss.ContentId).Name,"P5: inimigo gerado nao entra no combate.");
            var p5event=new ProceduralEventService().Create(new EventContext{Game=p5,Node=p5.AtlasNodes.First(n=>n.Kind==AtlasNodeKind.Event),Assets=assets});
            Require(p5event.Text.Contains(p5.WorldLore.RegionName,StringComparison.OrdinalIgnoreCase)||p5event.Text.Contains("ilha",StringComparison.OrdinalIgnoreCase),"P5: evento offline ignorou o mundo.");
            Require(p5event.Choices.Any(c=>c.Gamble),"P5: minijogo da lanterna ausente.");
            var available=p5.AtlasNodes.First(n=>!n.Persistent&&n.Status==AtlasNodeStatus.Available);
            AtlasGenerator.Complete(p5,available.Id);
            Require(p5.StoryLog.Count>0&&p5.CurrentStoryBeat.Length>0,"P5: Atlas vivo nao registou o no.");
            var look=CanonicalLook.Ensure(p5.GeneratedCast[0],p5.WorldLore);
            Require(look.Length>24&&p5.GeneratedCast[0].CanonicalAppearance.Length>0,"P6: CanonicalAppearance vazia.");
            var k1=ImageCache.Key(ImageKind.Background,"fog road",1280,720,1);
            var k2=ImageCache.Key(ImageKind.Background,"fog road",1280,720,1);
            var k3=ImageCache.Key(ImageKind.Portrait,"fog road",768,1024,1);
            Require(k1==k2&&k1!=k3,"P6: cache key instavel.");
            var bgPrompt=BackgroundGenerationService.PromptFor(p5,p5boss);
            Require(bgPrompt.Contains(p5.WorldLore.RegionName,StringComparison.OrdinalIgnoreCase),"P6: fundo sem o lore do mundo.");
            var offlineBytes=await new OfflineImageProvider().GenerateAsync("test",64,64,1,ImageKind.Background,CancellationToken.None);
            Require(offlineBytes==null,"P6: provider offline nao deve baixar.");
            Require(ImageAi.Lock("a hall",ImageKind.Portrait).Contains("portrait",StringComparison.OrdinalIgnoreCase),"P6: style lock ausente.");
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
