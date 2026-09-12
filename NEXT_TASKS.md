# PRÓXIMAS TAREFAS

Não puxe trabalhos distantes e enormes. Foque na manutenção da estabilidade do código atual. Respeite as prioridades antes de avançar de categoria. 

**NUNCA adicione sistemas novos se os P0 ou P1 estiverem pendentes.**

### P0 (Erros Críticos / Bugs / Regressões a arrumar)
- Atualmente a build compila 100% livre de falhas arquiteturais aparentes. O Grok deve se certificar que a perfomance dos POIs no mapa não afete drasticamente as taxas de quadro ao longo do avanço do Save (Lixo acumulado do chunk generator no C#).
- Consertar problema onde alternar muito rápido a tela de "Deck/Cartas" pode fazer com que o mouse escape da janela e force o player a apertar `TAB` novamente para destravar.

### P1 (Economia e Loot das Cartas) — EM PR / Grok
*Balanceamento de recompensas e mercador (branch `grok/p1-card-economy-loot`).*
- `CombatManager.Settle` agora escala XP/moedas/raridade por `EnemyDefinition` (CoinMin/Max + LootTier).
- Inimigos tier ≥3 têm chance de drop extra de poção (cartas Heal).
- Catálogo `Assets/Cards/generic.json`: atributos reforçados nas cartas iniciais do starter.
- Mercador: pacotes 35 / 45 (poções) / 70; pacote básico com peso leve de rara.

### P2 (Melhorias Importantes na Fila)
- **Animais (Vida Selvagem e Atmosfera):** A floresta tem vaga-lumes e neblina, mas carece de criaturas passivas no chão (veados, coelhos). Crie um `AnimalManager` que espalhe malhas não-agressivas de baixa poligonagem.
- **Áudio no Combate:** Inserir chamadas simples de `AudioStreamPlayer3D` para cada intent de combate (Corte, Magia, Dano tomado) em `CombatArenaController`.

### P3 (Futuras Ideias Conceptuais - NÃO FAZER AGORA)
- Interligar as missões de Companheiros diretamente em cartas que eles jogam sozinhos em batalhas, evoluindo o estado `IsDuel` e `Companion` no `CardModels`.
