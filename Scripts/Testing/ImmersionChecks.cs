using Godot;
namespace Heartbeat;

public partial class ImmersionChecks : Node
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); GD.Print("[PASS] " + message); }
    static IEnumerable<Node> Descendants(Node root) { foreach (var child in root.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    async Task PhysicsFrames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        try
        {
            var game = new GameSave { WorldSeed = 52, WorldMinutes = 17 * 60 + 50, Period = "Afternoon", ActionsLeft = 2 };
            var world = new WorldController { InitialSave = game }; AddChild(world); await Frames(15);
            var clock = Descendants(world).OfType<WorldTimeSystem>().Single();
            var sky = Descendants(world).OfType<SkyController>().Single();
            var player = Descendants(world).OfType<PlayerController>().Single();
            Check(clock.Hour > 17.8f && clock.Hour < 18.1f, "Relógio contínuo inicia no horário salvo");
            clock.Paused = true; var before = game.WorldMinutes; await Frames(10); Check(Math.Abs(game.WorldMinutes - before) < .01f, "Relógio pode ser pausado");
            clock.Paused = false; clock.SkipHours(1); Check(game.Period == "Evening", "Mudança de período ocorre pelo relógio central");
            Check(sky.GetChildren().Count(n => n.Name.ToString().StartsWith("CloudLayer")) == 2, "Céu contém duas camadas de nuvens otimizadas");
            Check(Descendants(sky).OfType<MultiMeshInstance3D>().Count() >= 2, "Horizonte florestal e estrelas usam instâncias leves");
            Check(player.FirstPerson && player.ActiveCamera.Current, "Jogo inicia e permanece em primeira pessoa");
            Check(!Descendants(player).OfType<MeshInstance3D>().Any(), "Jogador não renderiza corpo, braços ou mãos");
            Check(Descendants(world).OfType<MultiMeshInstance3D>().Any(n => n.Name.ToString().StartsWith("Forest")), "Vegetação da floresta usa MultiMesh");
            Check(Descendants(world).OfType<MeshInstance3D>().Any(n => n.Name == "NaturalTerrain"), "Chunks usam terreno contínuo com relevo");
            Check(Descendants(world).OfType<MeshInstance3D>().Any(n => n.Name == "ForestTrail"), "Trilha acompanha o relevo da floresta");
            Check(Godot.FileAccess.FileExists("res://Assets/Materials/forest_floor/forest_floor_diff_1k.jpg"), "Textura PBR CC0 do solo está presente");
            Check(Descendants(world).OfType<ProgressBar>().Count() == 2, "HUD permanente contém apenas barras de Vida e Mana");
            Check(Descendants(world).OfType<ForestPoi>().Any(n => n.Kind == "Cabin"), "Chunk inicial contém prefab completo de cabana");
            Check(!Descendants(world).OfType<Label3D>().Any(label => label.GetParent() is not NpcActor), "POIs não contêm nomes 3D flutuantes");
            var target = new NpcActor { Data = new CharacterData { Name = "Alvo" }, State = new CharacterState(), Position = player.Position + new Vector3(0, 0, -2) };
            world.AddChild(target); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(player.AimedNpc() == target, "Raycast central identifica NPC em primeira pessoa");
            target.QueueFree();
            await PhysicsFrames(45);
            float jumpStart = player.Position.Y;
            player._UnhandledInput(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
            await PhysicsFrames(2);
            Check(player.Velocity.Y > 0 && player.Position.Y > jumpStart, "Espaço executa salto apenas a partir do chão");
            game.FirstPerson = true; game.PlayerRotationY = .7f; new SaveManager().Migrate(game);
            Check(game.SaveVersion == 5 && game.Player.Health == 100 && game.Player.Mana == 80, "Save v5 preserva Vida e Mana do jogador");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://.qa-cache"));
                clock.Paused = true; game.WorldMinutes = 17 * 60 + 20;
                await Frames(10); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://.qa-cache/immersion-preview.png");
            }
            GD.Print("[IMMERSION] PASS"); GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr("[IMMERSION] FAIL: " + e); GetTree().Quit(1); }
    }
}
