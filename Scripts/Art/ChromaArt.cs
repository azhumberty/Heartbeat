using Godot;

namespace Heartbeat;

/// <summary>
/// VN ArtKit helpers: chroma-key materials + path maps for backgrounds/enemies.
/// </summary>
public static class ChromaArt
{
	public const string ChromaShaderPath = "res://Assets/ArtKit/Characters/chroma_key.gdshader";

	public static ShaderMaterial? CreateChromaMaterial(float threshold = 0.35f, float smoothness = 0.08f)
	{
		if (!ResourceLoader.Exists(ChromaShaderPath))
		{
			GD.PushWarning($"[ChromaArt] Missing shader at {ChromaShaderPath}");
			return null;
		}

		var shader = GD.Load<Shader>(ChromaShaderPath);
		if (shader == null) return null;

		var mat = new ShaderMaterial { Shader = shader };
		mat.SetShaderParameter("key_color", new Color(0f, 1f, 0f));
		mat.SetShaderParameter("threshold", threshold);
		mat.SetShaderParameter("smoothness", smoothness);
		return mat;
	}

	public static void ApplyChroma(CanvasItem node, float threshold = 0.35f)
	{
		var mat = CreateChromaMaterial(threshold);
		if (mat != null) node.Material = mat;
	}

	/// <summary>Atlas destination id → ArtKit background relative path.</summary>
	public static string BackgroundForDestination(string destinationId) =>
		destinationId.ToLowerInvariant() switch
		{
			"merchant" => "Backgrounds/merchant_tent.png",
			"forest" => "Backgrounds/forest_dark.png",
			"camp" => "Backgrounds/camp.png",
			"tavern" => "Interiors/tavern.png",
			"ruin" or "ruins" => "Interiors/ruins_hall.png",
			_ => "Backgrounds/" + destinationId + ".png"
		};

	/// <summary>Combat arena name → ArtKit background.</summary>
	public static string BackgroundForArena(string arena) =>
		arena.ToLowerInvariant() switch
		{
			"camp" => "Backgrounds/camp.png",
			"forest" => "Backgrounds/forest_dark.png",
			"merchant" => "Backgrounds/merchant_tent.png",
			"night" => "Backgrounds/forest_dark.png",
			"ruin" or "ruins" => "Interiors/ruins_hall.png",
			_ => "Backgrounds/camp.png"
		};

	/// <summary>Enemy id → chroma sprite under Characters/Monsters.</summary>
	public static string EnemySpritePath(string enemyId) =>
		enemyId.ToLowerInvariant() switch
		{
			"forest" or "night" => "Characters/Monsters/monster_horned_chroma.png",
			"camp" or "ruin" => "Characters/Monsters/monster_stone_chroma.png",
			_ => "Characters/Monsters/monster_horned_chroma.png"
		};

	public const string MerchantSprite = "Characters/NPCs/merchant_sunga_chroma.png";

	public static Texture2D LoadArt(string relativePath)
	{
		if (ImageManager.Instance != null)
			return ImageManager.Instance.GetArt(relativePath);

		var full = ImageManager.ArtKitRoot + relativePath.TrimStart('/');
		if (ResourceLoader.Exists(full))
			return GD.Load<Texture2D>(full)!;

		GD.PushWarning($"[ChromaArt] IMAGE MISSING (no ImageManager): {relativePath}");
		return new ImageTexture();
	}
}
