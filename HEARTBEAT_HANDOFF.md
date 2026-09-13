# HEARTBEAT — HANDOFF ATUAL

**Atualizado em:** 12 de setembro de 2026  
**Stack:** Godot 4.7.2 Mono, C# e .NET 8

## Direção atual

Heartbeat é um **Visual Novel / Dating Sim / RPG narrativo 2D** com combate de cartas. Não há exploração em primeira ou terceira pessoa, avatar físico do jogador, cenário 3D ou movimentação WASD. O jogador existe apenas como nome e escolhas no texto.

## O que está implementado neste passe

- Menu principal reduzido às quatro entradas: **Novo Jogo**, **Carregar Jogo**, **Criativo** e **Opções**.
- Novo Jogo cria `WorldLore`, pede um nome textual e salva um Atlas inicial em `GameSave`.
- Atlas é uma rede 2D persistente de nós: estrada, mercador, floresta, taverna, acampamento, cavaleiro e ruínas. Nós têm estado bloqueado, disponível ou concluído.
- Personagens com `CanBuildRelationship=true` podem evoluir relacionamento e liberar cartas especiais. NPCs genéricos, mercadores e inimigos não acumulam vínculo romântico.
- Combates dão XP e moeda; não dão mais cartas. Cartas são compradas no mercador ou vêm de personagens de vínculo.
- Duelos amistosos mostram o retrato 2D do personagem escolhido na arena e retornam ao Atlas.
- `CreativeModeController` mantém uma biblioteca simples de cenários, personagens, NPCs, inimigos e mercadores, com metadados e importação de PNG em `user://Content`.
- O diálogo limita cada encontro a oito mensagens do jogador e encerra corretamente quando o painel é fechado.

## Arquivos principais

- `Scripts/UI/MainMenuController.cs` — quatro ações principais.
- `Scripts/UI/NewGameController.cs` — introdução, lore e nome.
- `Scripts/UI/CreativeModeController.cs` — biblioteca criativa.
- `Scripts/CampaignServices.cs` — lore offline, geração do Atlas e economia centralizada.
- `Scripts/World/AtlasController.cs` — mapa 2D em rede.
- `Scripts/World/WorldController.cs` — orquestra Atlas, VN, diálogo e combate.
- `Scripts/Dialogue/DialogueController.cs` — diálogo online Groq ou fallback offline.
- `Scripts/Combat/CombatManager.Settle.cs` — recompensas de combate.

## Verificação desta etapa

- `dotnet build --no-restore -v:q`: concluído com 0 erros e 3 avisos já existentes de nulidade em `ImageManager.cs` e `CombatArenaController.UI.cs`.
- Godot aberto em modo sem interface com a cena `Scenes/World/World.tscn`: sem erro de inicialização.

## Próximas tarefas recomendadas

1. Refinar cada tipo de nó do Atlas em eventos narrativos próprios e concluir nós apenas após a escolha ou vitória correspondente.
2. Ligar a biblioteca Criativo aos seletores de conteúdo dos eventos, inimigos e mercadores.
3. Polir a tela de combate 2D e o editor de baralho mantendo a arte principal visível.
4. Criar o gerador opcional de eventos por Groq com JSON validado e fallback local, usando contexto curto.

## Regras essenciais

- Nunca colocar `GROQ_API_KEY` no código, logs ou saves.
- Preservar `SaveManager`, personagens, importação de sprites, memórias, diálogo online/offline e compatibilidade de saves.
- Compilar após alterações relevantes.
