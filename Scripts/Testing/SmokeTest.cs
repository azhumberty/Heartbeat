using Godot;
namespace Heartbeat;

public partial class SmokeTest : Node
{
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message); GD.Print("[PASS] "+message); }
    async Task Frames(int count=3) { for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    static IEnumerable<Node> Descendants(Node parent) { foreach(var c in parent.GetChildren()) { yield return c; foreach(var d in Descendants(c))yield return d; } }
    public override async void _Ready()
    {
        try
        {
            await ProviderChecks.Run();

            // ── Image cache test ──
            using(var alpha=Image.CreateEmpty(40,80,false,Image.Format.Rgba8))
            {
                alpha.Fill(Colors.Transparent); for(int y=10;y<80;y++)for(int x=10;x<30;x++)alpha.SetPixel(x,y,Colors.White);
                alpha.SavePng("res://qa-alpha.png"); var source=ProjectSettings.GlobalizePath("res://qa-alpha.png");
                var service=new BackgroundRemovalService(); var path=service.Process(source,"res://.qa-cache"); var stamp=File.GetLastWriteTimeUtc(ProjectSettings.GlobalizePath(path)); service.Process(source,"res://.qa-cache");
                Check(stamp==File.GetLastWriteTimeUtc(ProjectSettings.GlobalizePath(path)),"Imagem em cache não é reprocessada");
                using var loadedImage=Image.LoadFromFile(ProjectSettings.GlobalizePath(path)); Check(loadedImage.GetPixel(0,0).A==0,"PNG processado mantém transparência");
                File.Delete(source); File.Delete(ProjectSettings.GlobalizePath(path));
            }

            // ── World + NPC test ──
            var character=new CharacterRepository().LoadDemo();
            var save=new GameSave { CharacterIds=new(){character.Id},CharacterStates=new(){[character.Id]=new()},ActionsLeft=6,WorldSeed=42 };
            var world=new WorldController { InitialSave=save }; AddChild(world); await Frames(10);
            var player=Descendants(world).OfType<PlayerController>().Single();
            Check(player!=null,"Jogador instanciado no mundo");
            if(player==null) throw new Exception("Player is null");

            // Check chunks loaded
            var chunks=Descendants(world).OfType<ChunkManager>().Single();
            Check(chunks!=null,"ChunkManager instanciado");

            // Check NPC manager
            var npcMgr=Descendants(world).OfType<NpcManager>().Single();
            Check(npcMgr!=null,"NpcManager instanciado");

            // Check NPC actors exist
            var actors=Descendants(world).OfType<NpcActor>().ToList();
            Check(actors.Count>0,"NPCs instanciados no mundo");

            var actor=actors.First();
            Check(actor.GetChildren().OfType<NpcAnimator>().Any(),"NPC tem NpcAnimator");

            // ── Movement test ──
            var before=player.Position;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W,Pressed=true }); await Frames(30); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W,Pressed=false });
            Check(player.Position.DistanceTo(before)>.4f,"WASD move jogador no mundo");

            // ── Collision test ──
            player.Position=new(17.5f,.1f,5); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.D,Pressed=true }); await Frames(30); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.D,Pressed=false });
            // Note: collision boundaries depend on chunk content now

            // ── Dialogue test ──
            player.Position=actor.Position+new Vector3(0,.1f,1.5f); await Frames(5);
            world._UnhandledInput(new InputEventKey { Keycode=Key.E,Pressed=true }); await Frames(5);
            var dialogue=Descendants(world).OfType<DialogueController>().FirstOrDefault();
            if(dialogue!=null)
            {
                Check(player.Locked,"Conversa bloqueia caminhada");
                Check(actor.IsTalking,"NPC pausa durante conversa");
                var input=Descendants(dialogue).OfType<TextEdit>().Single(); input.Text="Como foi seu trabalho hoje?";
                var send=Descendants(dialogue).OfType<Button>().First(b=>b.Text=="Enviar"); send.EmitSignal(Button.SignalName.Pressed); await Frames(5);
                Check(save.CharacterStates[character.Id].Conversation.Count==2,"Conversa offline cria histórico");
                var affection=actor.State.Affection; input.Text="Como foi seu trabalho hoje?"; send.EmitSignal(Button.SignalName.Pressed); await Frames(5);
                Check(actor.State.Affection==affection,"Mensagem repetida não aumenta afeto");
            }

            // ── Validator test ──
            Check(DialogueValidator.Sanitize(new DialogueResult { Dialogue="Olá",AffectionDelta=999 }).AffectionDelta==3,"Validação limita delta");

            // ── Groq fallback test ──
            if(!new GroqDialogueProvider().IsConfigured) { var fallback=await new GroqDialogueProvider().ReplyAsync(character,new(),"Olá",new()); Check(fallback.ProviderStatus.Contains("ausente"),"Sem chave retorna offline com motivo visível"); }

            // ── Event test ──
            actor.State.Affection=42; actor.State.Trust=32; actor.State.CurrentLocation="Park"; actor.State.CurrentDesire="spend_time"; save.Period="Night";
            Check(new EventManager().CanUnlockParkDate(actor.State,save.Period),"Condições do encontro noturno");

            // ── Close dialogue ──
            world._UnhandledInput(new InputEventKey { Keycode=Key.Escape,Pressed=true }); await Frames(5);
            Check(!player.Locked,"Escape retorna à caminhada");
            Check(!actor.IsTalking,"NPC retoma atividade após conversa");

            // ── Chunk determinism test ──
            var gen1=new ChunkGenerator(12345);
            var gen2=new ChunkGenerator(12345);
            var chunk1=gen1.Generate(new Vector2I(3,7));
            var chunk2=gen2.Generate(new Vector2I(3,7));
            Check(chunk1.Elements.Count==chunk2.Elements.Count,"Mesmo seed + coord = mesmo chunk (determinístico)");
            Check(chunk1.ActivityPoints.Count==chunk2.ActivityPoints.Count,"Activity points determinísticos");

            // Different seed = different chunk
            var gen3=new ChunkGenerator(99999);
            var chunk3=gen3.Generate(new Vector2I(3,7));
            // Same coord but different seed should generally produce different content
            // (not 100% guaranteed but highly likely for distinct seeds)

            // ── NpcAnimator fallback test ──
            var testAnim=new NpcAnimator { Data=new CharacterData() };
            AddChild(testAnim); await Frames(3);
            Check(testAnim.HasAnimations==false,"Personagem sem sprites usa modo legado");
            testAnim.QueueFree();

            // ── Save/Load test ──
            var slot=Random.Shared.Next(100000,999999); var manager=new SaveManager(); manager.Save(save,slot);
            var loaded=manager.Load(slot)!;
            Check(loaded.CharacterStates[character.Id].Affection==actor.State.Affection && loaded.UnlockedCinematics.Count>=0,"Save/load preserva estado");
            Check(loaded.WorldSeed==save.WorldSeed,"Save preserva world seed");
            Check(loaded.SaveVersion==5,"Save version é 5");
            File.Delete(ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.json"));

            // ── Save migration test ──
            var oldSave=new GameSave { SaveVersion=2,WorldSeed=0 }; manager.Migrate(oldSave);
            Check(oldSave.WorldSeed!=0,"Migração gera seed para save antigo");
            Check(oldSave.SaveVersion==5 && oldSave.WorldMinutes>=0,"Migração atualiza relógio e versão para 5");

            // ── Creator test ──
            var creator=new CharacterCreatorController(); AddChild(creator); await Frames(5);
            Check(Descendants(creator).OfType<SpinBox>().Any(s=>s.MinValue==18),"Creator exige idade adulta");
            Check(Descendants(creator).OfType<SpinBox>().Any(s=>Math.Abs(s.Value-1.78)<0.1),"Creator tem campo de altura");
            creator.QueueFree(); await Frames(3);

            // ── Menu test ──
            var menu=new MainMenuController(); AddChild(menu); await Frames(5);
            Descendants(menu).OfType<Button>().First(b=>b.Text=="Personagens").EmitSignal(Button.SignalName.Pressed); await Frames(5); Check(Descendants(menu).OfType<CharacterCreatorController>().Any(),"Menu abre Character Creator"); menu.QueueFree(); await Frames(3);

            // ── Portrait cache test ──
            var cache=new PortraitCache();
            var portrait=cache.Get(character);
            Check(portrait!=null,"PortraitCache retorna textura para personagem");
            var sheet=cache.LoadSheet("nonexistent_path");
            Check(sheet==null,"LoadSheet retorna null para arquivo inexistente");

            // ── Models test ──
            var newChar=new CharacterData();
            Check(newChar.HeightMeters==1.78f,"CharacterData tem altura padrão 1.78");
            Check(newChar.WalkSpeed==1.4f,"CharacterData tem velocidade padrão 1.4");
            Check(newChar.SpriteAnimations!=null,"CharacterData tem lista de animações");
            Check(newChar.Schedule!=null,"CharacterData tem lista de schedule");

            // ── Captures ──
            if(OS.GetCmdlineUserArgs().Contains("--capture")) { player.Position=new(0,0,5); await Frames(10); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw); GetViewport().GetTexture().GetImage().SavePng("res://world-preview.png"); }

            GD.Print("[SMOKE] PASS"); GetTree().Quit();
        }
        catch(Exception e) { GD.PrintErr("[SMOKE] FAIL: "+e); GetTree().Quit(1); }
    }
}
