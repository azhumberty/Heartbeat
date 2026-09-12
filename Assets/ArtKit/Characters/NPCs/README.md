# NPCs (ArtKit) — USO PARA AGENTES

**Pasta:** `Assets/ArtKit/Characters/NPCs/`  
**Tipo:** PNG transparente  
**Público:** Adulto (dating sim masculino).

## Mercador barbudo peludo (sunga)

Personagem de **venda de cartas** (Card Shop / diálogo de loja). Homem adulto, barbudo, peludo, de sunga.

| Arquivo | Uso | Sistema |
|---|---|---|
| `merchant_sunga_fullbody.png` | Corpo inteiro no mundo / POI / Sprite3D do vendedor | Spawn NPC mercador / `ForestPoi` camp / billboard |
| `merchant_sunga_portrait.png` | Retrato no diálogo da loja (peito nu + barba) | `DialogueController` / portrait UI / `PortraitCache` |
| `merchant_dressed_portrait.png` | Alternativa vestida (se quiser tom mais “taverna”) | Mesmo pipeline de portrait |

### Integração sugerida
1. Ligar ao fluxo existente da **loja de cartas** (`CardShopController` + opção 💰 no diálogo).
2. Criar/atualizar `Characters/{id}.json` do mercador com `MainImagePath` / portraits apontando para estes paths `res://Assets/ArtKit/Characters/NPCs/...`.
3. Fullbody: billboard `Sprite3D` perto do camp/POI de comércio.
4. **Não** usar como inimigo de combate.

### Tags para busca
`merchant`, `shop`, `vendor`, `sunga`, `bear`, `hairy`, `bearded`, `adult-male-npc`
