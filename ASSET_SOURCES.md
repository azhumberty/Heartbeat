# Fontes de assets

Todas as texturas externas desta lista são CC0 e podem ser usadas, modificadas e redistribuídas sem atribuição obrigatória. A atribuição é mantida aqui para rastreabilidade.

| Asset | Autor | Fonte | Licença | Uso no jogo |
|---|---|---|---|---|
| Forest Floor | eye-candy.xyz | https://polyhaven.com/a/forest_floor | CC0 | Solo procedural da floresta |
| Mossy Rock | Rob Tuytel | https://polyhaven.com/a/mossy_rock | CC0 | Rochas distribuídas nos chunks |
| Bark Brown 01 | Rob Tuytel | https://polyhaven.com/a/bark_brown_01 | CC0 | Troncos de árvores |

Foram usadas as variantes JPG 1K de cor, normal OpenGL e roughness, obtidas pela API/CDN oficial do Poly Haven. A geometria, a trilha, a vegetação, o céu e os quatro POIs continuam sendo gerados com malhas e shaders do próprio projeto.

## Billboards 2.5D (`Assets/World/Billboards/`)

| Asset | Arquivo | Fonte | Licença | Propósito |
|---|---|---|---|---|
| Pinheiro escuro | `tree_pine.png.b64` | GenerateImage (Cursor) | personal private test per user instruction | Árvores coníferas billboard |
| Carvalho / folhosa | `tree_oak.png.b64` | GenerateImage (Cursor) | personal private test per user instruction | Árvores folhosas billboard |
| Ruínas | `ruins.png.b64` | GenerateImage (Cursor) | personal private test per user instruction | POI ruin / arena |
| Fogueira | `campfire.png.b64` | GenerateImage (Cursor) | personal private test per user instruction | POI camp |
| Cabana | `cabin.png.b64` | GenerateImage (Cursor) | personal private test per user instruction | POI cabin / shelter |

Os arquivos estão em Base64 (`.png.b64`) porque o GitHub MCP corrompe PNG binário via UTF-8. `BillboardSprites` decodifica em runtime (`Marshalls.Base64ToRaw` + `Image.LoadPngFromBuffer`). Se um `.png` real existir no mesmo nome, ele tem prioridade.

## ASSETS FORNECIDOS PELO JOGADOR
* O jogo suporta carregamento dinâmico de artes modulares de terceiros diretamente via runtime sem necessidade de importação pelo Godot.
* **Fotos de NPCs / Cartas / Inimigos:** Arquivos locais devem ser colocados pelo usuário em `user://Enemies/{id}.png` ou pastas de personagens correspondentes. Responsabilidade de copyright dessas imagens injetadas dinamicamente localmente é exclusiva do usuário.

## World Billboards (2026-09-12 · Grok)
| Asset | Path | Origin | License note | Purpose |
|---|---|---|---|---|
| pine | Assets/World/Billboards/pine.png.b64 | AI-generated (Grok Bot GenerateImage) | Personal private test per project owner | Forest Sprite3D billboard |
| oak | Assets/World/Billboards/oak.png.b64 | AI-generated | Personal private test | Forest Sprite3D billboard |
| ruins | Assets/World/Billboards/ruins.png.b64 | AI-generated | Personal private test | Ruin POI billboard |
| campfire | Assets/World/Billboards/campfire.png.b64 | AI-generated | Personal private test | Camp POI billboard |
| cabin | Assets/World/Billboards/cabin.png.b64 | AI-generated | Personal private test | Cabin/shelter POI billboard |

Loaded at runtime via `BillboardSprites` (PNG or `.png.b64` sidecar).

## Grok Imagine · 13 set 2026

Assets gerados para o próprio Heartbeat e incorporados da entrega `DATING SIM.zip`; nenhum conteúdo foi extraído de jogos comerciais.

| Asset | Caminho | Origem | Uso |
|---|---|---|---|
| Atlas isométrico | `Assets/ArtKit/Backgrounds/atlas_map.png` | Grok Imagine | Fundo do Atlas procedural |
| Atlas iluminado | `Assets/ArtKit/Backgrounds/atlas_map_lit.png` | Grok Imagine | Variante visual do Atlas |
| Acampamento, caverna, parque e rua | `Assets/ArtKit/Backgrounds/` | Grok Imagine | Cenas narrativas e arenas |
| Roan | `Assets/ArtKit/Characters/NPCs/barbarian_chroma.png` | Grok Imagine + chroma | Personagem de relacionamento |
| Silas | `Assets/ArtKit/Characters/NPCs/merchant_traveler_chroma.png` | Grok Imagine + chroma | Mercador sem romance |
| Minotauro | `Assets/ArtKit/Characters/Monsters/minotaur_chroma.png` | Grok Imagine + chroma | Inimigo de expedição |
