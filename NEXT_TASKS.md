# PRÓXIMAS TAREFAS

Não puxe trabalhos distantes e enormes. Foque na manutenção da estabilidade do código atual. Respeite as prioridades antes de avançar de categoria. 

**NUNCA adicione sistemas novos se os P0 ou P1 estiverem pendentes.**

### P0 (Erros Críticos / Bugs / Regressões a arrumar)
- Atualmente a build compila 100% livre de falhas arquiteturais aparentes. O Grok deve se certificar que a perfomance dos POIs no mapa não afete drasticamente as taxas de quadro ao longo do avanço do Save (Lixo acumulado do chunk generator no C#).
- Consertar problema onde alternar muito rápido a tela de "Deck/Cartas" pode fazer com que o mouse escape da janela e force o player a apertar `TAB` novamente para destravar.

### P1 (PRÓXIMA TAREFA IMEDIATAMENTE RECOMENDADA)
*Esta é a sua missão inicial principal ao abrir este handoff:*

1. **Afinar a Economia e Loot das Cartas (Expansão do Merchant)**
   O Mercador atual (em `CardShopController`) vende pacotes baseados em Reais (`Coins`). Seu objetivo agora deve ser **balancear as Recompensas de Combate e os Custos dos Drops**.
   - Acesse `Scripts/Models.cs` e `Scripts/Combat/CombatManager.cs` (método `Settle`).
   - Melhore as regras matemáticas ou tabelas de peso de drops aleatórios baseados na dificuldade do Monstro (ID/Level) e na categoria de Inimigo. Inimigos fortes devem dar cartas ou poções únicas.
   - Reforce os atributos das Cartas Genéricas Iniciais (Atualmente geradas aleatoriamente em `CardRepository.Catalog`).

### P2 (Melhorias Importantes na Fila)
- **Animais (Vida Selvagem e Atmosfera):** A floresta tem vaga-lumes e neblina, mas carece de criaturas passivas no chão (veados, coelhos). Crie um `AnimalManager` que espalhe malhas não-agressivas de baixa poligonagem.
- **Áudio no Combate:** Inserir chamadas simples de `AudioStreamPlayer3D` para cada intent de combate (Corte, Magia, Dano tomado) em `CombatArenaController`.

### P3 (Futuras Ideias Conceptuais - NÃO FAZER AGORA)
- Interligar as missões de Companheiros diretamente em cartas que eles jogam sozinhos em batalhas, evoluindo o estado `IsDuel` e `Companion` no `CardModels`.
