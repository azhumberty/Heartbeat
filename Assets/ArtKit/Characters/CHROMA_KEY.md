# Chroma Key (#00FF00)

Sprites NPC/monstro: fundo verde puro para `chroma_key.gdshader` (canvas_item).

## Uso no codigo
```csharp
_enemy.Texture = ChromaArt.LoadArt(ChromaArt.EnemySpritePath(enemyId));
ChromaArt.ApplyChroma(_enemy);
```

## Assets
- `NPCs/merchant_sunga_chroma.png`
- `Monsters/monster_horned_chroma.png` (forest/night)
- `Monsters/monster_stone_chroma.png` (camp/ruin)

Shader: `Assets/ArtKit/Characters/chroma_key.gdshader`
Helper: `Scripts/Art/ChromaArt.cs`
Loader: `ImageManager` Autoload (fallback IMAGE MISSING)
