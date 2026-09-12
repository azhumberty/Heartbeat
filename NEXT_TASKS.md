# PRÓXIMAS TAREFAS

Não puxe trabalhos distantes e enormes. Foque na manutenção da estabilidade do código atual. Respeite as prioridades antes de avançar de categoria. 

**NUNCA adicione sistemas novos se os P0 ou P1 estiverem pendentes.**

### P0 (Erros Críticos / Bugs / Regressões a arrumar)
- Consertar problema onde alternar muito rápido a tela de "Deck/Cartas" pode fazer com que o mouse escape da janela e force o player a apertar `TAB` novamente para destravar.
- Validar localmente (`dotnet build` + play) o overhaul 2.5D/combate da branch `grok/25d-combat-overhaul` (billboards via `.png.b64` loader, terreno opaco, shop header).
- Observar FPS dos chunks com Sprite3D billboards (textures compartilhadas); se cair, reduzir `treeCount` ou aumentar culling.

### P1 (Economia e Loot das Cartas) — feito na branch `grok/p1-card-economy-loot`
*Balanceamento de recompensas e mercador.*
- `CombatManager.Settle` escala XP/moedas/raridade por `EnemyDefinition` (CoinMin/Max + LootTier).
- Mercador + catálogo starter reforçado.

### P1b (2.5D + Combate) — EM PR / Grok (`grok/25d-combat-overhaul`)
- Shop: header labels sem Autowrap esmagado pelo ExpandFill.
- Mundo 2.5D: terreno triplanar opaco; árvores/POIs como Sprite3D billboard; assets em `Assets/World/Billboards/*.png.b64`.
- Combate: HP/Mana separados na UI, intents empilháveis, AI com combos/cura, night HP ↑, cartas g41–g55 (veneno/sangue/lifesteal/debuffs).

### P2 (Melhorias Importantes na Fila)
- **Animais (Vida Selvagem e Atmosfera):** `AnimalManager` com criaturas passivas de baixa poligonagem / billboards.
- **Áudio no Combate:** `AudioStreamPlayer` por intent em `CombatArenaController`.
- Decodar `.png.b64` → `.png` reais no repo (MCP não preserva binário UTF-8) para import nativo do Godot.

### P3 (Futuras Ideias Conceptuais - NÃO FAZER AGORA)
- Interligar missões de Companheiros em cartas que jogam sozinhas (`IsDuel` / Companion).
