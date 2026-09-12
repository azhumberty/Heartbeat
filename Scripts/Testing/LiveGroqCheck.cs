using Godot;
namespace Heartbeat;

/// <summary>Optional manual integration check. It never prints or stores the API key.</summary>
public partial class LiveGroqCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            var provider=new GroqDialogueProvider();
            if(!provider.IsConfigured) { GD.Print("[LIVE_GROQ] SKIP: GROQ_API_KEY ausente"); GetTree().Quit(); return; }
            var character=new CharacterData { Name="Erik",Age=29,Personality="reservado, leal e observador",Interests=new(){"floresta","histórias antigas"},SpeechStyle="calmo e direto" };
            var state=new CharacterState { CurrentLocation="Cabin",Relationship="Acquaintance" };
            new SocialMemoryService().PrepareTurn(character,state,"Interior de uma cabana medieval, durante a manhã",1,540);
             var result=await provider.ReplyAsync(character,state,"Como está a floresta esta manhã?",new GameSettings { UseOnlineAi=true },CancellationToken.None);
            if(!result.ProviderStatus.StartsWith("Online",StringComparison.OrdinalIgnoreCase))throw new Exception(result.ProviderStatus);
            if(string.IsNullOrWhiteSpace(result.Dialogue))throw new Exception("Resposta online vazia");
            GD.Print("[LIVE_GROQ] PASS: resposta JSON recebida da Groq"); GetTree().Quit();
        }
        catch(Exception e) { GD.PrintErr("[LIVE_GROQ] FAIL: "+e.Message); GetTree().Quit(1); }
    }
}
