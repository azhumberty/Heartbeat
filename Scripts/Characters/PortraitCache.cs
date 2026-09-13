using Godot;
namespace Heartbeat;

public sealed class PortraitCache
{
    readonly Dictionary<string, Texture2D> _cache = new();

    /// <summary>
    /// Get a portrait texture for a character. Applies auto-crop for portrait images only.
    /// For sprite sheets, use LoadSheet() instead which preserves full image.
    /// </summary>
    public Texture2D Get(CharacterData c, CharacterState? state = null, bool cinematic = false)
    {
        var outfit = Wardrobe.Resolve(c, state?.CurrentOutfitId);
        if (!cinematic && outfit != null && !outfit.UseLegacy && !string.IsNullOrEmpty(outfit.Idle.Cutout))
        {
            var texture = LoadRaw(Wardrobe.PathFor(c, outfit.Idle.Cutout));
            if (texture != null) return texture;
        }
        var expression = new ExpressionResolver();
        var entry = cinematic ? c.Images.FirstOrDefault(i => i.Tags.Contains("special")) : expression.ImageFor(c, state == null ? "neutral" : expression.Resolve(c, state));
        if(state==null && !cinematic) entry=c.Images.FirstOrDefault(i=>i.ProcessedPath==c.MainImagePath)??entry;
        entry ??= c.Images.FirstOrDefault(i => i.ProcessedPath == c.MainImagePath) ?? c.Images.FirstOrDefault(i => !i.Tags.Contains("special"));
        foreach (var path in new[] { entry?.ProcessedPath, entry?.OriginalPath })
        {
            if (string.IsNullOrEmpty(path)) continue;
            if (_cache.TryGetValue(path, out var cached)) return cached;
            var full = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(full)) continue;
            using var image = ImageManager.LoadImage(full); if (image == null || image.IsEmpty()) continue;
            // Auto-crop is valid for portrait images (removes transparent margins)
            var rect = image.GetUsedRect(); if (rect.Size.X > 0 && rect.Size.Y > 0) { using var cropped = image.GetRegion(rect); return _cache[path] = ImageTexture.CreateFromImage(cropped); }
        }
        return GetPlaceholder(c);
    }

    /// <summary>
    /// Load a sprite sheet image WITHOUT auto-crop.
    /// Preserves all cells and pivot consistency.
    /// </summary>
    public Texture2D? LoadSheet(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var cacheKey = "sheet:" + path;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var full = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(full)) return null;

        using var image = ImageManager.LoadImage(full);
        if (image == null || image.IsEmpty()) return null;

        // NO crop — preserve entire sheet for consistent cell division
        var tex = ImageTexture.CreateFromImage(image);
        _cache[cacheKey] = tex;
        return tex;
    }

    /// <summary>
    /// Load an image by hash-based cache key without any crop.
    /// Returns null if file not found.
    /// </summary>
    public Texture2D? LoadRaw(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath)) return null;
        var cacheKey = "raw:" + absolutePath;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        using var image = ImageManager.LoadImage(absolutePath);
        if (image == null || image.IsEmpty()) return null;
        var tex = ImageTexture.CreateFromImage(image);
        _cache[cacheKey] = tex;
        return tex;
    }

    /// <summary>Generates a simple placeholder silhouette for characters without images.</summary>
    Texture2D GetPlaceholder(CharacterData c)
    {
        var key = "placeholder:" + c.Id;
        if (_cache.TryGetValue(key, out var placeholder)) return placeholder;
        using var img = Image.CreateEmpty(128, 256, false, Image.Format.Rgba8); img.Fill(Colors.Transparent);
        var color = c.Id.Contains("lucas") ? new Color("6798c5") : new Color("b7799d");
        for (int y = 0; y < 256; y++) for (int x = 0; x < 128; x++)
        {
            bool head = new Vector2(x-64,y-33).Length() < 25;
            bool body = y > 61 && y < 160 && Math.Abs(x-64) < 37;
            bool legs = y >= 155 && y < 255 && (Math.Abs(x-45)<15 || Math.Abs(x-83)<15);
            if (head || body || legs) img.SetPixel(x,y,head ? new Color("d7b8ad") : color);
        }
        return _cache[key] = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Clear all cached textures.</summary>
    public void ClearCache() => _cache.Clear();
}
