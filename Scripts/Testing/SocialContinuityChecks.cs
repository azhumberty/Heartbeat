using Godot;
namespace Heartbeat;

public partial class SocialContinuityChecks : Node
{
    static void Check(bool value,string message) { if(!value)throw new Exception(message); GD.Print("[PASS] "+message); }
    public override async void _Ready()
    {
        int slot=Random.Shared.Next(100000,999999);
        try
        {
            var character=new CharacterData { Id="erik-test",Name="Erik",Age=28,Personality="tímido, orgulhoso e leal",Interests=new(){"lobos","histórias antigas"},Traits=new(){"reservado","competitivo"},SpeechStyle="direto e contido" };
            SocialModelMigrator.Migrate(character);
            Check(character.PersonalityProfile.Extraversion==25&&character.PersonalityProfile.Pride==75,"Personalidade antiga gera dimensões estáveis");
            Check(character.Preferences.FavoriteTopics.Contains("lobos"),"Interesses antigos migram para preferências");
            var state=new CharacterState(); var memory=new SocialMemoryService();
            memory.PrepareTurn(character,state,"Cabana na floresta, manhã",1,480);
            memory.RecordConfirmedPlayerAction(character,state,"Meu animal favorito é lobo.",1,481);
            for(int i=0;i<5;i++)await new ProceduralDialogueProvider().ReplyAsync(character,state,"Conte algo sobre a floresta",new());
            var recall=await new ProceduralDialogueProvider().ReplyAsync(character,state,"Você lembra qual animal eu gosto?",new());
            Check(recall.Dialogue.Contains("lobo",StringComparison.OrdinalIgnoreCase),"Fallback recupera fato após outras conversas");

            memory.RecordConfirmedPlayerAction(character,state,"Você é um idiota.",1,500);
            Check(state.CurrentMood=="irritated"&&state.Emotions.Anger>=45,"Insulto cria memória emocional e irritação");
            memory.PrepareTurn(character,state,"Trilha da floresta, tarde",1,800);
            Check(state.Emotions.Anger<50&&memory.Retrieve(state,"insulto",2).Any(m=>m.Tags.Contains("insult")),"Raiva diminui com o tempo, acontecimento permanece");

            int trust=state.Trust; memory.RecordConfirmedPlayerAction(character,state,"Eu te ajudei quando você precisou.",1,805);
            Check(state.Trust>trust&&state.Emotions.Gratitude>0,"Ajuda confirmada altera confiança e gratidão");
            state.Memories.Add(new CharacterMemory { Content="Um dragão entregou um reino ao jogador.",Confirmed=false,Importance=5,Tags=new(){"invented"},Source="provider" });
            string prompt=CharacterPromptBuilder.Build(character,state,"Você lembra de mim?",new());
            Check(prompt.Length<=4800&&prompt.Contains("Cabana",StringComparison.OrdinalIgnoreCase)==false,"Prompt é curto e usa apenas contexto atual");
            Check(!prompt.Contains("dragão entregou",StringComparison.OrdinalIgnoreCase),"Memória não confirmada não entra no prompt");

            var game=new GameSave { CharacterIds=new(){character.Id},CharacterStates=new(){[character.Id]=state},WorldSeed=7 };
            var saves=new SaveManager(); saves.Save(game,slot); var loaded=saves.Load(slot)!; var loadedState=loaded.CharacterStates[character.Id];
            Check(loadedState.Trust==state.Trust&&loadedState.Memories.Any(m=>m.Tags.Contains("favorite_animal")),"Save preserva confiança e memória estruturada");
            var afterReload=await new ProceduralDialogueProvider().ReplyAsync(character,loadedState,"Lembra do meu animal favorito?",new());
            Check(afterReload.Dialogue.Contains("lobo",StringComparison.OrdinalIgnoreCase),"NPC mantém continuidade após recarregar");

            var oldKey=System.Environment.GetEnvironmentVariable("GROQ_API_KEY"); System.Environment.SetEnvironmentVariable("GROQ_API_KEY",null);
            try
            {
                var offline=await new GroqDialogueProvider().ReplyAsync(character,loadedState,"Lembra do meu animal favorito?",new());
                Check(offline.Dialogue.Contains("lobo",StringComparison.OrdinalIgnoreCase)&&offline.ProviderStatus.Contains("ausente"),"Groq indisponível mantém personagem e memória no fallback");
            }
            finally { System.Environment.SetEnvironmentVariable("GROQ_API_KEY",oldKey); }
            var samePrompt=CharacterPromptBuilder.Build(character,loadedState,"Lembra do meu animal favorito?",new());
            Check(samePrompt.Contains("favorito é lobo",StringComparison.OrdinalIgnoreCase),"Retorno futuro da Groq recebe o mesmo canon local");
            var crowded=new CharacterState(); for(int i=0;i<20;i++)crowded.Memories.Add(new CharacterMemory { Content=$"Conversa cotidiana {i}.",Importance=1,Tags=new(){"small_talk"} });
            memory.RecordConfirmedPlayerAction(character,crowded,"Trouxe um presente para você.",2,600);
            Check(!string.IsNullOrWhiteSpace(crowded.MemorySummary)&&crowded.Memories.Count<21,"Interações antigas são compactadas em resumo persistente");
            GD.Print("[SOCIAL_CONTINUITY] PASS"); GetTree().Quit();
        }
        catch(Exception e) { GD.PrintErr("[SOCIAL_CONTINUITY] FAIL: "+e); GetTree().Quit(1); }
        finally
        {
            string path=ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.json"); if(File.Exists(path))File.Delete(path); if(File.Exists(path+".bak"))File.Delete(path+".bak");
        }
    }
}
