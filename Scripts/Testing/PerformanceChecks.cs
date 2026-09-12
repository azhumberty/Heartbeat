using Godot;
using System.Diagnostics;
namespace Heartbeat;

public partial class PerformanceChecks : Node
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); GD.Print("[PASS] " + message); }
    static IEnumerable<Node> Descendants(Node root) { foreach (var child in root.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            var world = new WorldController { InitialSave = new GameSave { WorldSeed = 73021, WorldMinutes = 13 * 60 } };
            AddChild(world);
            await Frames(45);
            var player = Descendants(world).OfType<PlayerController>().Single();
            var chunks = Descendants(world).OfType<ChunkManager>().Single();

            var timer = Stopwatch.StartNew();
            await Frames(180);
            timer.Stop();
            double averageFps = 180d / Math.Max(.001, timer.Elapsed.TotalSeconds);
            GD.Print($"[PERFORMANCE] média após aquecimento: {averageFps:0.0} FPS");
            Check(averageFps >= 30, "Cena procedural mantém pelo menos 30 FPS no computador de teste");

            player.Position = new Vector3(ChunkGenerator.ChunkSize + 2, TerrainHeight.Sample(ChunkGenerator.ChunkSize + 2, 4, 73021) + .2f, 4);
            await Frames(90);
            Check(chunks.IsChunkLoaded(new Vector2I(1, 0)), "Streaming carrega o chunk vizinho durante a travessia");
            Check(Descendants(world).OfType<MultiMeshInstance3D>().Count() >= 6, "Vegetação distante continua agrupada em MultiMesh");
            GD.Print("[PERFORMANCE] PASS");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr("[PERFORMANCE] FAIL: " + e); GetTree().Quit(1); }
    }
}
