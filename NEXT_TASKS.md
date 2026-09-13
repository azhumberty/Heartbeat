# HEARTBEAT — PRÓXIMAS TAREFAS

## P0 — Eventos procedurais jogáveis

1. Criar `EventContext`, `EventResult`, `EventChoice` e `ProceduralEventService`.
2. Implementar eventos locais com duas a quatro escolhas para evento, descanso, cena, mistério e chefe.
3. Aplicar consequências com limites definidos pelo C# e registrar somente um resumo curto no save.
4. Concluir o nó do Atlas apenas depois da escolha ou da vitória.

## P1 — Groq opcional para eventos

1. Enviar contexto curto: lore, nó, nome do jogador, assets ativos e eventos recentes.
2. Exigir JSON, validar todos os campos e limitar deltas.
3. Implementar timeout, cancelamento e fallback local sem interromper a campanha.

## P2 — Conteúdo Criativo restante

1. Transformar personagens e NPCs cadastrados em pacotes compatíveis com `CharacterRepository`.
2. Ligar mercadores personalizados a estoques e preços.
3. Ligar cartas personalizadas cadastradas à coleção do jogador.
4. Adicionar campos específicos por categoria sem sobrecarregar a tela.

## P3 — Interface

1. Tornar o painel de detalhes do combate recolhível para preservar a arte central.
2. Testar os layouts em 1280×720, 1600×900 e 1920×1080.
3. Substituir os textos com codificação antiga restantes.
