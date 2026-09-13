using Godot;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Heartbeat;

/// <summary>
/// Downloads AI-generated backgrounds from Pollinations.ai at runtime.
/// Completely free, no API key required. 
/// Cache folder: user://PollinationsCache/
/// </summary>
public static class ImageGenerationService
{
    private static readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };

    private static string CacheDir
    {
        get
        {
            var dir = ProjectSettings.GlobalizePath("user://PollinationsCache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Returns a 1280x720 texture for the given English scene description.
    /// Checks the on-disk cache first; downloads from Pollinations if not found.
    /// Returns null if network fails (caller should keep the existing background).
    /// </summary>
    public static async Task<ImageTexture?> FetchBackgroundAsync(string scenePrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scenePrompt)) return null;

        // Sanitize prompt for cache key and URL
        var safePrompt = string.Concat(scenePrompt.ToLowerInvariant()
            .Replace(' ', '_')
            .Where(c => char.IsLetterOrDigit(c) || c == '_'))
            .Trim('_');

        if (safePrompt.Length > 120) safePrompt = safePrompt[..120];

        var cacheFile = Path.Combine(CacheDir, safePrompt + ".png");

        // ── Cache hit ──────────────────────────────────────────────────────────
        if (File.Exists(cacheFile))
        {
            GD.Print($"[Pollinations] Cache hit: {safePrompt}");
            return LoadPngFromDisk(cacheFile);
        }

        // ── Download ───────────────────────────────────────────────────────────
        // Full prompt appends a stable dark-fantasy style suffix for consistency
        var fullPrompt = Uri.EscapeDataString(
            scenePrompt + ", dark fantasy, medieval, oil painting style, atmospheric lighting, highly detailed");
        var url = $"https://image.pollinations.ai/prompt/{fullPrompt}?width=1280&height=720&nologo=true&seed=42";

        GD.Print($"[Pollinations] Fetching: {url}");
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(cacheFile, bytes, ct);
            GD.Print($"[Pollinations] Saved to cache: {cacheFile}");
            return LoadPngFromBytes(bytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            GD.PushWarning($"[Pollinations] Download failed: {ex.Message}");
            return null;
        }
    }

    private static ImageTexture? LoadPngFromDisk(string path)
    {
        var img = Image.LoadFromFile(path);
        if (img == null) return null;
        return ImageTexture.CreateFromImage(img);
    }

    private static ImageTexture? LoadPngFromBytes(byte[] bytes)
    {
        var img = new Image();
        var err = img.LoadPngFromBuffer(bytes);
        if (err != Error.Ok)
        {
            GD.PushWarning($"[Pollinations] Failed to parse PNG from bytes: {err}");
            return null;
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>
    /// Builds a concise English scene prompt from what the GroqEventProvider already knows about the node.
    /// </summary>
    public static string BuildPromptForNode(AtlasNodeData node, string regionName = "medieval kingdom")
    {
        var kind = node.Kind switch
        {
            AtlasNodeKind.Rest    => "peaceful campfire rest area",
            AtlasNodeKind.Scene   => "dramatic scene encounter",
            AtlasNodeKind.Mystery => "mysterious ancient ruins",
            AtlasNodeKind.Combat  => "dark forest combat clearing",
            AtlasNodeKind.Boss    => "ominous boss lair dungeon",
            AtlasNodeKind.Merchant => "travelling merchant tent market",
            _ => "forest road path"
        };
        return $"{kind}, {regionName}, night time, fog";
    }
}
