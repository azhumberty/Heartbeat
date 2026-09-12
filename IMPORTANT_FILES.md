# MAPA DE ARQUIVOS IMPORTANTES

Apenas os arquivos fundamentais estão listados aqui.

### CORE E ESTADOS
- `Scripts/Models.cs`: Controla o Save e o estado basal RPG (Player/Data/Progressão).
- `Scripts/CardModels.cs`: Controla o modelo das Cartas, do Inimigo no Duelo e o Turno em batalha.
- `Scripts/Repositories.cs`: Persistência estática e banco de dados de fallback para itens/personagens.

### MUNDO E EXPLORAÇÃO
- `Scripts/World/WorldController.cs`: O "Maestro" 3D primário. Adiciona NPCs, lida com Menus e transições pra POIs/Batalhas.
- `Scripts/World/ChunkManager.cs`: Gerencia o mundo visível ativando e desativando zonas do mapa ao redor do jogador.
- `Scripts/World/ChunkGenerator.cs`: A lógica matemática e procedural por trás das árvores, montanhas e grama.
- `Scripts/World/SkyController.cs`: Gerencia iluminação e céus.
- `Scripts/Characters/PlayerController.cs`: Movimento FPS do usuário.

### IA E DIÁLOGO
- `Scripts/Dialogue/DialogueController.cs`: O canvas de conversa 2D que recebe a resposta textual do ator.
- `Scripts/GameplayServices.cs`: Possui a classe `GroqDialogueProvider` que despacha mensagens para a Groq com base na variável `GROQ_API_KEY`.
- `Scripts/NpcActor.cs`: Classe 3D/lógica individual associada a cada NPC instanciado no mapa que avança sua rotina com base na agenda.

### COMBATE E DECK
- `Scripts/Combat/EncounterManager.cs`: Spawna e vigia a detecção de colisões dos monstros procedurais do mapa para chamar o duelo. Contém classe `EnemyVisual` que gerencia a foto 2D.
- `Scripts/Combat/CombatManager.cs`: Motor aritmético/estado puro de turnos (compras de cartas, dano em health, custo de energia).
- `Scripts/Combat/CombatArenaController.cs`: Controla a sub-cena renderizada 3D falsa para as animações de cartas se movendo e danos vermelhos.
- `Scripts/Combat/DeckEditor.cs` e `Scripts/Combat/CardView.cs`: Visualização interativa da interface da grid do baralho.

### SALVAMENTO E UI
- `Scripts/SaveManager.cs`: Grava arquivos em disco do sistema local de Godot.
- `Scripts/UI/CardShopController.cs`: Sub-tela do Mercador responsável por gastar as coins em pacotes de cartas via RNG.
