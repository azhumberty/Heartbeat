# ARQUITETURA DE PROJETO HEARTBEAT

O Heartbeat foge de ter dezenas de Scenes `.tscn` lotando o projeto. Em vez disso, a arquitetura é predominantemente construída via **Código C# dinâmico** sob alguns controladores primários orquestradores, injetando UIs e World Nodes na SceneTree conforme necessário, para facilitar hot-reloads modulares e reduzir dependência forte de editor Godot.

## FLUXOS PRINCIPAIS

### WORLD
A exploração do mundo funciona girando ao redor da posição do Jogador.

`WorldController` (Root Node Orquestrador)
→ gerencia `WorldTimeSystem` (Avanço dia/noite)
→ passa coordenadas ao `ChunkManager` (Carregamento espacial / Culling)
→ consulta o `ChunkGenerator` matemático para gerar biomas/POIs.
→ delega o Player, o HUD, a UI (`Tab` Quick Menu).

### PLAYER / INTERAÇÕES
O jogador não possui corpo físico exibido na câmera.
`PlayerController` (CharacterBody3D)
→ Movimentação WASD/Jump/Gravidade.
→ Dispara Raycasts para encontrar NPCs na frente (Mecanismo AimedNpc)
→ Ao cruzar com um NPC, ou POI (via meta "poi_id" em Node3D), o Player envia a requisição de interação (`E`).
→ O `WorldController` bloqueia a câmera (`Locked = true`) e injeta o pop-up de `DialogueController` ou Menu de Interação (POI).

### DIÁLOGOS E I.A. (Groq)
`DialogueController`
→ Recebe a referência da classe puramente lógica do `NpcActor`.
→ Mostra o formulário de Chat 2D sobreposto ao HUD.
→ Lê a entrada do usuário e despacha via Tarefa Assíncrona para o `GameplayServices` / `GroqDialogueProvider`.
→ Resposta volta -> Salva em `MemoryManager` -> Altera status Emocionais (`RelationshipSystem`).
→ Ao voltar ao mundo, NPC continua a agir no Schedule dele.

### COMBATE POR CARTAS & DUELOS
O sistema de combate substitui o espaço 3D temporariamente (Não transporta o player fisicamente para uma nova Godot Scene, mas escurece a tela e joga no Viewport um Arena Fake).

WORLD ENEMY (Ou botão Duelo Amistoso no Diálogo)
→ colide/detecta jogador
→ `WorldController` captura Evento de "EnterCombat"
→ Pula pro `CombatArenaController` (Renderiza SubViewport 3D Fake e as UI de Cartas em CanvasLayer superior)
→ `CombatManager` dita as regras do turno puramente por dados (matemática separada de UI).
→ Resulta em Vitória / Derrota.
→ Retorna ao mundo aplicando recompensas (Xp/Cartas/Reais).

### ARQUITETURA DE ARQUIVOS (Principais)
- **`Models.cs`**: Todo o núcleo de Dados (Saves, PlayerStats, GameSave). Classes "Burras" puras para serialização JSON.
- **`Repositories.cs`**: Onde as leituras/gravações físicas de arquivo acontecem para o Models.
- **`CardModels.cs`**: Mesma coisa que `Models.cs` mas focado 100% nas definitions do Baralho e Combate.
- **`ChunkGenerator.cs` & `ChunkManager.cs`**: Coração procedural 3D.
- **`CardView.cs` & `CardUi.cs`**: As âncoras da interface gráfica procedural para desenhar as cartas 2D escuras/dark fantasy do jogo.
- **`WorldController.cs`**: O super maestro que cria todos na tela.

*(Todo script do Godot deve pertencer ao namespace `Heartbeat`)*
