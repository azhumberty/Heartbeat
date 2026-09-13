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
    const string DefaultAtlasBackground="res://Assets/ArtKit/Backgrounds/atlas_map.png";

    public static string Backdrop(long seed,int expeditionIndex,string? previous=null)
    {
        if(expeditionIndex<=0)return DefaultAtlasBackground;
        var candidates=new ContentLibrary().Enabled("Cenários")
            .Where(asset=>!asset.Path.StartsWith("res://",StringComparison.OrdinalIgnoreCase)||asset.Path.Contains("/Backgrounds/",StringComparison.OrdinalIgnoreCase))
            .Select(asset=>asset.Path).Where(path=>!string.IsNullOrWhiteSpace(path))
            .Append(DefaultAtlasBackground).Append("res://Assets/ArtKit/Backgrounds/atlas_map_lit.png")
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).ToList();
        if(candidates.Count==0)return DefaultAtlasBackground;
        var rng=new Random(unchecked((int)(seed+expeditionIndex*130363L)));var selected=candidates[rng.Next(candidates.Count)];
        if(candidates.Count>1&&selected.Equals(previous,StringComparison.OrdinalIgnoreCase))selected=candidates[(candidates.IndexOf(selected)+1)%candidates.Count];
        return selected;
    }

    public static List<AtlasNodeData> Create(long seed,int expeditionIndex=0,IReadOnlyCollection<string>? preferredContent=null)
    {
        expeditionIndex=Math.Max(0,expeditionIndex);var rng=new Random(unchecked((int)(seed+expeditionIndex*104729L)));
        var library=new ContentLibrary();var backgrounds=library.Enabled("Cenários");var enemies=library.Enabled("Inimigos");var characters=library.Enabled("Personagens");var merchants=library.Enabled("Mercadores");var cast=new CharacterRepository().List();
        if(characters.Any(a=>!a.BuiltIn))characters=characters.Where(a=>!a.BuiltIn).ToList();
        if(merchants.Any(a=>!a.BuiltIn))merchants=merchants.Where(a=>!a.BuiltIn).ToList();
        var prefer=new HashSet<string>(preferredContent??Array.Empty<string>(),StringComparer.OrdinalIgnoreCase);
        ContentAssetRecord? Pick(List<ContentAssetRecord> source,string hint)
        {
            var favored=prefer.Count==0?new List<ContentAssetRecord>():source.Where(a=>prefer.Contains(a.Id)).ToList();
            var matching=(favored.Count>0?favored:source).Where(a=>(a.Tags+","+a.Biome+","+a.DisplayName+","+a.Path).Contains(hint,StringComparison.OrdinalIgnoreCase)).ToList();
            var pool=matching.Count>0?matching:(favored.Count>0?favored:source);if(pool.Count==0)return null;
            var total=pool.Sum(a=>Math.Max(.1f,a.Weight));var roll=rng.NextDouble()*total;
            foreach(var item in pool){roll-=Math.Max(.1f,item.Weight);if(roll<=0)return item;}return pool[^1];
        }
        string NodeId(string template)=>expeditionIndex==0?template:$"e{expeditionIndex}_{template}";
        string Art(string hint,string fallback)=>Pick(backgrounds,hint)?.Path??fallback;
        string CastId(ContentAssetRecord? record,string fallback)
        {
            if(record==null)return fallback;if(!record.BuiltIn)return record.Id;var file=Path.GetFileName(record.Path);return cast.FirstOrDefault(character=>Path.GetFileName(character.MainImagePath).Equals(file,StringComparison.OrdinalIgnoreCase))?.Id??fallback;
        }
        string Content(AtlasNodeKind kind,string hint)=>kind switch
        {
            AtlasNodeKind.Combat or AtlasNodeKind.Boss=>Pick(enemies,hint)?.Id??(kind==AtlasNodeKind.Boss?"ruin":expeditionIndex>0?"minotaur":"forest"),
            AtlasNodeKind.Character=>CastId(Pick(characters,hint),"roan"),
            AtlasNodeKind.Merchant=>CastId(Pick(merchants,hint),"silas"),
            _=>""
        };
        var nodes=new List<AtlasNodeData>();
        var camp=new AtlasNodeData
        {
            Id="camp",TemplateId="camp",Title="Acampamento",Kind=AtlasNodeKind.Rest,Status=AtlasNodeStatus.Available,
            X=.08f,Y=.82f,BackgroundId=Art("camp","camp"),Description="Seu refúgio permanente entre expedições.",Persistent=true,Risk=0,Layer=expeditionIndex+1
        };
        nodes.Add(camp);
        int columns=expeditionIndex>=2?6:5;var columnsNodes=new List<List<AtlasNodeData>>();
        for(int col=0;col<columns;col++)
        {
            int count=col==0||col==columns-1?1:col==1?2:rng.Next(2,4);var column=new List<AtlasNodeData>();
            for(int row=0;row<count;row++)
            {
                var kind=KindFor(col,columns,expeditionIndex,rng);var template=col==0?"road":col==columns-1?"ruin":$"n{col}_{row}";var spec=Spec(kind,rng);
                float x=.20f+col*(.70f/Math.Max(1,columns-1)),y=count==1?.46f:.22f+row*(.56f/Math.Max(1,count-1));
                var node=new AtlasNodeData {Id=NodeId(template),TemplateId=kind switch {AtlasNodeKind.Merchant=>"merchant",AtlasNodeKind.Character=>"knight",AtlasNodeKind.Scene=>"tavern",_=>template},Title=spec.Title,Kind=kind,Status=col==0?AtlasNodeStatus.Available:AtlasNodeStatus.Locked,X=Math.Clamp(x+(float)(rng.NextDouble()-.5)*.03f,.16f,.93f),Y=Math.Clamp(y+(float)(rng.NextDouble()-.5)*.04f,.16f,.80f),BackgroundId=Art(spec.Hint,spec.Fallback),ContentId=Content(kind,spec.Hint),Description=spec.Text,Risk=kind switch {AtlasNodeKind.Boss=>Math.Clamp(3+expeditionIndex,3,5),AtlasNodeKind.Combat=>Math.Clamp(2+expeditionIndex/2,2,5),AtlasNodeKind.Mystery=>Math.Clamp(1+expeditionIndex/2,1,4),_=>Math.Clamp(expeditionIndex/2,0,3)},Layer=expeditionIndex+1};
                column.Add(node);nodes.Add(node);
            }
            columnsNodes.Add(column);
        }
        camp.Connections=columnsNodes[0].Select(n=>n.Id).ToList();
        for(int col=0;col<columnsNodes.Count-1;col++)
        {
            var current=columnsNodes[col];var next=columnsNodes[col+1];
            for(int i=0;i<current.Count;i++)
            {
                var links=new HashSet<string>{next[Math.Clamp((int)Math.Round(i*(next.Count-1)/(double)Math.Max(1,current.Count-1)),0,next.Count-1)].Id};if(next.Count>1&&rng.NextDouble()<.55)links.Add(next[rng.Next(next.Count)].Id);current[i].Connections=links.ToList();
            }
        }
        foreach(var kind in new[]{AtlasNodeKind.Merchant,AtlasNodeKind.Character,AtlasNodeKind.Combat})
        {
            if(nodes.Any(n=>n.Kind==kind))continue;var target=nodes.First(n=>!n.Persistent&&n.Kind!=AtlasNodeKind.Boss&&n.TemplateId!="road"&&n.Kind is not (AtlasNodeKind.Merchant or AtlasNodeKind.Character or AtlasNodeKind.Combat));var spec=Spec(kind,rng);target.Kind=kind;target.TemplateId=kind==AtlasNodeKind.Merchant?"merchant":kind==AtlasNodeKind.Character?"knight":"forest";target.Title=spec.Title;target.Description=spec.Text;target.BackgroundId=Art(spec.Hint,spec.Fallback);target.ContentId=Content(kind,spec.Hint);
        }
        return nodes;
    }
    public static void Complete(GameSave save,string id)
    {
        var node=save.AtlasNodes.FirstOrDefault(n=>n.Id==id);if(node==null||node.Persistent)return;
        node.Status=AtlasNodeStatus.Completed;
        int unlocked=0;
        foreach(var next in node.Connections)
            if(save.AtlasNodes.FirstOrDefault(n=>n.Id==next) is { Status:AtlasNodeStatus.Locked } target)
            { target.Status=AtlasNodeStatus.Available; unlocked++; }
        if(unlocked>0)return;
        var later=save.AtlasNodes
            .Where(n=>!n.Persistent&&n.Id!=node.Id&&n.Status==AtlasNodeStatus.Locked&&n.X>node.X+0.03f)
            .OrderBy(n=>n.X).ThenBy(n=>Math.Abs(n.Y-node.Y)).ToList();
        var first=later.FirstOrDefault();
        if(first==null)return;
        foreach(var sibling in later.Where(n=>Math.Abs(n.X-first.X)<0.06f))
            sibling.Status=AtlasNodeStatus.Available;
    }
    public static void BeginNextExpedition(GameSave save)
    {
        save.ExpeditionIndex=Math.Min(save.ExpeditionIndex+1,1000000);save.AtlasBackgroundPath=Backdrop(save.WorldSeed,save.ExpeditionIndex,save.AtlasBackgroundPath);save.AtlasNodes=Create(save.WorldSeed,save.ExpeditionIndex,save.SelectedLibraryIds);
        save.RecentEvents.Add($"expedition:{save.ExpeditionIndex}:iniciada");while(save.RecentEvents.Count>16)save.RecentEvents.RemoveAt(0);
    }
    public static string TemplateFromId(string id)
    {
        if(string.IsNullOrWhiteSpace(id))return "event";var separator=id.IndexOf('_');return id.Length>2&&id[0]=='e'&&separator>1&&int.TryParse(id[1..separator],out _)?id[(separator+1)..]:id;
    }
    static AtlasNodeKind KindFor(int col,int columns,int expedition,Random rng)
    {
        if(col==0)return AtlasNodeKind.Event;if(col==columns-1)return AtlasNodeKind.Boss;
        var table=col==1?new[]{AtlasNodeKind.Merchant,AtlasNodeKind.Combat,AtlasNodeKind.Scene}:col==columns-2?new[]{AtlasNodeKind.Combat,AtlasNodeKind.Mystery,AtlasNodeKind.Rest,AtlasNodeKind.Character}:new[]{AtlasNodeKind.Combat,AtlasNodeKind.Character,AtlasNodeKind.Event,AtlasNodeKind.Mystery,AtlasNodeKind.Scene};
        if(expedition>=2&&rng.NextDouble()<.18+Math.Min(.28,expedition*.04))return AtlasNodeKind.Combat;return table[rng.Next(table.Length)];
    }
    static (string Title,string Text,string Hint,string Fallback) Spec(AtlasNodeKind kind,Random rng)
    {
        var options=kind switch
        {
            AtlasNodeKind.Merchant=>new[]{("Tenda do mercador","Cartas, rumores e preços que mudam com a lua.","merchant","merchant_tent"),("Bazar de cinza","Um viajante oferece baralhos selados e mapas rasgados.","merchant","merchant_tent")},
            AtlasNodeKind.Combat=>new[]{("Clareira hostil","Algo grande move as raízes à frente.","forest","forest_dark"),("Boca da caverna","Um uivo baixo ecoa na pedra molhada.","cave","cave_chamber"),("Trilha de sangue","Pegadas pesadas atravessam o barro.","street","street")},
            AtlasNodeKind.Character=>new[]{("Encontro na névoa","Alguém espera, como se já soubesse o seu nome.","park","park"),("A ponte velha","Um vulto robusto observa a travessia.","street","street")},
            AtlasNodeKind.Rest=>new[]{("Fogueira escondida","Um descanso breve antes da próxima curva.","camp","camp"),("Clareira segura","O vento baixa. Dá para respirar.","park","park")},
            AtlasNodeKind.Scene=>new[]{("Taverna da última chama","Conversas baixas e olhares discretos.","tavern","tavern"),("Rua da chuva","Lanternas molhadas, portas entreabertas.","street","street")},
            AtlasNodeKind.Mystery=>new[]{("Eco na pedra","Uma voz reconhece o seu nome.","ruin","ruins_hall"),("Poço da lua","A água mostra um caminho que ainda não existe.","park","park")},
            AtlasNodeKind.Boss=>new[]{("Salão das ruínas","A origem do rumor espera além do portão.","ruin","ruins_hall"),("Coração da muralha","A expedição termina onde a pedra ainda respira.","cave","cave_chamber")},
            _=>new[]{("A estrada quebrada","O primeiro passo rumo ao desconhecido.","street","street"),("Caminho antigo","Marcas recentes somem na lama.","forest","forest_dark")}
        };return options[rng.Next(options.Length)];
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
