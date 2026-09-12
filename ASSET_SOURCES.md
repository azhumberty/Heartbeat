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
