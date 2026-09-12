# ESTADO DE HANDOFF DO HEARTBEAT

**DATA DO HANDOFF:** 12 de Setembro de 2026
**ÚLTIMO AGENTE:** Gemini Pro / Antigravity

## VISÃO GERAL
Heartbeat é um RPG medieval *dark fantasy* em primeira pessoa. Seu núcleo mescla elementos imersivos (exploração procedural em florestas densas) com um sistema de relacionamento (Dating Sim) voltado para parceiros masculinos adultos, aliados a um robusto sistema de Batalhas de Cartas para combates e duelos.

## STACK TÉCNICA
- **Engine:** Godot 4.7.2 Forward+
- **Linguagem:** C# (.NET 8)
- **Renderização:** PBR Textures (Uv1Triplanar), Volumetric Fog, Sky dinâmico, Particles.
- **LLM/IA:** Groq API SDK (via C# HttpClient) para diálogos contextuais de NPCs.

## CENAS PRINCIPAIS
- `Scenes/MainMenu/MainMenu.tscn`: A porta de entrada, carregando configurações e saves.
- `Scripts/World/WorldController.cs` (Dinâmico): O palco global que instancializa e orquestra chunks, POIs, combate, interface e player sem necessitar de cenas hardcoded gigantes.

## ARQUITETURA E SISTEMAS IMPLEMENTADOS

- **CHUNK GENERATOR (WORKING):** Mundo virtual gerado proceduralmente com culling ativo. 
- **SKY & DIA/NOITE (WORKING):** Ciclo de 24 horas dinâmico que afeta clima, fog e spawns. Partículas no ar reagem se é dia (Pólen) ou noite (Vagalumes).
- **PLAYER CONTROLLER FPS (WORKING):** Movimento, Headbob, View em 1ª Pessoa, jump single.
- **HUD & UI (WORKING):** Interface dark fantasy. Bússola e Retícula Minimalista minimalista.
- **BARALHO E ECONOMIA (WORKING):** Inventário gerencia cartas. O `DeckEditor` visualiza uma grid de cartas modulares. Moedas (Reais) dropam em batalhas e podem ser usadas para comprar cartas no NPC Mercador.
- **COMBATE POR CARTAS (WORKING):** Módulo 3D separado (`CombatArenaController`) com física reduzida, cartas em 2D na mão, máscara inteligente para fotos (Clipping). Animações de intent (⚔️🛡️💤) e health bars animados no inimigo/player. Recompensas incluem coins e cartas com XP.
- **DUELOS AMISTOSOS (WORKING):** Permite desafiar NPCs pacíficos para um jogo de cartas através do menu de diálogos, ganhando coins extras sem risco de penalidades mortais de Game Over.
- **MONSTROS E INIMIGOS 2D (WORKING):** Encontros no mundo spawnados pelo `EncounterManager`. Monstros agora suportam renderização de sprites `user://Enemies/{id}.png` em 3D, com animação Tween de respiração e vermelhidão (flash de impacto) no `CombatArenaController`. Se não houver arquivo PNG local, volta para primitivos 3D procedurais.
- **POIs & INTERAÇÕES NO MUNDO (WORKING):** Cabanas, Acampamentos e Ruínas onde o jogador aperta `E` para descansar (Vida/Energia) ou coletar mana localmente.
- **NPC MANAGER & CRIAÇÃO (WORKING):** O jogo parseia NPCs data-driven de `Characters/*.json`. Spawna e os gerencia conforme horários na agenda diária da floresta. Os personagens possuem fotos modulares gerenciadas por `SpriteSheetImporter` e `PortraitCache`.
- **DIÁLOGO ONLINE/OFFLINE (WORKING):** Conexão via `GROQ_API_KEY` aciona um Chat LLM persistido com memória temporal injetada. Sem internet ou sem chave, um gerador procedural estático assume sem erros impeditivos (Offline Fallback).
- **MEMÓRIA / RELACIONAMENTOS (WORKING):** Salvamento passivo das afeições, lembranças baseadas em tempo ("passou a noite no parque").
- **SAVE SYSTEM (WORKING):** Estrutura agnóstica salvando transformações e json data localmente.

## SISTEMAS PARCIAIS / EM APRIMORAMENTO
- **SISTEMA DE ANIMAIS:** Base procedural planejada mas atualmente os únicos mobs que movem no mundo são os EnemyActors. Não há animais passivos soltos no mundo livre além dos pássaros ambientes por som.
- **COMPANION CARDS (PARCIAL):** NPCs acompanhantes existem no código, e os cards de "Companheiro" aparecem, mas a lógica profunda deles interagirem dinamicamente no mundo em combates complexos (multi-turnos combinados) carece de ajustes polidos fora do laboratório.

## SISTEMAS NÃO IMPLEMENTADOS / PLANEJADOS
- Eventos de história estritos fixos gigantescos (Ainda foca mais no sandbox sistêmico).
- Corpos com braços 3D para o protagonista e armas brancas físicas (É focado em Deckbuilding no momento).

## ESTADO DE ASSETS EXTERNOS
Foi feita uma extensa melhoria nos assets visuais para texturas triplanares do cenário e densidade da grama (3000 instâncias por bloco). Referências de texturas devem estar listadas em `ASSET_SOURCES.md`. Nenhuma engine pirata.

## BUGS CONHECIDOS E LIMITAÇÕES
- Por conta da quantia maciça de capim no Forward+, máquinas antigas sem redução no `ChunkGenerator.cs` podem sentir framedrops pesados.
- Se o arquivo de sprite configurado no inimigo não tiver fundo cortado limpo, ele renderiza com retângulo visível opaco atrás.
- Ao alternar rapidamente entre a tela de editor de cartas e o retorno, o mouse mode `Captured/Visible` pode dessincronizar pontualmente exigindo abrir o TAB (Menu rápido) para arrumar.

## ARQUIVOS IMPORTANTES (Contexto de Mudanças Recentes)
- `Scripts/World/WorldController.cs`: Recebeu os hooks de Duelos e pontas de interação com POIs.
- `Scripts/Combat/CombatArenaController.cs`: Lógica de animação Tween dos Sprites Customizados nos acertos recebidos.
- `Scripts/Dialogue/DialogueController.cs`: Modificado para adicionar loja e duelos (⚔️ / 💰).
- `Scripts/UI/CardShopController.cs`: Nova tela injetada no passe passado.

## PRÓXIMO PASSO EXATO
Veja o **`NEXT_TASKS.md`**. A prioridade deve estar focada no que o usuário indicar para ser melhorado na área visual/deck, ou novos assets.
