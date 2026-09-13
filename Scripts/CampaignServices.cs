namespace Heartbeat;

/// <summary>Small, reusable campaign seed. It is intentionally local-first so a campaign is playable offline.</summary>
public static class WorldLoreManager
{
    public static WorldLore CreateOffline(long seed)
    {
        var rng=new Random(unchecked((int)seed));
        string[] regions={"Vale de Eredan","Marca de Vesper","Bosque de Arken","Terras de Noctis"};
        string[] threats={"uma lua negra se aproxima das ruínas","uma ordem perdida procura o Coração de Âmbar","as raízes da floresta começaram a lembrar nomes","um pacto antigo está se desfazendo"};
        string[] factions={"a Vigília das Lanternas","os Mercadores de Cinza","a Irmandade do Véu"};
        string region=regions[rng.Next(regions.Length)],threat=threats[rng.Next(threats.Length)];
        return new WorldLore {
            RegionName=region,Threat=threat,Atmosphere="sombria, íntima e cheia de promessas não cumpridas",
            Premise=$"Em {region}, {threat}. Você chega sem um passado certo, guiado apenas por um rumor.",
            Factions=factions.OrderBy(_=>rng.Next()).Take(2).ToList(),
            Rumors=new(){"Uma luz permanece acesa na taverna depois da meia-noite.","O mercador sabe mais do que admite."}
        };
    }
}

public static class AtlasGenerator
{
    public static List<AtlasNodeData> Create(long seed,int expeditionIndex=0)
    {
        expeditionIndex=Math.Max(0,expeditionIndex);var rng=new Random(unchecked((int)(seed+expeditionIndex*104729L)));
        var library=new ContentLibrary();var backgrounds=library.Enabled("Cenários");var enemies=library.Enabled("Inimigos");var characters=library.Enabled("Personagens");var merchants=library.Enabled("Mercadores");
        if(characters.Any(a=>!a.BuiltIn))characters=characters.Where(a=>!a.BuiltIn).ToList();
        if(merchants.Any(a=>!a.BuiltIn))merchants=merchants.Where(a=>!a.BuiltIn).ToList();
        ContentAssetRecord? Pick(List<ContentAssetRecord> source,string hint)
        {
            var matching=source.Where(a=>(a.Tags+","+a.Biome+","+a.DisplayName).Contains(hint,StringComparison.OrdinalIgnoreCase)).ToList();
            var pool=matching.Count>0?matching:source;if(pool.Count==0)return null;
            var total=pool.Sum(a=>Math.Max(.1f,a.Weight));var roll=rng.NextDouble()*total;
            foreach(var item in pool){roll-=Math.Max(.1f,item.Weight);if(roll<=0)return item;}return pool[^1];
        }
        string NodeId(string template)=>expeditionIndex==0?template:$"e{expeditionIndex}_{template}";
        AtlasNodeData N(string template,string title,AtlasNodeKind kind,float x,float y,string fallbackArt,string hint,string text,AtlasNodeStatus status,params string[] links)
        {
            var art=Pick(backgrounds,hint);var content=kind switch{AtlasNodeKind.Combat or AtlasNodeKind.Boss=>Pick(enemies,hint)?.Id??"",AtlasNodeKind.Character=>Pick(characters,hint)?.Id??"knight",AtlasNodeKind.Merchant=>Pick(merchants,hint)?.Id??"merchant",_=>art?.Id??""};return new(){Id=NodeId(template),TemplateId=template,Title=title,Kind=kind,X=Math.Clamp(x+(float)(rng.NextDouble()-.5)*.025f,.08f,.92f),Y=Math.Clamp(y+(float)(rng.NextDouble()-.5)*.04f,.14f,.82f),BackgroundId=art?.Path??fallbackArt,ContentId=content,Description=text,Status=status,Connections=links.Select(NodeId).ToList()};
        }
        return new()
        {
            N("road","A estrada quebrada",AtlasNodeKind.Event,.12f,.55f,"street","street","O primeiro passo rumo ao desconhecido.",AtlasNodeStatus.Available,"merchant","forest"),
            N("merchant","Tenda do mercador",AtlasNodeKind.Merchant,.34f,.31f,"merchant_tent","merchant","Cartas e rumores sob uma lona dourada.",AtlasNodeStatus.Locked,"tavern"),
            N("forest","Floresta sombria",AtlasNodeKind.Combat,.36f,.70f,"forest_dark","forest","Algo observa entre as raízes.",AtlasNodeStatus.Locked,"camp","knight"),
            N("tavern","Taverna da última chama",AtlasNodeKind.Scene,.58f,.23f,"tavern","tavern","Um lugar seguro para ouvir segredos.",AtlasNodeStatus.Locked,"ruin"),
            N("camp","Acampamento abandonado",AtlasNodeKind.Rest,.59f,.72f,"camp","camp","Cinzas ainda guardam calor.",AtlasNodeStatus.Locked,"ruin"),
            N("knight","O cavaleiro sem brasão",AtlasNodeKind.Character,.68f,.52f,"street","street","Um encontro que pode mudar seu caminho.",AtlasNodeStatus.Locked,"ruin"),
            N("ruin","Salão das ruínas",AtlasNodeKind.Boss,.86f,.48f,"ruins_hall","ruin","A origem do rumor espera além do portão.",AtlasNodeStatus.Locked)
        };
    }
    public static void Complete(GameSave save,string id)
    {
        var node=save.AtlasNodes.FirstOrDefault(n=>n.Id==id);if(node==null)return;
        node.Status=AtlasNodeStatus.Completed;
        foreach(var next in node.Connections)
            if(save.AtlasNodes.FirstOrDefault(n=>n.Id==next) is { Status:AtlasNodeStatus.Locked } unlocked)unlocked.Status=AtlasNodeStatus.Available;
    }
    public static void BeginNextExpedition(GameSave save)
    {
        save.ExpeditionIndex=Math.Min(save.ExpeditionIndex+1,1000000);save.AtlasNodes=Create(save.WorldSeed,save.ExpeditionIndex);
        save.RecentEvents.Add($"expedition:{save.ExpeditionIndex}:iniciada");while(save.RecentEvents.Count>16)save.RecentEvents.RemoveAt(0);
    }
    public static string TemplateFromId(string id)
    {
        if(string.IsNullOrWhiteSpace(id))return "event";var separator=id.IndexOf('_');return id.Length>2&&id[0]=='e'&&separator>1&&int.TryParse(id[1..separator],out _)?id[(separator+1)..]:id;
    }
}

public static class CampService
{
    public static bool CanInvite(GameSave game,CharacterData character,CharacterState state)=>character.CanBuildRelationship&&state.Affection>=40&&!game.CampResidents.Contains(character.Id);
    public static bool Invite(GameSave game,CharacterData character,CharacterState state)
    {
        if(!CanInvite(game,character,state))return false;game.CampResidents.Add(character.Id);if(!state.Flags.Contains("camp_resident"))state.Flags.Add("camp_resident");
        state.RecentMemories.Insert(0,$"Aceitou morar no acampamento de {game.PlayerName}.");while(state.RecentMemories.Count>12)state.RecentMemories.RemoveAt(state.RecentMemories.Count-1);return true;
    }
}

public static class EconomyService
{
    public static void Grant(GameSave game, int coins)
    {
        game.Player.Coins += Math.Max(0, coins); game.Player.Clamp();
    }
    public static bool TrySpend(GameSave game, int coins)
    {
        if (coins < 0 || game.Player.Coins < coins) return false;
        game.Player.Coins -= coins; return true;
    }
    public static int GrantCombatReward(GameSave game,EnemyDefinition foe,Random rng)
    {
        int coins=rng.Next(foe.CoinMin,foe.CoinMax+1); Grant(game, coins); return coins;
    }
}
