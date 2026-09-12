using Godot;
namespace Heartbeat;

/// <summary>
/// Shared Octopath/Doom-style Sprite3D billboards. Textures load from
/// res://Assets/World/Billboards/{name}.png when present, otherwise from
/// {name}.png.b64 (base64 sidecars — used when GitHub MCP cannot push raw binary).
/// </summary>
public static class BillboardSprites
{
    static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D? Texture(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        string png = $"res://Assets/World/Billboards/{name}.png";
        string b64 = png + ".b64";
        string artKitPng = $"res://Assets/ArtKit/Billboards/{name}.png";
        string artKitB64 = artKitPng + ".b64";
        Texture2D? tex = null;
        if (ResourceLoader.Exists(png)) tex = GD.Load<Texture2D>(png);
        else if (Godot.FileAccess.FileExists(b64)) tex = LoadB64(b64);
        else if (ResourceLoader.Exists(artKitPng)) tex = GD.Load<Texture2D>(artKitPng);
        else if (Godot.FileAccess.FileExists(artKitB64)) tex = LoadB64(artKitB64);
        
        if (tex != null) Cache[name] = tex;
        return tex;
    }

    static Texture2D? LoadB64(string path)
    {
        string payload = Godot.FileAccess.GetFileAsString(path).StripEdges();
        var bytes = Marshalls.Base64ToRaw(payload);
        var image = new Image();
        if (image.LoadPngFromBuffer(bytes) == Error.Ok)
            return ImageTexture.CreateFromImage(image);
        return null;
    }

    public static Sprite3D Create(string name, Vector3 position, Vector2 pixelSize, float yaw = 0)
    {
        var sprite = new Sprite3D
        {
            Name = "Billboard_" + name,
            Position = position,
            PixelSize = Math.Max(0.004f, pixelSize.Y / 256f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Transparent = true,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            AlphaScissorThreshold = 0.12f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Texture = Texture(name),
            Modulate = Colors.White
        };
        if (sprite.Texture != null)
        {
            float height = pixelSize.Y;
            sprite.PixelSize = height / Math.Max(1, sprite.Texture.GetHeight());
        }
        sprite.RotateY(yaw);
        return sprite;
    }

    public static Sprite3D Attach(Node3D parent, string name, Vector3 position, Vector2 worldSize, float yaw = 0)
    {
        var sprite = Create(name, position, worldSize, yaw);
        if (sprite.Texture != null)
            sprite.PixelSize = worldSize.Y / Math.Max(1, sprite.Texture.GetHeight());
        parent.AddChild(sprite);
        return sprite;
    }
}
