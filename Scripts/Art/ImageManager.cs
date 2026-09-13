using System.Collections.Generic;
using Godot;

/// <summary>
/// VN/ArtKit texture loader with missing-asset fallback.
/// Autoload as ImageManager. Prefer this over ResourceLoader.Load&lt;Texture2D&gt; for ArtKit paths.
/// </summary>
public partial class ImageManager : Node
{
	public const string ArtKitRoot = "res://Assets/ArtKit/";
	private static ImageManager? _instance;
	private readonly Dictionary<string, Texture2D> _cache = new();
	private Texture2D? _missingPlaceholder;

	public static ImageManager? Instance => _instance;

	public override void _Ready()
	{
		_instance = this;
		_missingPlaceholder = BuildMissingPlaceholder();
	}

	public override void _ExitTree()
	{
		if (_instance == this)
			_instance = null;
	}

	/// <summary>
	/// Load by relative ArtKit path, e.g. "Interiors/tavern.png" or "Backgrounds/camp.png".
	/// Missing files return a gray "IMAGE MISSING" texture and log a warning (no crash).
	/// </summary>
	public Texture2D GetArt(string relativeOrLogicalPath)
	{
		if (string.IsNullOrWhiteSpace(relativeOrLogicalPath))
		{
			GD.PushWarning("[ImageManager] Empty path requested â€” returning IMAGE MISSING placeholder.");
			return _missingPlaceholder!;
		}

		var key = NormalizeKey(relativeOrLogicalPath);
		if (_cache.TryGetValue(key, out var cached) && cached != null && GodotObject.IsInstanceValid(cached))
			return cached;

		foreach (var candidate in BuildCandidates(key))
		{
			var globalPath = ProjectSettings.GlobalizePath(candidate);
			// Grok assets are JPEG payloads kept under historical .png names.
			// Load by signature before ResourceLoader so Godot does not emit a false corrupt-PNG error.
			if (System.IO.File.Exists(globalPath) && IsJpeg(globalPath))
			{
				using var image = LoadImage(globalPath);
				if (image != null)
				{
					var texture = ImageTexture.CreateFromImage(image);
					_cache[key] = texture;
					return texture;
				}
			}
			if (ResourceLoader.Exists(candidate))
			{
				var tex = ResourceLoader.Load<Texture2D>(candidate);
				if (tex != null)
				{
					_cache[key] = tex;
					return tex;
				}
			}
			
			// Fallback: Raw load for dynamically generated images by AI agents
			if (System.IO.File.Exists(globalPath))
			{
				using var img = LoadImage(globalPath);
				if (img != null)
				{
					var tex = ImageTexture.CreateFromImage(img);
					_cache[key] = tex;
					return tex;
				}
			}
		}

		GD.PushWarning($"[ImageManager] IMAGE MISSING: '{relativeOrLogicalPath}' (tried under {ArtKitRoot}). Using placeholder.");
		_cache[key] = _missingPlaceholder;
		return _missingPlaceholder!;
	}

	public static Image? LoadImage(string absolutePath)
	{
		if (!System.IO.File.Exists(absolutePath)) return null;
		if (!IsJpeg(absolutePath)) return Image.LoadFromFile(absolutePath);
		var image = new Image();
		if (image.LoadJpgFromBuffer(System.IO.File.ReadAllBytes(absolutePath)) == Error.Ok) return image;
		image.Dispose();
		return null;
	}

	static bool IsJpeg(string absolutePath)
	{
		using var stream = System.IO.File.OpenRead(absolutePath);
		return stream.Length >= 3 && stream.ReadByte() == 0xff && stream.ReadByte() == 0xd8 && stream.ReadByte() == 0xff;
	}

	/// <summary>Convenience: Interiors/Backgrounds lookup by bare filename (e.g. tavern.png).</summary>
	public Texture2D GetBackground(string fileName)
	{
		var name = fileName.EndsWith(".png") ? fileName : fileName + ".png";
		var tex = GetArt("Backgrounds/" + name);
		if (!ReferenceEquals(tex, _missingPlaceholder))
			return tex;
		_cache.Remove(NormalizeKey("Backgrounds/" + name));
		return GetArt("Interiors/" + name);
	}

	public Texture2D GetCharacter(string relativeUnderCharacters)
	{
		var rel = relativeUnderCharacters.Replace('\\', '/').TrimStart('/');
		if (!rel.StartsWith("Characters/"))
			rel = "Characters/" + rel;
		return GetArt(rel);
	}

	private static string NormalizeKey(string path)
	{
		var p = path.Replace('\\', '/').Trim();
		if (p.StartsWith(ArtKitRoot))
			p = p[ArtKitRoot.Length..];
		if (p.StartsWith("res://"))
			p = p["res://".Length..];
		if (p.StartsWith("Assets/ArtKit/"))
			p = p["Assets/ArtKit/".Length..];
		return p.TrimStart('/');
	}

	private static IEnumerable<string> BuildCandidates(string key)
	{
		yield return ArtKitRoot + key;
		if (!key.EndsWith(".png"))
			yield return ArtKitRoot + key + ".png";
		// bare name helpers
		var file = key.Contains('/') ? key[(key.LastIndexOf('/') + 1)..] : key;
		if (!file.EndsWith(".png"))
			file += ".png";
		yield return ArtKitRoot + "Backgrounds/" + file;
		yield return ArtKitRoot + "Interiors/" + file;
		yield return ArtKitRoot + "Characters/" + file;
	}

	private static Texture2D BuildMissingPlaceholder()
	{
		const int w = 640;
		const int h = 360;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0.12f, 0.12f, 0.14f, 1f));

		// Simple pixel-font banner "IMAGE MISSING"
		DrawPlaceholderLabel(img, "IMAGE MISSING", new Color(0.85f, 0.75f, 0.45f));

		var tex = ImageTexture.CreateFromImage(img);
		return tex;
	}

	private static void DrawPlaceholderLabel(Image img, string text, Color color)
	{
		// 5x7 bitmap digits/letters for A-Z space
		var glyphs = BuildGlyphs();
		const int scale = 4;
		const int gw = 5;
		const int gh = 7;
		var chars = text.ToUpperInvariant();
		var totalW = chars.Length * (gw + 1) * scale;
		var startX = System.Math.Max(0, (img.GetWidth() - totalW) / 2);
		var startY = System.Math.Max(0, (img.GetHeight() - gh * scale) / 2);

		for (var i = 0; i < chars.Length; i++)
		{
			if (!glyphs.TryGetValue(chars[i], out var rows))
				continue;
			var ox = startX + i * (gw + 1) * scale;
			for (var row = 0; row < gh; row++)
			{
				var bits = rows[row];
				for (var col = 0; col < gw; col++)
				{
					if (((bits >> (gw - 1 - col)) & 1) == 0)
						continue;
					for (var sy = 0; sy < scale; sy++)
					for (var sx = 0; sx < scale; sx++)
					{
						var x = ox + col * scale + sx;
						var y = startY + row * scale + sy;
						if ((uint)x < (uint)img.GetWidth() && (uint)y < (uint)img.GetHeight())
							img.SetPixel(x, y, color);
					}
				}
			}
		}
	}

	private static Dictionary<char, int[]> BuildGlyphs()
	{
		// Each entry: 7 rows of 5-bit patterns
		return new Dictionary<char, int[]>
		{
			['A'] = new[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
			['E'] = new[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
			['G'] = new[] { 0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110 },
			['I'] = new[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111 },
			['M'] = new[] { 0b10001, 0b11011, 0b10101, 0b10001, 0b10001, 0b10001, 0b10001 },
			['N'] = new[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
			['S'] = new[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
			[' '] = new[] { 0, 0, 0, 0, 0, 0, 0 },
		};
	}
}

