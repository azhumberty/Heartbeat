using Godot;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Heartbeat;

public sealed class WorldLoreGeneration
{
    public WorldLore Lore {get;init;}=new();
    public string ProviderStatus {get;init;}="Offline · introdução procedural";
}
public interface IWorldLoreProvider
{
    Task<WorldLoreGeneration> CreateAsync(long seed,GameSettings settings,CancellationToken token=default,string prompt="");
}
public sealed class OfflineWorldLoreProvider : IWorldLoreProvider
{
    public Task<WorldLoreGeneration> CreateAsync(long seed,GameSettings settings,CancellationToken token=default,string prompt="")
    {
        token.ThrowIfCancellationRequested();
        var lore=string.IsNullOrWhiteSpace(prompt)
            ? WorldLoreManager.CreateOffline(seed)
            : WorldGenerationService.ToLore(WorldGenerationService.CreateOffline(prompt,seed));
        return Task.FromResult(new WorldLoreGeneration{Lore=lore,ProviderStatus=string.IsNullOrWhiteSpace(prompt)?"Offline · introdução procedural":"Offline · lore a partir do pedido"});
    }
}
public sealed class GroqWorldLoreProvider : IWorldLoreProvider
{
    readonly HttpMessageHandler? _handler;readonly OfflineWorldLoreProvider _fallback=new();
    public GroqWorldLoreProvider(HttpMessageHandler? handler=null){_handler=handler;}
    public async Task<WorldLoreGeneration> CreateAsync(long seed,GameSettings settings,CancellationToken token=default,string prompt="")
    {
        if(string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GROQ_API_KEY")))return await Fallback(seed,settings,"IA indisponível · introdução criada offline",token,prompt);
        try
        {
            if(!Uri.TryCreate(settings.Endpoint,UriKind.Absolute,out var endpoint)||endpoint.Scheme!="https"||endpoint.Host!="api.groq.com")return await Fallback(seed,settings,"Endpoint inválido · introdução criada offline",token,prompt);
            using var client=_handler==null?new System.Net.Http.HttpClient():new System.Net.Http.HttpClient(_handler,false);client.Timeout=TimeSpan.FromSeconds(20);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",System.Environment.GetEnvironmentVariable("GROQ_API_KEY"));
            var assets=new ContentLibrary().Load().Where(a=>a.Enabled&&a.Procedural).Take(10).Select(a=>new{a.Id,a.DisplayName,a.Category,a.Tags}).ToArray();
            var cast=new CharacterRepository().List().Where(c=>!c.Tags.Contains("creative-disabled")).Take(6).Select(c=>new{c.Id,c.Name,c.Description,c.CanBuildRelationship}).ToArray();
            var promptText="Crie a introdução original de uma campanha 2D medieval dark, romântica e misteriosa em português. O jogador ainda escolherá seu nome. Use o PEDIDO do jogador como guia principal. Use apenas inspiração nos assets, sem citar IDs. Responda somente no JSON do schema. Contexto: "+JsonSerializer.Serialize(new{seed,prompt=prompt??"",assets,cast});
            bool gptOss=settings.Model.StartsWith("openai/gpt-oss",StringComparison.OrdinalIgnoreCase);
            var request=new Dictionary<string,object>{["model"]=settings.Model,["temperature"]=Math.Clamp(settings.Temperature,0f,1.1f),["max_completion_tokens"]=gptOss?900:500,["response_format"]=gptOss?StrictFormat():new{type="json_object"},["messages"]=new[]{new{role="user",content=promptText}}};
            if(gptOss){request["reasoning_format"]="hidden";request["reasoning_effort"]="low";}
            using var response=await client.PostAsync(settings.Endpoint.TrimEnd('/')+"/chat/completions",new StringContent(JsonSerializer.Serialize(request),Encoding.UTF8,"application/json"),token);
            if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Groq HTTP {(int)response.StatusCode}");
            using var envelope=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));var content=envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"{}";
            content=content.Trim().TrimStart('`').TrimEnd('`').Replace("json\n","",StringComparison.OrdinalIgnoreCase);var lore=JsonSerializer.Deserialize<WorldLore>(content,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new JsonException("Lore vazio");
            Sanitize(lore);GD.Print("[AI] Introdução Groq validada");return new WorldLoreGeneration{Lore=lore,ProviderStatus="Online · introdução criada pela Groq"};
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(Exception e){GD.Print($"[AI] Introdução Groq indisponível ({e.GetType().Name}); usando offline");return await Fallback(seed,settings,"Falha na IA · introdução criada offline",token,prompt);}
    }
    async Task<WorldLoreGeneration> Fallback(long seed,GameSettings settings,string status,CancellationToken token,string prompt=""){var generated=await _fallback.CreateAsync(seed,settings,token,prompt);return new WorldLoreGeneration{Lore=generated.Lore,ProviderStatus=status};}
    public static WorldLore Sanitize(WorldLore lore)
    {
        lore.RegionName=Limit(lore.RegionName,70,"Terras sem nome");lore.Premise=Limit(lore.Premise,600,"Uma estrada antiga chama.");lore.Threat=Limit(lore.Threat,180,"Algo desperta nas ruínas.");lore.Atmosphere=Limit(lore.Atmosphere,160,"medieval, sombria e romântica");
        lore.Factions=(lore.Factions??new()).Where(v=>!string.IsNullOrWhiteSpace(v)).Take(3).Select(v=>Limit(v,80,"")).ToList();lore.Rumors=(lore.Rumors??new()).Where(v=>!string.IsNullOrWhiteSpace(v)).Take(4).Select(v=>Limit(v,150,"")).ToList();if(lore.Rumors.Count==0)lore.Rumors.Add("Há olhos atentos na estrada.");return lore;
    }
    static string Limit(string? value,int length,string fallback){var text=string.IsNullOrWhiteSpace(value)?fallback:value.Trim();return text[..Math.Min(text.Length,length)];}
    static object StrictFormat()
    {
        var properties=new Dictionary<string,object>{["regionName"]=new{type="string"},["premise"]=new{type="string"},["threat"]=new{type="string"},["atmosphere"]=new{type="string"},["factions"]=new{type="array",items=new{type="string"}},["rumors"]=new{type="array",items=new{type="string"}}};
        return new{type="json_schema",json_schema=new{name="heartbeat_world_lore",strict=true,schema=new{type="object",properties,required=properties.Keys.ToArray(),additionalProperties=false}}};
    }
}
