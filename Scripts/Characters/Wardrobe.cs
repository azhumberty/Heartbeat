using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace Heartbeat;

public sealed class OutfitPose
{
    public string Original { get; set; } = "";
    public string Cutout { get; set; } = "";
    public string Mask { get; set; } = "";
    public string AutoCutout { get; set; } = "";
    public float Scale { get; set; } = 1;
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class CharacterOutfit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nova roupa";
    public bool UseLegacy { get; set; }
    public OutfitPose Idle { get; set; } = new();
    public OutfitPose Left { get; set; } = new();
    public OutfitPose Right { get; set; } = new();
    public float FootOffset { get; set; }
    public float Fps { get; set; } = 4;
    public float Breathing { get; set; } = 0.003f;
    [System.Text.Json.Serialization.JsonIgnore] public IEnumerable<OutfitPose> Poses => new[] { Idle, Left, Right };
}

public static class Wardrobe
{
    public static void Migrate(CharacterData data)
    {
        data.Outfits ??= new();
        if (data.Outfits.Count == 0)
        {
            var legacy = new CharacterOutfit { Id = "legacy", Name = "Padrão", UseLegacy = true };
            data.Outfits.Add(legacy);
        }
        if (!data.Outfits.Any(o => o.Id == data.DefaultOutfitId)) data.DefaultOutfitId = data.Outfits[0].Id;
    }

    public static CharacterOutfit? Resolve(CharacterData data, string? id = null)
    {
        Migrate(data);
        return data.Outfits.FirstOrDefault(o => o.Id == (string.IsNullOrEmpty(id) ? data.DefaultOutfitId : id))
            ?? data.Outfits.FirstOrDefault(o => o.Id == data.DefaultOutfitId) ?? data.Outfits.FirstOrDefault();
    }

    public static string PathFor(CharacterData data, string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (path.StartsWith("user://") || path.StartsWith("res://")) return ProjectSettings.GlobalizePath(path);
        if (Path.IsPathRooted(path)) return path; // compatibility with existing packages
        var root = Path.GetFullPath(ProjectSettings.GlobalizePath($"user://Characters/{data.Id}"));
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Caminho fora do pacote.");
        if (File.Exists(full)) return full;
        var bundled = ProjectSettings.GlobalizePath($"res://Characters/{data.Id}/{path}");
        return File.Exists(bundled) ? bundled : full;
    }

    public static Image? Read(CharacterData data, string path)
    {
        var full = PathFor(data, path);
        if (!File.Exists(full)) return null;
        var image = Image.LoadFromFile(full);
        if (image == null || image.IsEmpty()) { image?.Dispose(); return null; }
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    public static string Store(CharacterData data, Image image, string folder)
    {
        var bytes = image.SavePngToBuffer();
        return StoreBytes(data, bytes, folder, ".png");
    }

    public static string StoreBytes(CharacterData data, byte[] bytes, string folder, string extension)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var relative = $"wardrobe/{folder}/{hash}{extension}";
        var full = PathFor(data, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        if (!File.Exists(full)) File.WriteAllBytes(full, bytes);
        return relative;
    }

    public static OutfitPose Import(CharacterData data, string source)
    {
        if (new FileInfo(source).Length > 40_000_000) throw new ArgumentException("Use uma imagem menor que 40 MB.");
        using var image = Image.LoadFromFile(source);
        if (image == null || image.IsEmpty() || image.GetWidth() > 8192 || image.GetHeight() > 8192)
            throw new ArgumentException("Imagem inválida ou maior que 8192 pixels.");
        image.Convert(Image.Format.Rgba8);
        var original = StoreBytes(data, File.ReadAllBytes(source), "originals", Path.GetExtension(source).ToLowerInvariant());
        LimitWorkingSize(image);
        var cutout = Store(data, image, "cutouts");
        return new OutfitPose { Original = original, Cutout = cutout, AutoCutout = cutout };
    }

    public static void LimitWorkingSize(Image image)
    {
        float ratio = Math.Min(1, 1600f / Math.Max(image.GetWidth(), image.GetHeight()));
        if (ratio < 1) image.Resize(Math.Max(1, (int)(image.GetWidth() * ratio)), Math.Max(1, (int)(image.GetHeight() * ratio)), Image.Interpolation.Lanczos);
    }

    public static CharacterOutfit Duplicate(CharacterOutfit source)
    {
        var copy = JsonSerializer.Deserialize<CharacterOutfit>(JsonSerializer.Serialize(source))!;
        copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " (cópia)";
        return copy; // immutable, content-addressed files can safely be shared
    }

    // A common transform is derived ONLY from neutral. Poses retain their relative
    // margins and lifted feet. Canvas dimensions are normalized before that transform.
    public static Image Bake(CharacterData data, CharacterOutfit outfit, OutfitPose pose)
    {
        using var neutral = Read(data, outfit.Idle.Cutout) ?? throw new ArgumentException("Adicione a imagem Parado.");
        using var source = Read(data, pose.Cutout) ?? throw new ArgumentException("Imagem da pose ausente.");
        var bounds = neutral.GetUsedRect();
        if (bounds.Size.Y <= 0) throw new ArgumentException("Recorte totalmente transparente.");
        var ratio = (float)neutral.GetHeight() / source.GetHeight();
        var fit = Math.Min(900f / bounds.Size.Y, 460f / Math.Max(1, bounds.Size.X));
        var scale = Math.Clamp(pose.Scale, .5f, 1.5f) * fit * ratio;
        var width = Math.Max(1, (int)(source.GetWidth() * scale));
        var height = Math.Max(1, (int)(source.GetHeight() * scale));
        if (width > 8192 || height > 8192) throw new ArgumentException("Recorte pequeno demais. Restaure ou ajuste a imagem.");
        source.Resize(width, height, Image.Interpolation.Lanczos);
        var target = Image.CreateEmpty(512, 1024, false, Image.Format.Rgba8); target.Fill(Colors.Transparent);
        var cx = bounds.Position.X + bounds.Size.X / 2f;
        var baseline = bounds.End.Y;
        target.BlitRect(source, new Rect2I(0, 0, width, height), new Vector2I(
            (int)(256 - cx * fit * pose.Scale + pose.X * 512),
            (int)(962 - baseline * fit * pose.Scale + pose.Y * 1024)));
        return target;
    }

    public static float BodyFraction(CharacterData data, CharacterOutfit outfit)
    {
        using var neutral = Read(data, outfit.Idle.Cutout);
        if (neutral == null) return .88f;
        var bounds = neutral.GetUsedRect();
        var fit = Math.Min(900f / Math.Max(1, bounds.Size.Y), 460f / Math.Max(1, bounds.Size.X));
        return Math.Max(.1f, bounds.Size.Y * fit / 1024f);
    }
}
