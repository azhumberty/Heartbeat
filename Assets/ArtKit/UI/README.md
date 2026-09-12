# UI / Cards / HUD (ArtKit) — GUIA PARA AGENTES

Tema extraído do código Heartbeat (`CardView`, `CardUi`, `Ui`):
- Fundo painel: `#0a0e12` / `#131d2f`
- Borda ouro: `#b39a64`
- Texto: `#e8e0d4`
- Mana/custo: `#93c9ee`
- Botões: `#263449` → hover `#405671`

## Pastas

### `UI/Cards/` — arte e moldura de cartas
| Arquivo | Uso | Onde plugar |
|---|---|---|
| `cardart_attack.png` | Arte genérica categoria Ataque | `CardDefinition.ImagePath` ou fallback por `CardCategory.Attack` |
| `cardart_defense.png` | Defesa | idem `Defense` |
| `cardart_mana.png` | Mana | idem `Mana` |
| `cardart_control.png` | Controle/debuff | idem `Control` |
| `cardart_heal.png` | Cura | idem `Heal` |
| `cardart_poison.png` | Veneno (cartas g41+) | ImagePath de Poison cards |
| `card_frame_empty.png` | Moldura vazia (template) | Referência visual / TextureRect atrás do `CardView` |

Cores de frame já no JSON: Attack `ba705a`, Defense `839c9a`, Mana `648ccd`, Control `a67fc2`, Heal `78a475`, Utility `b4a375`.

### `UI/Icons/` — HUD / mundo
| Arquivo | Uso |
|---|---|
| `health.png` | Ícone Vida (barra/player HUD) |
| `mana.png` | Ícone Mana |
| `coin.png` | Reais / economia / loja |
| `reticule.png` | Mira FPS (`PlayerController` HUD) |

### `UI/Intents/` — combate (empilhar sobre inimigo)
| Arquivo | Intent |
|---|---|
| `attack.png` | Atacar |
| `shield.png` | Guarda |
| `potion.png` | Cura/poção da IA |
| `sleep.png` | Atordoado / não age |

Usar em `CombatArenaController` intent chips (substituir emoji por TextureRect).

### `UI/Frames/`
| Arquivo | Uso |
|---|---|
| `menu_background.png` | Fundo `MainMenu` |
| `combat_panel.png` | Nine-patch / painel HP-Mana da arena |

## Integração sugerida (Astra / outros)
1. Não recriar `CardView` — só alimentar `ImagePath` e TextureRects.
2. Preferir `res://Assets/ArtKit/UI/...` (PNG) ou sidecar `.b64`.
3. Manter compatibilidade com saves; assets são cosméticos.
