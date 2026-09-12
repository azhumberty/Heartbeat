using Godot;
namespace Heartbeat;

public partial class CombatChecks : Node
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("[PASS] "+text);}
    static IEnumerable<Node> Nodes(Node parent){foreach(var n in parent.GetChildren()){yield return n;foreach(var d in Nodes(n))yield return d;}}
    async Task Frames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    async Task Capture(string name)
    {
        if(!OS.GetCmdlineUserArgs().Contains("--capture"))return;
        await Frames(12);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://.qa-cache"));
        GetViewport().GetTexture().GetImage().SavePng("res://.qa-cache/"+name+".png");
    }
    public override async void _Ready()
    {
        int slot=Random.Shared.Next(1000000,2000000);string testFile="res://.qa-cache/cards-"+Guid.NewGuid().ToString("N")+".json";
        try
        {
            var repo=new CardRepository(testFile);var generic=repo.Generic;var catalog=repo.Catalog(Array.Empty<CharacterData>());
            Check(generic.Count==40&&generic.Select(c=>c.Id).Distinct().Count()==40,"40 cartas genéricas com IDs únicos");
            int[] counts={12,8,6,6,4,4};
            Check(counts.Select((count,i)=>generic.Count(c=>(int)c.Category==i)==count).All(x=>x),"Distribuição 12/8/6/6/4/4");
            var game=new GameSave {WorldSeed=123,WorldMinutes=780};new SaveManager().Migrate(game);
            Check(game.SaveVersion==5&&game.Deck.Cards.Count==20&&DeckManager.Validate(game.Deck,catalog)=="","Save antigo recebe baralho inicial válido");
            Check(DeckManager.Add(game.Deck,"g04",catalog)&&DeckManager.Remove(game.Deck,"g04"),"Adicionar e remover respeita coleção");
            game.Deck.Cards.Add("missing");Check(DeckManager.Validate(game.Deck,catalog).Length>0,"Carta excluída produz validação, sem apagar o baralho");game.Deck.Cards.Remove("missing");
            var custom=new CardDefinition{Name="Teste",Cost=999,Effects=new(){new(){Kind=EffectKind.Damage,Value=999}}};
            repo.SaveCustom(custom);var stored=repo.Custom().Single();Check(stored.Cost==60&&stored.Effects[0].Value==35,"Criador salva dados com limites");
            bool protectedCard=false;try{repo.DeleteCustom("g01");}catch(ArgumentException){protectedCard=true;}Check(protectedCard,"Carta oficial protegida");
            repo.DeleteCustom(custom.Id);Check(repo.Custom().Count==0,"Excluir customizada não altera oficiais");
            var person=new CharacterData {Id="test-companion",Name="Ari",Age=30};
            Check(!repo.Catalog(new[]{person}).Values.Any(c=>c.CharacterId==person.Id),"Personagem antigo sem CardData continua válido");
            person.CardData=new(){Enabled=true,Companion=new(){Name="Ari",Category=CardCategory.Companion,Cost=10,Effects=new(){new(){Kind=EffectKind.Shield,Target=CardTarget.Self,Value=10}},Passive=new(){Kind=EffectKind.Heal,Target=CardTarget.Self,Value=2}},Specials=new(){new(){Id="strike",Name="Golpe de Ari",Requirement="Amigo"}}};
            catalog=repo.Catalog(new[]{person});game.CharacterStates[person.Id]=new(){Trust=12,Affection=15};
            DeckManager.SyncUnlocks(game,catalog);Check(game.Deck.Owned.ContainsKey("comp_"+person.Id)&&!game.Deck.Owned.ContainsKey("special_test-companion_strike"),"Vínculo desbloqueia companheiro e mantém golpe bloqueado");
            game.CharacterStates[person.Id].Trust=30;game.CharacterStates[person.Id].Affection=35;DeckManager.SyncUnlocks(game,catalog);
            Check(game.Deck.Owned.ContainsKey("special_test-companion_strike"),"Amizade desbloqueia golpe especial");
            game.Deck.CompanionId="comp_"+person.Id;
            var fight=CombatManager.Start(game,catalog,"test","forest","Forest",13);
            Check(fight.State.Hand.Count==6,"Compra cinco cartas e disponibiliza companheiro");
            int companionIndex=fight.State.Hand.IndexOf(game.Deck.CompanionId);fight.Play(companionIndex);
            Check(fight.State.Summoned.Count==1&&fight.State.Passives.Count==1&&fight.State.Player.Shield==10,"Companheiro entra com habilidade e passiva");
            void Give(string id){fight.State.Hand.Clear();fight.State.Hand.Add(id);fight.State.Mana=80;}
            Give("g01");fight.State.Mana=0;int enemyHp=fight.State.Enemy.Health;Check(fight.Play(0).Length>0&&fight.State.Enemy.Health==enemyHp,"Mana insuficiente bloqueia uso");
            Give("g01");fight.Play(0);Check(fight.State.Enemy.Health<enemyHp&&fight.State.Mana==68&&fight.State.Discard.Contains("g01"),"Dano, custo e descarte funcionam");
            Give("g13");fight.Play(0);Check(fight.State.Player.Shield>=10,"Escudo é aplicado");
            fight.State.Player.Health=40;Give("g33");fight.Play(0);Check(fight.State.Player.Health>40,"Cura respeita vida máxima");
            Give("g21");fight.State.Mana=20;fight.Play(0);Check(fight.State.Mana>20,"Carta de Mana recupera recurso");
            Give("g27");fight.Play(0);Check(fight.State.Enemy.Status.GetValueOrDefault(EffectKind.Vulnerable)>0,"Vulnerável aplicado");
            Give("g28");fight.Play(0);Check(fight.State.Player.Status.GetValueOrDefault(EffectKind.Strengthened)>0,"Fortalecido aplicado");
            Give("g29");fight.Play(0);int bleedHp=fight.State.Enemy.Health;fight.EndTurn();Check(fight.State.Enemy.Health<bleedHp,"Sangramento causa dano no turno");
            Give("g30");fight.Play(0);int beforeStun=fight.State.Player.Health;fight.EndTurn();Check(fight.State.Player.Health>=beforeStun,"Atordoado impede ação inimiga");
            fight.State.DrawPile.Clear();fight.State.Hand.Clear();fight.State.Discard=new(){"g01","g01","g13"};fight.Draw(3);
            Check(fight.State.Hand.Count==3&&fight.State.Hand.Count(x=>x=="g01")==2,"Descarte embaralha preservando cópias");
            var saver=new SaveManager();saver.Save(game,slot);var loaded=saver.Load(slot)!;
            Check(loaded.ActiveCombat!=null&&loaded.ActiveCombat.Hand.SequenceEqual(fight.State.Hand)&&loaded.Deck.CompanionId==game.Deck.CompanionId,"Save/load preserva batalha, baralho e companheiro");
            fight.State.Enemy.Health=1;Give("g01");fight.Play(0);fight.Settle();int xp=game.Player.Experience;int owned=game.Deck.Owned[fight.State.RewardId];fight.Settle();
            Check(fight.State.Result=="Victory"&&xp>0&&game.Player.Experience==xp&&game.Deck.Owned[fight.State.RewardId]==owned,"Vitória recompensa exatamente uma vez");
            Check(game.Encounters["test"].AvailableAt>780,"Encontro recebe cooldown persistente");
            game.Deck.CompanionId="";fight=CombatManager.Start(game,catalog,"loss","forest","Forest",13);fight.State.Player.Health=1;fight.State.Player.Shield=0;fight.EndTurn();fight.Settle();
            Check(fight.State.Result=="Defeat"&&game.Player.Health==50&&game.PlayerX==0&&game.PlayerZ==5,"Derrota preserva save e retorna ao ponto seguro");
            var deckUi=new DeckEditor {Game=game,CatalogOverride=catalog,Saved=()=>saver.Save(game,slot)};AddChild(deckUi);await Frames(8);
            Check(Nodes(deckUi).OfType<ItemList>().Count()==2,"Editor apresenta coleção e baralho");
            await Capture("deck-editor");deckUi.QueueFree();await Frames();
            var editor=new CardEditor {Repository=repo};AddChild(editor);await Frames(5);
            Check(Nodes(editor).OfType<CardView>().Any(),"Criador tem preview de carta");
            editor.QueueFree();await Frames();
            // A real world transition, then return through the actual UI.
            var worldGame=new GameSave {WorldSeed=73021,WorldMinutes=780};
            var world=new WorldController {InitialSave=worldGame};AddChild(world);await Wait(1.4);
            var encounters=Nodes(world).OfType<EncounterManager>().Single();
            Check(encounters.Active.Count>0,"Inimigos visíveis são gerados em posição segura");
            Check(!encounters.Eligible(new(){Id="night-test",Period="Night"}),"Trigger noturno respeita horário");
            var enemy=encounters.Active.First();var player=Nodes(world).OfType<PlayerController>().Single();var pos=player.Position;var yaw=player.CameraYRotation;
            await world.EnterCombat(enemy);await Frames(5);
            var arena=Nodes(world).OfType<CombatArenaController>().Single();
            Check(world.IsInCombat&&player.Locked&&worldGame.ActiveCombat!=null,"Transição abre arena e bloqueia exploração");
            await Capture("combat-arena");
            int playable=arena.Manager.State.Hand.FindIndex(id=>arena.Manager.State.Cards[id].Cost<=arena.Manager.State.Mana);
            arena.Select(playable);await arena.PlaySelected();Check(!arena.Busy,"Animação conclui e libera nova jogada");
            arena.Manager.State.Enemy.Health=1;
            var attack=arena.Manager.State.Cards.Values.First(c=>c.Effects.Any(e=>e.Kind==EffectKind.Damage));
            arena.Manager.State.Hand=new(){attack.Id};arena.Manager.State.Mana=80;arena.Select(0);await arena.PlaySelected();
            Check(arena.Manager.State.Result=="Victory","Vitória é apresentada pela arena");
            Nodes(arena).OfType<Button>().First(b=>b.Text=="Retornar à floresta").EmitSignal(Button.SignalName.Pressed);await Wait(1);
            Check(!world.IsInCombat&&worldGame.ActiveCombat==null&&!player.Locked,"Retorno fecha arena e devolve controle");
            Check(new Vector2(player.Position.X-pos.X,player.Position.Z-pos.Z).Length()<.2f&&Math.Abs(player.CameraYRotation-yaw)<.01,"Retorno preserva posição e rotação");
            world.QueueFree();await Frames();
            GD.Print("[COMBAT] PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr("[COMBAT] FAIL: "+e);GetTree().Quit(1);}
        finally
        {
            foreach(var path in new[]{ProjectSettings.GlobalizePath(testFile),ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.json"),ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.json.bak")})if(File.Exists(path))File.Delete(path);
        }
    }
}
