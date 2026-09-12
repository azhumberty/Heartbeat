# Heartbeat ArtKit (Grok) — pacote para outros agentes

Pasta **separada** do código de gameplay. Objetivo: Astra / Claude / Gemini / outros agentes pegarem assets prontos e plugarem no Godot sem regenerar arte.

## Formatos
| Tipo | Extensão | Uso no Godot 4.7 |
|---|---|---|
| Texturas de chão | `Textures/*.png` (+ `.b64`) | `StandardMaterial3D` albedo, idealmente UV1 triplanar (`SurfaceMaterials`) |
| Céus | `Sky/sky_*.png` | Panorama do `Sky` / background do ciclo dia-noite |
| Celestes | `Celestial/*.png` | `Sprite3D` ou decals no céu (sol/lua/planeta) — fundo transparente |
| Props mundo | `Billboards/*.png` | `Sprite3D` com `Billboard = Enabled` via `BillboardSprites.Attach(name)` |
| Interiores | `Interiors/*.png` | Fundo 2D de cena / `TextureRect` / plate de diálogo |

Se o PNG binário não importar bem pelo Git, use o sidecar `arquivo.png.b64` (base64 puro). O loader atual em `BillboardSprites` já lê `.png` **ou** `.png.b64`.

## Inventário (lote 1)
### Textures/
- `ground_forest.png` — solo floresta tileável
- `grass.png` — grama tileável
- `cave_floor.png` — chão de caverna tileável

### Sky/
- `sky_day.png` — dia
- `sky_dusk.png` — tarde/pôr do sol
- `sky_night.png` — noite + estrelas/Via Láctea

### Celestial/
- `sun.png`, `moon.png`, `planet_rings.png` — sprites transparentes

### Billboards/
- `tree_leafy.png`, `tree_pine.png`, `tree_oak_dead.png`
- `house.png`, `cabin.png`, `cave.png`, `ruins.png`, `campfire.png`
- `mountains.png` — faixa de montanhas (parallax/fundo)

### Interiors/
- `cottage_room.png` — sala medieval (fundo de interior)

## Como integrar (para Astra / outros)
1. Checkout branch `grok/art-kit-2.5d` **ou** copie `Assets/ArtKit/` para o working tree.
2. Não reescreva sistemas existentes; só referencie caminhos `res://Assets/ArtKit/...`.
3. Chão: aplique texturas em `SurfaceMaterials` / materiais do terreno.
4. Céu: ligue `sky_day|dusk|night` ao `SkyController` conforme período.
5. Props: `BillboardSprites.Attach(parent, "tree_leafy", pos, size)` — se o nome não estiver em `Assets/World/Billboards/`, copie/symlink de `ArtKit/Billboards/` ou estenda o loader para também procurar `res://Assets/ArtKit/Billboards/`.
6. Interiores: use como texture de cena de diálogo / POI indoor.

## Convenções
- Estilo: dark fantasy medieval, coerente com Heartbeat.
- Billboards: fundo transparente, frente ortográfica.
- Texturas: preferência tileável top-down.
- Licença: gerado por Grok Bot para uso **pessoal/privado** no projeto Heartbeat (pedido do owner). Registrar em `ASSET_SOURCES.md` ao consumir.

## Próximos lotes (pedir ao Grok)
Interiores extras (taverna, ruína interna), mais árvores sazonais, flora, UI icons, cards art, wall/cliff tiles.


## Characters/ (lote personagens)
- `Characters/Monsters/` — inimigos humanoides masculinos (pouca roupa). Ver README da pasta. **Combate only.**
- `Characters/NPCs/` — mercador barbudo peludo de sunga (+ portrait). Ver README da pasta. **Loja/diálogo only.**

## UI/
- Ver `UI/README.md` — card art, HUD icons, combat intents, menu/combat frames alinhados ao tema `#b39a64` / `#0a0e12`.

## Flora/
- Ver `Flora/README.md` — bush, fern, mushrooms, flowers, tall grass, ivy (Sprite3D).

## UI/Cards/Faces/
- Ver `UI/Cards/Faces/README.md` — artes g41/g42/g45/g48/g50/g53/g54/g55.
