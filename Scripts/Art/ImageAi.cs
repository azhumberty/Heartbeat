using Godot;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Heartbeat;

public enum ImageKind { Background, Portrait, Special }

public interface IImageAiProvider
{
    Task<byte[]?> GenerateAsync(string prompt, int width, int height, int seed, ImageKind kind, CancellationToken ct);
}

public sealed class OfflineImageProvider : IImageAiProvider
{
    public Task<byte[]?> GenerateAsync(string prompt, int width, int height, int seed, ImageKind kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>(null);
    }
}

/// <summary>Free Pollinations Flux endpoint. No key. Style lock keeps the campaign photoreal and consistent.</summary>
public sealed class PollinationsImageProvider : IImageAiProvider
{
    static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<byte[]?> GenerateAsync(string prompt, int width, int height, int seed, ImageKind kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return null;
        var full = ImageAi.Lock(prompt, kind);
        var url = "https://image.pollinations.ai/prompt/" + Uri.EscapeDataString(full)
                  + $"?width={width}&height={height}&nologo=true&seed={seed}&model=flux";
        GD.Print("[ImageAI] Pollinations " + kind + " seed=" + seed);
        try
        {
            return await Http.GetByteArrayAsync(url, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            GD.PushWarning("[ImageAI] Pollinations falhou: " + e.GetType().Name);
            return null;
        }
    }
}

public static class ImageAi
{
    public const string StyleVersion = "p6-flux-v1";
    public const string StyleLock = "photorealistic cinematic still, medieval dark fantasy, natural skin, volumetric light, film grain, no text, no watermark, no logo, no illustration";

    public static IImageAiProvider From(GameSettings settings) =>
        settings.UseImageAi ? new PollinationsImageProvider() : new OfflineImageProvider();

    public static string Lock(string prompt, ImageKind kind)
    {
        var role = kind switch
        {
            ImageKind.Portrait => "isolated cutout, solid chroma-key green background #00FF00, no scenery, no floor, upper-body or creature, looking toward camera, ",
            ImageKind.Special => "narrative cinematic moment, two-shot or lone figure in place, ",
            _ => "wide establishing environment, no readable signs, "
        };
        return role + prompt.Trim() + ", " + StyleLock;
    }
}

public static class ImageCache
{
    public static string DirectoryFor(ImageKind kind)
    {
        var dir = ProjectSettings.GlobalizePath("user://ImageCache/" + kind);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }

    public static string Key(ImageKind kind, string prompt, int width, int height, int seed)
    {
        var raw = string.Join('|', ImageAi.StyleVersion, kind, width, height, seed, prompt.Trim().ToLowerInvariant());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return hash[..20].ToLowerInvariant();
    }

    public static string PathFor(ImageKind kind, string key) =>
        Path.Combine(DirectoryFor(kind), key + ".png");

    public static ImageTexture? Load(ImageKind kind, string key)
    {
        var path = PathFor(kind, key);
        if (!File.Exists(path)) return null;
        return Decode(File.ReadAllBytes(path));
    }

    public static void Save(ImageKind kind, string key, byte[] bytes, bool cutGreen = false)
    {
        var img = DecodeImage(bytes);
        if (img == null) return;
        if (cutGreen) PunchGreen(img);
        img.SavePng(PathFor(kind, key));
    }

    public static ImageTexture? Decode(byte[] bytes)
    {
        var img = DecodeImage(bytes);
        return img == null ? null : ImageTexture.CreateFromImage(img);
    }

    static Image? DecodeImage(byte[] bytes)
    {
        var img = new Image();
        if (img.LoadPngFromBuffer(bytes) != Error.Ok && img.LoadJpgFromBuffer(bytes) != Error.Ok)
            return null;
        return img;
    }

    public static void PunchGreen(Image img)
    {
        img.Convert(Image.Format.Rgba8);
        var w = img.GetWidth();
        var h = img.GetHeight();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var c = img.GetPixel(x, y);
            float key = c.G - Math.Max(c.R, c.B);
            if (c.G > 0.42f && key > 0.12f)
            {
                c.A *= Math.Clamp(1f - key * 3.2f, 0f, 1f);
                if (c.A < 0.08f) c.A = 0;
                img.SetPixel(x, y, c);
            }
        }
    }
}

public static class CanonicalLook
{
    public static string Ensure(CharacterData c, WorldLore lore)
    {
        if (!string.IsNullOrWhiteSpace(c.CanonicalAppearance)) return c.CanonicalAppearance;
        var age = Math.Max(18, c.Age);
        var job = string.IsNullOrWhiteSpace(c.Profession) ? "traveler" : c.Profession;
        var region = string.IsNullOrWhiteSpace(lore.RegionName) ? "a misty borderland" : lore.RegionName;
        var hair = c.PersonalityProfile.Extraversion < 35 ? "dark unkept hair" : c.PersonalityProfile.Pride > 65 ? "thick beard, short hair" : "shoulder-length brown hair";
        c.CanonicalAppearance = $"adult man, {age} years, {job} from {region}, {hair}, weathered face, {c.Personality}, medieval clothing, distinct silhouette";
        return c.CanonicalAppearance;
    }
}

public static class BackgroundGenerationService
{
    public static string PromptFor(GameSave game, AtlasNodeData node, string? eventPrompt = null)
    {
        var lore = game.WorldLore ?? new WorldLore();
        var kind = node.Kind switch
        {
            AtlasNodeKind.Rest => "quiet campfire clearing at dusk",
            AtlasNodeKind.Scene => "inhabited interior with candlelight",
            AtlasNodeKind.Mystery => "ancient stone ruins with faint runes",
            AtlasNodeKind.Combat => "hostile forest clearing, trampled earth",
            AtlasNodeKind.Boss => "monumental lair, heavy stone, a waiting presence",
            AtlasNodeKind.Merchant => "roadside merchant tent, hanging relics",
            AtlasNodeKind.Character => "foggy path where two people might meet",
            _ => "lonely medieval road in fog"
        };
        var body = string.IsNullOrWhiteSpace(eventPrompt) ? kind : eventPrompt;
        return $"{body}, {lore.RegionName}, {lore.Atmosphere}, distant threat: {lore.Threat}, night";
    }

    public static async Task<ImageTexture?> FetchAsync(GameSave game, AtlasNodeData node, string? eventPrompt, CancellationToken ct)
    {
        var prompt = PromptFor(game, node, eventPrompt);
        var seed = Seed(game.WorldSeed, node.Id);
        var key = ImageCache.Key(ImageKind.Background, prompt, 1280, 720, seed);
        var cached = ImageCache.Load(ImageKind.Background, key);
        if (cached != null)
        {
            Remember(game, "bg:" + node.Id, ImageCache.PathFor(ImageKind.Background, key));
            return cached;
        }
        var bytes = await ImageAi.From(game.Settings).GenerateAsync(prompt, 1280, 720, seed, ImageKind.Background, ct);
        if (bytes == null || bytes.Length < 32) return null;
        ImageCache.Save(ImageKind.Background, key, bytes, false);
        Remember(game, "bg:" + node.Id, ImageCache.PathFor(ImageKind.Background, key));
        return ImageCache.Load(ImageKind.Background, key);
    }

    static void Remember(GameSave game, string id, string path)
    {
        game.ImagePaths ??= new();
        game.ImagePaths[id] = path;
    }

    public static int Seed(long world, string salt)
    {
        unchecked
        {
            int h = (int)(world ^ (world >> 32));
            foreach (var c in salt) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}

public static class PortraitGenerationService
{
    public static async Task<ImageTexture?> EnsureAsync(CharacterData person, GameSave game, CancellationToken ct)
    {
        var look = CanonicalLook.Ensure(person, game.WorldLore ?? new());
        var prompt = $"{look}, isolated cutout, solid chroma-key green background #00FF00, no scenery";
        var seed = BackgroundGenerationService.Seed(game.WorldSeed, "portrait:" + person.Id);
        var key = ImageCache.Key(ImageKind.Portrait, prompt, 768, 1024, seed);
        var cached = ImageCache.Load(ImageKind.Portrait, key);
        if (cached != null)
        {
            person.GeneratedPortraitPath = ImageCache.PathFor(ImageKind.Portrait, key);
            game.ImagePaths ??= new();
            game.ImagePaths["portrait:" + person.Id] = person.GeneratedPortraitPath;
            return cached;
        }
        if (!game.Settings.UseImageAi) return null;
        var bytes = await ImageAi.From(game.Settings).GenerateAsync(prompt, 768, 1024, seed, ImageKind.Portrait, ct);
        if (bytes == null || bytes.Length < 32) return null;
        ImageCache.Save(ImageKind.Portrait, key, bytes, true);
        person.GeneratedPortraitPath = ImageCache.PathFor(ImageKind.Portrait, key);
        game.ImagePaths ??= new();
        game.ImagePaths["portrait:" + person.Id] = person.GeneratedPortraitPath;
        return ImageCache.Load(ImageKind.Portrait, key);
    }

    public static async Task<ImageTexture?> SpecialAsync(CharacterData person, GameSave game, string beat, string scene, CancellationToken ct)
    {
        var look = CanonicalLook.Ensure(person, game.WorldLore ?? new());
        var prompt = $"{look}, {scene}, {game.WorldLore?.RegionName}, cinematic moment";
        var seed = BackgroundGenerationService.Seed(game.WorldSeed, "special:" + person.Id + ":" + beat);
        var key = ImageCache.Key(ImageKind.Special, prompt, 1280, 720, seed);
        var cacheId = "special:" + person.Id + ":" + beat;
        var cached = ImageCache.Load(ImageKind.Special, key);
        if (cached != null)
        {
            game.ImagePaths ??= new();
            game.ImagePaths[cacheId] = ImageCache.PathFor(ImageKind.Special, key);
            return cached;
        }
        if (!game.Settings.UseImageAi) return null;
        var bytes = await ImageAi.From(game.Settings).GenerateAsync(prompt, 1280, 720, seed, ImageKind.Special, ct);
        if (bytes == null || bytes.Length < 32) return null;
        ImageCache.Save(ImageKind.Special, key, bytes, false);
        game.ImagePaths ??= new();
        game.ImagePaths[cacheId] = ImageCache.PathFor(ImageKind.Special, key);
        return ImageCache.Load(ImageKind.Special, key);
    }
}
