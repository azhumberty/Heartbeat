using Godot;

namespace Heartbeat;

/// <summary>
/// VN ArtKit helpers: chroma-key materials + path maps for backgrounds/enemies.
/// </summary>
public static class ChromaArt
{
	public const string ChromaShaderPath = "res://Assets/ArtKit/Characters/chroma_key.gdshader";

	public static ShaderMaterial? CreateChromaMaterial(float threshold = 0.55f, float smoothness = 0.15f)
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

	public static void ApplyChroma(CanvasItem node, float threshold = 0.55f)
	{
		var mat = CreateChromaMaterial(threshold);
		if (mat != null) node.Material = mat;
	}

	/// <summary>Atlas destination id â†’ ArtKit background relative path.</summary>
	public const string BarmaidSprite = "Characters/NPCs/npc_barmaid_chroma.png";
	public const string KnightSprite = "Characters/NPCs/npc_knight_chroma.png";

	public static string BackgroundForDestination(string destinationId) =>
		destinationId.ToLowerInvariant() switch
		{
			"merchant" or "merchant_tent" => "Backgrounds/merchant_tent.png",
			"forest" or "forest_dark" => "Backgrounds/forest_dark.png",
			"camp" => "Backgrounds/camp.png",
			"knight" => "Backgrounds/street.png",
			"tavern" => "Interiors/tavern.png",
			"ruin" or "ruins" or "ruins_hall" => "Interiors/ruins_hall.png",
			"cave" or "cave_chamber" => "Backgrounds/cave_chamber.png",
			"street" => "Backgrounds/street.png",
			"park" => "Backgrounds/park.png",
			"cafe" => "Interiors/cafe.png",
			"atlas" or "atlas_map" => "Backgrounds/atlas_map.png",
			_ => "Backgrounds/" + destinationId + ".png"
		};

	/// <summary>Combat arena name â†’ ArtKit background.</summary>
	public static string BackgroundForArena(string arena) =>
		arena.ToLowerInvariant() switch
		{
			"camp" => "Backgrounds/camp.png",
			"forest" => "Backgrounds/forest_dark.png",
			"merchant" => "Backgrounds/merchant_tent.png",
			"night" => "Backgrounds/forest_dark.png",
			"ruin" or "ruins" => "Interiors/ruins_hall.png",
			"tavern" => "Interiors/tavern.png",
			"cave" or "cave_chamber" => "Backgrounds/cave_chamber.png",
			"street" => "Backgrounds/street.png",
			"park" => "Backgrounds/park.png",
			"cafe" => "Interiors/cafe.png",
			_ => "Backgrounds/camp.png"
		};

	/// <summary>Enemy id â†’ chroma sprite under Characters/Monsters.</summary>
	public static string EnemySpritePath(string enemyId)
	{
		var id = (enemyId ?? "").ToLowerInvariant();
		if (id.Contains("forest") || id.Contains("night") || id.Contains("ward")) return "Characters/Monsters/monster_horned_chroma.png";
		if (id.Contains("camp") || id.Contains("ruin") || id.Contains("elite")) return "Characters/Monsters/monster_stone_chroma.png";
		if (id.Contains("minotaur") || id.Contains("boss")) return "Characters/Monsters/minotaur_chroma.png";
		int h = 0; foreach (var c in id) h = h * 33 + c;
		return (Math.Abs(h) % 3) switch
		{
			0 => "Characters/Monsters/monster_horned_chroma.png",
			1 => "Characters/Monsters/monster_stone_chroma.png",
			_ => "Characters/Monsters/minotaur_chroma.png"
		};
	}

	public const string MerchantSprite = "Characters/NPCs/merchant_traveler_chroma.png";
	public const string BarbarianSprite = "Characters/NPCs/barbarian_chroma.png";
	public const string MinotaurSprite = "Characters/Monsters/minotaur_chroma.png";

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
