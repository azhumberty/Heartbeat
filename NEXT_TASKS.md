# PIVOT ARQUITETURAL: 3D PARA 2D VISUAL NOVEL & CARD GAME

## DECISÃO DO USUÁRIO
O usuário decidiu abandonar a exploração 3D em primeira pessoa devido aos gargalos técnicos de importação de assets (fundos falsos, bugs de cache do Godot) e complexidade de colisões. 
O novo foco é **100% 2D UI-Driven**, misturando **Visual Novel, Dating Sim, RPG Textual e Card Game**, com navegação por **Atlas (Node Map)** semelhante a Slay the Spire / Path of Exile.

## DIRETRIZES PARA OS PRÓXIMOS AGENTES (GROK, CLAUDE, ETC):

### 1. LIMPEZA E DESACOPLAMENTO (Fase 1)
- **Remover** as mecânicas de FPS: PlayerController.cs, ChunkGenerator.cs, ChunkManager.cs, SkyController.cs, BillboardSprites.cs.
- Limpar a cena principal para ser estritamente Control (CanvasLayer). O 3D só será usado se for estritamente para efeitos de partículas por cima do 2D (opcional).

### 2. NAVEGAÇÃO POR MAPA (ATLAS) (Fase 2)
- Criar um MapController.cs (Interface 2D).
- Renderizar pontos de interesse (Nodes) clicáveis (Cabana, Floresta Escura, Mercador).
- Ao invés de andar, o jogador gerencia energia/tempo e clica para viajar para os eventos.

### 3. INTERAÇÕES E HISTÓRIA (VISUAL NOVEL) (Fase 3)
- O DialogueController.cs se torna o centro do jogo.
- **Visual:** Fundo de tela cheia (16:9 gerado por IA). Personagem (Sprite) centralizado. Caixa de diálogo preta com texto branco embaixo.
- **Mecânica:** O jogador digita o que quer falar ou escolhe opções. O LLM (Groq) responde dinamicamente e altera o rumo da história.

### 4. COMBATE DE CARTAS (Fase 4)
- Adaptar a CombatArenaController.cs para a visão 2D.
- Inimigo desenhado no centro, fundo correspondente ao bioma.
- Refinar a IA do inimigo e escalar status para ficar mais desafiador.

### 5. ASSETS DE ARTE
- A partir de agora, **fundos** devem ser gerados em 16:9 (ex: paisagens de floresta, tavernas).
- **Sprites de Inimigos/NPCs:** Podem ser gerados com Chroma Key (fundo verde puro) para o Godot recortar nativamente no import, já que IAs lutam para gerar .png real.
