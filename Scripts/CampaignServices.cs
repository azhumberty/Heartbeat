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
    public static List<AtlasNodeData> Create(long seed)
    {
        _=seed; // layout is intentionally readable; seed will vary future event payloads.
        AtlasNodeData N(string id,string title,AtlasNodeKind kind,float x,float y,string art,string text,AtlasNodeStatus status,params string[] links)=>new(){Id=id,Title=title,Kind=kind,X=x,Y=y,BackgroundId=art,Description=text,Status=status,Connections=links.ToList()};
        return new()
        {
            N("road","A estrada quebrada",AtlasNodeKind.Event,.12f,.55f,"street","O primeiro passo rumo a Eredan.",AtlasNodeStatus.Available,"merchant","forest"),
            N("merchant","Tenda do mercador",AtlasNodeKind.Merchant,.34f,.31f,"merchant_tent","Cartas e rumores sob uma lona dourada.",AtlasNodeStatus.Locked,"tavern"),
            N("forest","Floresta sombria",AtlasNodeKind.Combat,.36f,.70f,"forest_dark","Algo observa entre as raízes.",AtlasNodeStatus.Locked,"camp","knight"),
            N("tavern","Taverna da última chama",AtlasNodeKind.Scene,.58f,.23f,"tavern","Um lugar seguro para ouvir segredos.",AtlasNodeStatus.Locked,"ruin"),
            N("camp","Acampamento abandonado",AtlasNodeKind.Rest,.59f,.72f,"camp","Cinzas ainda guardam calor.",AtlasNodeStatus.Locked,"ruin"),
            N("knight","O cavaleiro sem brasão",AtlasNodeKind.Character,.68f,.52f,"street","Um encontro que pode mudar seu caminho.",AtlasNodeStatus.Locked,"ruin"),
            N("ruin","Salão das ruínas",AtlasNodeKind.Boss,.86f,.48f,"ruins_hall","A origem do rumor espera além do portão.",AtlasNodeStatus.Locked)
        };
    }
    public static void Complete(GameSave save,string id)
    {
        var node=save.AtlasNodes.FirstOrDefault(n=>n.Id==id);if(node==null)return;
        node.Status=AtlasNodeStatus.Completed;
        foreach(var next in node.Connections)
            if(save.AtlasNodes.FirstOrDefault(n=>n.Id==next) is { Status:AtlasNodeStatus.Locked } unlocked)unlocked.Status=AtlasNodeStatus.Available;
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
