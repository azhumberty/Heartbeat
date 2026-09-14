using Godot;

namespace Heartbeat;

/// <summary>Legacy wrapper. New code should use BackgroundGenerationService.</summary>
public static class ImageGenerationService
{
    public static Task<ImageTexture?> FetchBackgroundAsync(string scenePrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scenePrompt)) return Task.FromResult<ImageTexture?>(null);
        return Fetch(scenePrompt, ct);
    }

    static async Task<ImageTexture?> Fetch(string scenePrompt, CancellationToken ct)
    {
        var seed = 42;
        var key = ImageCache.Key(ImageKind.Background, scenePrompt, 1280, 720, seed);
        var cached = ImageCache.Load(ImageKind.Background, key);
        if (cached != null) return cached;
        var bytes = await new PollinationsImageProvider().GenerateAsync(scenePrompt, 1280, 720, seed, ImageKind.Background, ct);
        if (bytes == null || bytes.Length < 32) return null;
        ImageCache.Save(ImageKind.Background, key, bytes);
        return ImageCache.Load(ImageKind.Background, key);
    }

    public static string BuildPromptForNode(AtlasNodeData node, string regionName = "medieval kingdom") =>
        BackgroundGenerationService.PromptFor(new GameSave { WorldLore = new WorldLore { RegionName = regionName } }, node);
}
