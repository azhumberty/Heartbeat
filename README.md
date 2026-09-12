# Heartbeat - Dating Sim MVP

**Novo:** [guia do guarda-roupa, recorte local e conversa livre](GUARDA-ROUPA.md).

Um protótipo de dating sim focado em conversas geradas por IA (Groq) integradas a um mundo procedural e sistemas de progressão de relacionamento clássicos.

## Funcionalidades Atuais

- **Menu Principal e Save System (v5)**: Slots, backups automáticos, migração automática e relógio/câmera persistentes.
- **Floresta Procedural em Chunks**: geração determinística, trilhas conectadas, vegetação agrupada e streaming ao redor do jogador.
- **Locais completos**: cabana acessível, acampamento, ruína e abrigo são prefabs reutilizáveis com pontos preparados para eventos sociais.
- **Sistema de NPCs Vivos**:
  - Simulador lógico separado da representação visual.
  - Rotinas baseadas em horário (Manhã, Tarde, Noite).
  - NPCs caminham pelo mundo em busca de Activity Points (pontos de descanso, trabalho, socialização).
  - Animação por Sprite Sheets com direção relativa à câmera e fallback procedural para animações ausentes (talk_X -> idle_X -> walk_X).
- **Diálogos com IA (Groq)**: Prompt injeta personalidade, memórias, relacionamento atual e a emoção anterior.
- **Sistema Offline / Fallback Procedural**: Funciona localmente sem internet com regras de intenção.
- **Progressão de Relacionamento**: Afeto, Confiança, Romance. Respostas repetidas não geram ganho infinito.
- **Editor de Personagens Completo (In-Game)**: 
  - Criação de NPCs diretamente no jogo com interface Godot.
  - Importação de retratos PNG/JPG/WebP com remoção de fundo (transparência).
  - Importação de Sprite Sheets com preview de animação e grid overlay.
- **Memórias Dinâmicas e Eventos**: O jogo lembra interações recentes. Eventos especiais (como encontro no parque) são desbloqueados baseados em condições.
- **Continuidade social local**: memórias confirmadas, fatos do jogador, resumo persistente, personalidade dimensional e emoções simuladas continuam funcionando mesmo quando a IA online cai.
- **Galeria de Momentos**: Reveja interações importantes.

## Controles

- **WASD**: caminhar.
- **Shift**: correr.
- **Espaço**: pular; o salto tem tolerância curta para responder bem em inclinações.
- **Mouse**: clique para capturar; mova para olhar; Esc libera.
- **E**: conversar com o personagem para o qual você está olhando.
- **Tab**: abre o menu de ações, onde ficam tempo, salvar, personagens, galeria e configurações.

O céu, sol, lua, estrelas, duas camadas de nuvens, iluminação artificial e
períodos dos NPCs respondem ao relógio contínuo. O horário, velocidade, pausa
e posição do jogador são preservados no save v5.

- **Enter / Shift+Enter**: Enviar mensagem / quebrar linha.
- **Esc**: Fechar janelas ou cancelar conversas.

O HUD durante a exploração mostra apenas dia/hora, Vida, Mana, mira e o aviso de interação. Vida e Mana fazem parte do save e já estão preparados para os sistemas de itens e combate.

## Configuração da API (Groq)

1. Crie uma conta em [Groq](https://console.groq.com/) e obtenha uma API Key.
2. Defina a variável de ambiente `GROQ_API_KEY` com a sua chave.
3. No jogo, clique em "Config." e ative a "IA Online".
4. *Nota: As chaves nunca são salvas nos arquivos `.json` do jogo para evitar vazamentos.*

## Tecnologias e Arquitetura

- **Godot 4.7.2** com .NET 8 (C#).
- Todo o código reside na pasta `Scripts/`, e os assets e salvamentos vão para `user://Characters` e `user://saves`.
- Padrão arquitetural voltado à testabilidade (`SmokeTest.cs` disponível para validações contínuas).
- `project.godot` usa Forward+ para sombras e neblina atmosférica otimizada.

## Como adicionar NPCs customizados

Você pode criar NPCs diretamente pelo botão **Personagens** no menu principal. Se preferir fazer manualmente:
1. Crie uma pasta dentro de `user://Characters/seu-npc-id/`.
2. Adicione um `character.json` seguindo a estrutura vista nos personagens de demonstração (`daniel-demo` e `lucas-demo`).
3. Suas sprite sheets importadas irão para a subpasta `sprites/`.
