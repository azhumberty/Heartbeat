using Godot;
using System;
using System.Collections.Generic;

namespace Heartbeat;

public static class BillboardSprites
{
    static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D? Texture(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        string png = "res://Assets/World/Billboards/$name.png";
        string b64 = png + ".b64";
        string artKitPng = "res://Assets/ArtKit/Billboards/$name.png";
        string artKitB64 = artKitPng + ".b64";
        
        Texture2D? tex = null;
        
        tex = LoadDirect(png);
        if (tex == null && Godot.FileAccess.FileExists(b64)) tex = LoadB64(b64);
        if (tex == null) tex = LoadDirect(artKitPng);
        if (tex == null && Godot.FileAccess.FileExists(artKitB64)) tex = LoadB64(artKitB64);
        
        if (tex != null) Cache[name] = tex;
        return tex;
    }

    static Texture2D? LoadDirect(string path) {
        string global = ProjectSettings.GlobalizePath(path);
        if (System.IO.File.Exists(global)) {
            var bytes = System.IO.File.ReadAllBytes(global);
            return LoadFromBytes(bytes);
        }
        return null;
    }

    static Texture2D? LoadB64(string path)
    {
        string payload = Godot.FileAccess.GetFileAsString(path).Replace("\n", "").Replace("\r", "").Replace(" ", "");
        var bytes = Marshalls.Base64ToRaw(payload);
        return LoadFromBytes(bytes);
    }

    static Texture2D? LoadFromBytes(byte[] bytes)
    {
        var img = new Image();
        if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) {
            if (img.LoadJpgFromBuffer(bytes) == Error.Ok) return ImageTexture.CreateFromImage(img);
        } else {
            if (img.LoadPngFromBuffer(bytes) == Error.Ok) return ImageTexture.CreateFromImage(img);
        }
        return null;
    }

    public static Sprite3D Create(string name, Vector3 position, Vector2 pixelSize, float yaw = 0)
    {
        var tex = Texture(name);
        var sprite = new Sprite3D
        {
            Name = "Billboard_" + name,
            Position = position,
            PixelSize = Math.Max(0.004f, pixelSize.Y / 256f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            AlphaScissorThreshold = 0.1f,
            Texture = tex
        };

        if (tex != null)
        {
            float height = pixelSize.Y;
            sprite.PixelSize = height / Math.Max(1, tex.GetHeight());
            
            // Apply checkerboard removal shader for JPEG artifacts
            if (ResourceLoader.Exists("res://Assets/ArtKit/Billboards/checker_mask.gdshader"))
            {
                var shader = GD.Load<Shader>("res://Assets/ArtKit/Billboards/checker_mask.gdshader");
                var mat = new ShaderMaterial { Shader = shader };
                mat.SetShaderParameter("tex", tex);
                sprite.MaterialOverride = mat;
            }
        }

        sprite.Rotation = new Vector3(0, yaw, 0);
        return sprite;
    }

    public static Sprite3D Attach(Node3D parent, string name, Vector3 position, Vector2 worldSize, float yaw = 0)
    {
        var sprite = Create(name, position, worldSize, yaw);
        parent.AddChild(sprite);
        return sprite;
    }
}
