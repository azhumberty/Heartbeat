using Godot;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;

namespace Heartbeat;

public partial class WardrobeChecks : Node
{
    void Check(bool condition, string label) { if (!condition) throw new Exception(label); GD.Print("[PASS] " + label); }
    async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    static IEnumerable<Node> Descendants(Node n) { foreach (var c in n.GetChildren()) { yield return c; foreach (var d in Descendants(c)) yield return d; } }
    sealed class RefusalHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"refusal\":\"test restriction\",\"content\":null}}]}") });
    }
    public override async void _Ready()
    {
        var id = "wardrobe-qa-" + Guid.NewGuid().ToString("N");
        var slot = Random.Shared.Next(10000000, 20000000);
        var previousKey = System.Environment.GetEnvironmentVariable("GROQ_API_KEY");
        try
        {
            var character = new CharacterData { Id = id, Name = "Teste de guarda-roupa", Age = 28 };
            Wardrobe.Migrate(character);
            Check(character.Outfits.Single().UseLegacy, "Migração mantém visual antigo");
            var outfit = new CharacterOutfit { Name = "Casual" }; character.Outfits.Add(outfit); character.DefaultOutfitId = outfit.Id;
            var root = ProjectSettings.GlobalizePath("res://.qa-cache"); Directory.CreateDirectory(root);
            using (var source = Image.CreateEmpty(128, 256, false, Image.Format.Rgba8))
            {
                source.Fill(Colors.Transparent);
                for (int y = 20; y < 242; y++) for (int x = 34; x < 94; x++)
                    source.SetPixel(x, y, y < 55 ? new Color("d9b599") : new Color("749aae"));
                var path = Path.Combine(root, "wardrobe-fixture.png"); source.SavePng(path);
                outfit.Idle = Wardrobe.Import(character, path);
                outfit.Left = Wardrobe.Import(character, path); outfit.Right = Wardrobe.Import(character, path);
                outfit.Left.X = -.02f; outfit.Right.X = .02f;
            }
            var originalHash = SHA256.HashData(File.ReadAllBytes(Wardrobe.PathFor(character, outfit.Idle.Original)));
            using var baked = Wardrobe.Bake(character, outfit, outfit.Idle);
            Check(baked.GetWidth() == 512 && baked.GetHeight() == 1024 && baked.GetPixel(0, 0).A == 0, "Tela comum preserva transparência");
            var mask = new CutoutCanvas(); AddChild(mask);
            using (var original = Wardrobe.Read(character, outfit.Idle.Original)!)
            using (var cutout = Wardrobe.Read(character, outfit.Idle.Cutout)!)
            {
                mask.LoadImages(original, cutout); mask.BeginStroke(); mask.PaintAt(new(64, 100), 12); mask.SaveTo(character, outfit.Idle);
                using var erased = Wardrobe.Read(character, outfit.Idle.Cutout)!;
                Check(erased.GetPixel(64, 100).A == 0, "Pincel apaga máscara");
                mask.Undo(); mask.SaveTo(character, outfit.Idle);
                using var undone = Wardrobe.Read(character, outfit.Idle.Cutout)!;
                Check(undone.GetPixel(64, 100).A == 1, "Desfazer recupera recorte");
                mask.BeginStroke(); mask.PaintAt(new(64, 100), 12); mask.Restore = true; mask.PaintAt(new(64, 100), 12); mask.SaveTo(character, outfit.Idle);
                using var restored = Wardrobe.Read(character, outfit.Idle.Cutout)!;
                Check(restored.GetPixel(64, 100) == original.GetPixel(64, 100), "Restaurar recupera pixels originais");
            }
            mask.QueueFree();
            Check(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Wardrobe.PathFor(character, outfit.Idle.Original)))), "Original permanece intacto");
            try { await new LocalCutoutService { PythonPath = "missing-python-test.exe" }.RemoveAsync("unused", root, CancellationToken.None); throw new Exception("Missing helper not detected"); }
            catch (InvalidOperationException) { Check(File.Exists(Wardrobe.PathFor(character, outfit.Idle.Cutout)), "Ferramenta ausente não impede PNG transparente"); }
            var repo = new CharacterRepository(); repo.Save(character);
            var loaded = repo.Load(character.Id)!;
            Check(loaded.Outfits[1].Idle.Mask.Length > 0 && loaded.Outfits[1].Left.X == -.02f, "Pacote reabre com máscara e ajustes");
            var state = new CharacterState { CurrentOutfitId = outfit.Id, Affection = 45, Trust = 40 };
            var game = new GameSave { CharacterIds = new() { id }, CharacterStates = new() { [id] = state }, WorldSeed = 42, ActionsLeft = 0 };
            var saver = new SaveManager(); saver.Save(game, slot);
            var read = saver.Load(slot)!;
            Check(read.CharacterStates[id].CurrentOutfitId == outfit.Id && read.CharacterStates[id].Affection == 45, "Save mantém roupa e relacionamento");
            var actor = new NpcActor { Data = character, State = state }; AddChild(actor); await Frames();
            var animator = actor.GetChildren().OfType<NpcAnimator>().Single();
            Check(animator.UsesOutfit, "Mundo usa roupa simplificada");
            var editor = new WardrobeEditor { Data = character, CurrentOutfitId = outfit.Id }; AddChild(editor); await Frames();
            var preview = Descendants(editor).OfType<NpcAnimator>().Single();
            Check(preview.UsesOutfit && preview.ResolvedOutfitId == animator.ResolvedOutfitId, "Preview e mundo usam mesma roupa e componente");
            animator.SetState("Walking", 1.4f); var frames = new HashSet<int>();
            for (int i = 0; i < 60; i++) { await Frames(); frames.Add(animator.CurrentFrame); }
            Check(frames.Contains(1) && frames.Contains(2), "Caminhada alterna os dois passos");
            animator.SetState("Idle"); await Frames(); Check(animator.CurrentFrame == 0, "Parar volta à pose neutra");
            var single = Wardrobe.Duplicate(outfit); single.Name = "Uma imagem"; single.Left = new(); single.Right = new(); character.Outfits.Add(single);
            animator.OutfitId = single.Id; animator.Reload(); animator.SetState("Walking", 1.4f); await Frames();
            Check(animator.UsesOutfit && animator.CurrentFrame == 0 && state.Affection == 45, "Uma imagem funciona e troca visual não muda afeto");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); GetViewport().GetTexture().GetImage().SavePng("res://.qa-cache/wardrobe-preview.png"); }
            editor.QueueFree(); await Frames();
            var offline = new ProceduralDialogueProvider().Reply(character, state, "Você é bonito, posso flertar?");
            Check(offline.Emotion == "flirty" && offline.RomanceDelta > 0, "Flertes offline reagem ao relacionamento");
            var limits = new ProceduralDialogueProvider().Reply(character, state, "não quero, vamos devagar");
            Check(limits.Desire == "talk", "Fallback respeita limites");
            System.Environment.SetEnvironmentVariable("GROQ_API_KEY", "qa-placeholder");
            using var handler = new RefusalHandler();
            var refusal = await new GroqDialogueProvider(handler).ReplyAsync(character, state, "Olá", new());
            Check(refusal.ProviderStatus.Contains("Restrição") && refusal.Dialogue.Length > 0, "Restrição simulada do provedor tem resposta e identificação offline");
            await ProviderChecks.Run();
            var cutoutArgument = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--cutout="));
            if (cutoutArgument != null)
            {
                var source = ProjectSettings.GlobalizePath(cutoutArgument["--cutout=".Length..]);
                var service = new LocalCutoutService();
                var output = await service.RemoveAsync(source, root, CancellationToken.None);
                using var actual = Image.LoadFromFile(output);
                Check(actual.DetectAlpha() != Image.AlphaMode.None && actual.GetUsedRect().Size.Y > 0, "Removedor real produz alpha e mantém personagem");
                var stamp = File.GetLastWriteTimeUtc(output);
                Check(await service.RemoveAsync(source, root, CancellationToken.None) == output && File.GetLastWriteTimeUtc(output) == stamp, "Recorte real reutiliza cache");
                actual.SavePng("res://.qa-cache/cutout-photo-preview.png");
            }
            var dialog = new DialogueController { Game = game, Actor = actor }; AddChild(dialog); await Frames();
            Descendants(dialog).OfType<TextEdit>().Single().Text = "Você é bonito";
            Descendants(dialog).OfType<Button>().First(b => b.Text == "Enviar").EmitSignal(Button.SignalName.Pressed); await Frames(10);
            Check(game.ActionsLeft == 0 && state.Affection == 45 && state.Conversation.Count == 2, "Sem ações: conversa responde sem progresso extra");
            GD.Print("[WARDROBE] PASS"); GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr("[WARDROBE] FAIL " + e); GetTree().Quit(1); }
        finally
        {
            System.Environment.SetEnvironmentVariable("GROQ_API_KEY", previousKey);
            // Only remove this run's uniquely named fixture package and slot.
            var folder = ProjectSettings.GlobalizePath($"user://Characters/{id}");
            if (id.StartsWith("wardrobe-qa-") && Directory.Exists(folder)) Directory.Delete(folder, true);
            var savePath = ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.json");
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }
}
