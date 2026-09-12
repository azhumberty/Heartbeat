# PRÓXIMAS TAREFAS

Não puxe trabalhos distantes e enormes. Foque na manutenção da estabilidade do código atual. Respeite as prioridades antes de avançar de categoria.

**NUNCA adicione sistemas novos se os P0 ou P1 estiverem pendentes.**

### P0 (Erros Críticos / Bugs / Regressões a arrumar)
- Performance dos POIs / lixo do chunk generator ao longo do Save.
- Mouse dessincroniza ao alternar rápido Deck/Cartas (Captured/Visible) — ainda aberto.

### P1 (Economia e Loot) — ver PR #1 / branch `grok/p1-card-economy-loot`
- Balanceamento de recompensas e mercador.

### P1b / Visual+Combate 2.5D — branch `grok/25d-combat-overhaul` (Grok)
- Fix header mercador (Autowrap).
- Terreno triplanar opaco.
- Árvores/POIs em Sprite3D billboard + assets em `Assets/World/Billboards/*.png.b64`.
- Arena: painéis HP/Mana, intents empilhados, tweens.
- IA inimiga com combos / poção; night HP elevado; EffectKind.Poison.
- +15 cartas (g41–g55): veneno, lifesteal, debuffs.

### P2 (Melhorias Importantes na Fila)
- Animais passivos no mundo.
- Áudio no combate.
- Trocar sidecars `.b64` por PNG binários nativos no Godot import pipeline se preferir.

### P3 (Futuras Ideias Conceptuais - NÃO FAZER AGORA)
- Companheiros jogando cartas sozinhos em batalhas multi-turno.
