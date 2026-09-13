using Godot;

namespace Heartbeat;

/// <summary>Headless integration check for the creative library and procedural Atlas.</summary>
public partial class CampaignChecks : Node
{
    public override void _Ready()
    {
        try
        {
            var assets=new ContentLibrary().Load();
            Require(assets.Any(a=>a.Category=="Cenários"&&a.Enabled),"Nenhum cenário ativo.");
            Require(assets.Any(a=>a.Category=="Inimigos"&&a.Enabled),"Nenhum inimigo ativo.");
            var atlas=AtlasGenerator.Create(20260912);
            Require(atlas.Count>=7,"Atlas incompleto.");
            Require(atlas.Count(n=>n.Status==AtlasNodeStatus.Available)==1,"Início do Atlas inválido.");
            Require(atlas.All(n=>n.Connections.All(id=>atlas.Any(other=>other.Id==id))),"Atlas contém conexão inexistente.");
            Require(atlas.All(n=>!string.IsNullOrWhiteSpace(n.BackgroundId)),"Nó sem cenário.");
            foreach(var combat in atlas.Where(n=>n.Kind is AtlasNodeKind.Combat or AtlasNodeKind.Boss))
                _=EnemyDefinition.Get(string.IsNullOrWhiteSpace(combat.ContentId)?"forest":combat.ContentId);
            var save=new GameSave {WorldSeed=20260912,AtlasNodes=atlas};
            var story=new ProceduralEventService().Create(new EventContext {Game=save,Node=atlas[0],Assets=assets});
            Require(story.Choices.Count is >=2 and <=4,"Evento sem escolhas válidas.");
            int ownedBefore=save.Deck.Owned.Count;
            _=new ProceduralEventService().Apply(save,story,story.Choices[0]);
            Require(save.RecentEvents.Count==1,"Escolha de evento não foi persistida.");
            Require(save.Deck.Owned.Count==ownedBefore,"Evento concedeu carta indevidamente.");
            Require(ChromaArt.LoadArt("UI/Frames/menu_background.png")!=null,"Arte do menu indisponível.");
            GD.Print($"CAMPAIGN_CHECKS_PASS assets={assets.Count} nodes={atlas.Count}");
            GetTree().Quit();
        }
        catch(Exception e)
        {
            GD.PushError("CAMPAIGN_CHECKS_FAIL "+e.Message);
            GetTree().Quit(1);
        }
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
