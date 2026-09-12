using System.Net;
using System.Net.Http;
using System.Text.Json;
namespace Heartbeat;

public static class ProviderChecks
{
    sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public bool Invalid;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;
            if(request.RequestUri?.Host!="api.groq.com" || request.Headers.Authorization?.Scheme!="Bearer")throw new Exception("Formato da requisição incorreto");
            if(Calls==1)return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429));
            var content=Invalid?"not json":JsonSerializer.Serialize(new DialogueResult { Dialogue="Bom te ver por aqui.",AffectionDelta=1000,Emotion="happy",Memory="O jogador veio conversar." });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { choices=new[]{new { message=new { content } }} })) });
        }
    }
    public static async Task Run()
    {
        var old=System.Environment.GetEnvironmentVariable("GROQ_API_KEY");
        try
        {
            System.Environment.SetEnvironmentVariable("GROQ_API_KEY","local-test-placeholder");
            using var handler=new Handler(); var result=await new GroqDialogueProvider(handler).ReplyAsync(new(),new(),"Olá",new());
            if(handler.Calls!=2 || result.AffectionDelta!=3 || !result.ProviderStatus.StartsWith("Online"))throw new Exception("Retry/validação falhou");
            Godot.GD.Print("[PASS] HTTP simulado: 429, retry, JSON e clamp");
            using var invalid=new Handler { Invalid=true }; result=await new GroqDialogueProvider(invalid).ReplyAsync(new(),new(),"Olá",new());
            if(result.ProviderStatus.StartsWith("Online") || string.IsNullOrEmpty(result.Dialogue))throw new Exception("Fallback JSON falhou");
            Godot.GD.Print("[PASS] HTTP simulado: JSON inválido retorna offline");
        }
        finally { System.Environment.SetEnvironmentVariable("GROQ_API_KEY",old); }
    }
}
