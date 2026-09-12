# Monstros humanoides (ArtKit) — USO PARA AGENTES

**Pasta:** `Assets/ArtKit/Characters/Monsters/`  
**Tipo:** PNG transparente full-body (billboard / combate)  
**Público:** Adulto. Homens adult humanoides com pouca roupa (tanga/loincloth). **Não são NPCs românticos** — são **inimigos**.

## Como usar no Heartbeat
| Arquivo | Uso recomendado | Sistema |
|---|---|---|
| `humanoid_horned_idle.png` | Sprite mundo / idle do inimigo | `EncounterManager` / `EnemyVisual` / `Sprite3D` billboard |
| `humanoid_horned_combat.png` | Arena de combate (pose agressiva) | `CombatArenaController` máscara/sprite do inimigo |
| `humanoid_wraith_idle.png` | Variante floresta/noite (`night` / forest) | Mapear em `EnemyDefinition` Id |
| `humanoid_stone_idle.png` | Variante ruína (`ruin`) | Mapear em `EnemyDefinition` Id |

### Integração sugerida
1. Copiar (ou symlink) para `user://Enemies/{id}.png` **ou** `res://Assets/ArtKit/Characters/Monsters/...`.
2. O jogo já suporta sprite custom em combate via `user://Enemies/{id}.png` (ver handoff). Preferir ids: `forest`, `night`, `ruin`, `camp` ou novos ids.
3. Para billboard no mundo: `BillboardSprites` / `Sprite3D` Billboard Enabled, altura ~1.8–2.2 m.
4. Cada PNG tem sidecar `.b64` se o import binário falhar.

### NÃO usar estes arquivos para
- Diálogo / dating / mercador / companion cards
- Personagens do Character Creator
