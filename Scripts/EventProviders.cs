using Godot;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

public interface IEventProvider
{
    Task<EventResult> CreateAsync(EventContext context,GameSettings settings,CancellationToken cancellationToken=default);
}

public sealed class OfflineEventProvider : IEventProvider
{
    public Task<EventResult> CreateAsync(EventContext context,GameSettings settings,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProceduralEventService().Create(context));
    }
}

/// <summary>Optional story variation. Every consequence is validated and later applied by ProceduralEventService.</summary>
public sealed class GroqEventProvider : IEventProvider
{
    readonly HttpMessageHandler? _handler;
    readonly OfflineEventProvider _fallback=new();
    public GroqEventProvider(HttpMessageHandler? handler=null){_handler=handler;}
    public bool IsConfigured=>!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));

    public async Task<EventResult> CreateAsync(EventContext context,GameSettings settings,CancellationToken cancellationToken=default)
    {
        if(!IsConfigured)return await Fallback("IA online indisponível · evento criado offline",context,settings,cancellationToken);
        try
        {
            if(!Uri.TryCreate(settings.Endpoint,UriKind.Absolute,out var endpoint)||endpoint.Scheme!="https"||endpoint.Host!="api.groq.com")
                return await Fallback("Endpoint Groq inválido · evento criado offline",context,settings,cancellationToken);
            using var client=_handler==null?new System.Net.Http.HttpClient():new System.Net.Http.HttpClient(_handler,false);
            client.Timeout=TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
            bool gptOss=settings.Model.StartsWith("openai/gpt-oss",StringComparison.OrdinalIgnoreCase);
            var request=new Dictionary<string,object>
            {
                ["model"]=settings.Model,["temperature"]=Math.Clamp(settings.Temperature,0f,1.1f),["max_completion_tokens"]=gptOss?1024:600,
                ["response_format"]=gptOss?StrictFormat():new {type="json_object"},
                ["messages"]=new[]{new {role="user",content=BuildPrompt(context)}}
            };
            if(gptOss){request["reasoning_format"]="hidden";request["reasoning_effort"]="low";}
            var payload=JsonSerializer.Serialize(request);
            using var response=await PostWithRetry(client,settings.Endpoint.TrimEnd('/')+"/chat/completions",payload,cancellationToken);
            if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Groq HTTP {(int)response.StatusCode}");
            using var envelope=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var content=envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"{}";
            content=content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n","",StringComparison.OrdinalIgnoreCase);
            var result=JsonSerializer.Deserialize<EventResult>(content,new JsonSerializerOptions {PropertyNameCaseInsensitive=true});
            if(result==null)throw new JsonException("Evento vazio");
            result.BackgroundPath=context.Node.BackgroundId;result.ProviderStatus="Online · evento criado pela Groq";
            GD.Print("[AI] Evento Groq validado");return ProceduralEventService.Sanitize(result);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception e)
        {
            GD.Print($"[AI] Evento Groq indisponível ({e.GetType().Name}); usando offline");
            return await Fallback("Falha na IA · evento criado offline",context,settings,cancellationToken);
        }
    }

    async Task<EventResult> Fallback(string status,EventContext context,GameSettings settings,CancellationToken token)
    {
        var result=await _fallback.CreateAsync(context,settings,token);result.ProviderStatus=status;return result;
    }
    internal static string SharedPrompt(EventContext context) => BuildPrompt(context);
    static string BuildPrompt(EventContext context)
    {
        var assets=context.Assets.Where(a=>a.Enabled&&a.Procedural).Take(8).Select(a=>new {a.Id,a.DisplayName,a.Category,a.Tags}).ToArray();
        var recent=context.Game.RecentEvents.TakeLast(5).Select(e=>e[..Math.Min(e.Length,120)]).ToArray();
        var compact=new {player=context.Game.PlayerName,seed=context.Game.WorldSeed,expedition=context.Game.ExpeditionIndex+1,campResidents=context.Game.CampResidents,region=context.Game.WorldLore.RegionName,premise=context.Game.WorldLore.Premise,worldPrompt=context.Game.WorldPrompt,threat=context.Game.WorldLore.Threat,factions=context.Game.WorldLore.Factions,node=new {context.Node.Id,context.Node.Title,kind=context.Node.Kind.ToString(),context.Node.Description},assets,recent};
        return "Crie um evento curto de RPG 2D em português, fiel ao pedido do jogador e à ameaça deste mundo. Forneça 2 a 4 escolhas distintas. Não conceda cartas. Deltas: coins -50..50, health -25..25, energy -25..25. Responda somente no JSON do schema. Contexto: "+JsonSerializer.Serialize(compact);
    }
    static async Task<HttpResponseMessage> PostWithRetry(System.Net.Http.HttpClient client,string url,string payload,CancellationToken token)
    {
        for(int attempt=0;;attempt++)
        {
            var response=await client.PostAsync(url,new StringContent(payload,Encoding.UTF8,"application/json"),token);
            if(attempt>0||((int)response.StatusCode!=429&&(int)response.StatusCode<500))return response;
            response.Dispose();await Task.Delay(TimeSpan.FromSeconds(1),token);
        }
    }
    static object StrictFormat()
    {
        var choiceProperties=new Dictionary<string,object>{["id"]=new{type="string"},["text"]=new{type="string"},["resultText"]=new{type="string"},["coinsDelta"]=new{type="integer"},["healthDelta"]=new{type="integer"},["energyDelta"]=new{type="integer"}};
        var properties=new Dictionary<string,object>{["id"]=new{type="string"},["title"]=new{type="string"},["text"]=new{type="string"},["choices"]=new{type="array",minItems=2,maxItems=4,items=new{type="object",properties=choiceProperties,required=choiceProperties.Keys.ToArray(),additionalProperties=false}}};
        return new {type="json_schema",json_schema=new{name="heartbeat_event",strict=true,schema=new{type="object",properties,required=properties.Keys.ToArray(),additionalProperties=false}}};
    }
}

public sealed class OpenRouterEventProvider : IEventProvider
{
    readonly OfflineEventProvider _fallback = new();

    public async Task<EventResult> CreateAsync(EventContext context, GameSettings settings, CancellationToken cancellationToken = default)
    {
        if (!OpenRouterClient.HasKey(settings))
            return await Fallback("Cole a chave OpenRouter em Opcoes · evento offline", context, settings, cancellationToken);
        try
        {
            var content = await OpenRouterClient.CompleteJson(settings, GroqEventProvider.SharedPrompt(context), 700, cancellationToken);
            var result = JsonSerializer.Deserialize<EventResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null) throw new JsonException("Evento vazio");
            result.BackgroundPath = context.Node.BackgroundId;
            result.ProviderStatus = "Online · OpenRouter";
            if (result.Choices == null || result.Choices.Count(c => !string.IsNullOrWhiteSpace(c.Text)) < 2)
            {
                var offline = new ProceduralEventService().Create(context);
                if (string.IsNullOrWhiteSpace(result.Title)) result.Title = offline.Title;
                if (string.IsNullOrWhiteSpace(result.Text)) result.Text = offline.Text;
                result.Choices = offline.Choices;
            }
            GD.Print("[AI] Evento OpenRouter validado");
            return ProceduralEventService.Sanitize(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            GD.Print("[AI] Evento OpenRouter indisponivel (" + e.GetType().Name + "); usando offline");
            return await Fallback("Falha na IA (" + e.GetType().Name + ") · evento offline", context, settings, cancellationToken);
        }
    }

    async Task<EventResult> Fallback(string status, EventContext context, GameSettings settings, CancellationToken token)
    {
        var result = await _fallback.CreateAsync(context, settings, token);
        result.ProviderStatus = status;
        return result;
    }
}
