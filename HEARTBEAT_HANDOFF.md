# HEARTBEAT — HANDOFF ATUAL

**Atualizado em:** 13 de setembro de 2026
**Stack:** Godot 4.7.2 Mono, C# e .NET 8

## Direção atual

Heartbeat é um **Visual Novel / Dating Sim / RPG narrativo 2D** com combate de cartas. Não há exploração em primeira ou terceira pessoa, avatar físico do jogador, cenário 3D ou movimentação WASD. O jogador existe apenas como nome e escolhas no texto.

## O que está implementado neste passe

- Menu principal reduzido às quatro entradas: **Novo Jogo**, **Carregar Jogo**, **Criativo** e **Opções**.
- Novo Jogo cria `WorldLore`, pede um nome textual e salva um Atlas inicial em `GameSave`.
- Novo Jogo usa `GroqWorldLoreProvider` quando a IA está ativa e uma chave está disponível; o lore local continua sendo o fallback imediato. O painel informa claramente qual origem foi usada.
- Configurações agora persistem em `user://settings.json` mesmo antes do primeiro save, permitindo ativar Groq antes de iniciar uma campanha.
- Atlas é uma rede 2D ilustrada e ramificada sobre a arte isométrica do Grok. Cada expedição varia a quantidade, posição, tipo e conexão dos nós, com risco, estado bloqueado/disponível/concluído e aumento de rotas nas camadas posteriores.
- Personagens com `CanBuildRelationship=true` podem evoluir relacionamento e liberar cartas especiais. NPCs genéricos, mercadores e inimigos não acumulam vínculo romântico.
- Combates dão XP e moeda; não dão mais cartas. Cartas são compradas no mercador ou vêm de personagens de vínculo.
- Duelos amistosos mostram o retrato 2D do personagem escolhido na arena e retornam ao Atlas.
- A arena usa HUD superior responsivo, mantém o personagem/inimigo central visível e só abre os detalhes quando uma carta é selecionada; o painel pode ser recolhido.
- O lote de arte do Grok no `Assets/ArtKit` está integrado ao carregador. Ele reconhece JPEGs mantidos com extensão histórica `.png`, evitando erros do Godot sem aumentar o tamanho de todos os arquivos.
- O menu usa `UI/Frames/menu_background.png`, as cartas sem arte própria usam as ilustrações por categoria e oito cartas g41–g55 usam suas faces exclusivas do Grok.
- `CreativeModeController` mantém uma biblioteca simples de cenários, personagens, NPCs, inimigos e mercadores, com metadados e importação de PNG em `user://Content`.
- O Criativo aceita PNG/JPG/WebP, permite editar ID, descrição, bioma, período, peso procedural e atributos de inimigo, além de duplicar e ativar/desativar conteúdo.
- Novas campanhas escolhem cenários e inimigos ativos da biblioteca por peso e seed. Inimigos personalizados levam imagem, nome, Vida, dano, XP e recompensa em Reais para o combate.
- Cadastros personalizados de Personagem, NPC e Mercador agora geram/atualizam pacotes no `CharacterRepository`; sua imagem, descrição, idade, personalidade, fala, função e permissão de relacionamento chegam ao jogo.
- Personagens desativados no Criativo deixam de entrar em novas campanhas sem que seus arquivos sejam apagados. Quando existem personagens ou mercadores personalizados ativos, o Atlas os prioriza.
- A categoria Cartas abre o editor completo existente, que já salva cartas personalizadas na coleção.
- A categoria Personagens oferece um atalho para editar a carta de companheiro e até quatro golpes especiais do personagem salvo. Assets internos devem ser duplicados antes da edição.
- Mercadores personalizados aceitam `ShopStock` no formato `Carta:preço` (ex.: `g01:12, custom_cura:30`). A loja vende essas cartas individualmente e usa os pacotes padrão quando não há estoque válido.
- O diálogo limita cada encontro a oito mensagens do jogador e encerra corretamente quando o painel é fechado.
- Estrada, taverna, descanso e mistério usam `ProceduralEventService`: cada cena oferece duas escolhas, aplica consequências limitadas pelo C# e salva um resumo curto.
- Quando a IA online está ativa, `GroqEventProvider` sugere o texto e as escolhas em JSON estrito; timeout, cancelamento, retry de 429/5xx e fallback local mantêm a campanha jogável.
- O Atlas atualiza bloqueios, cores e caminhos imediatamente depois de concluir um evento, encontro ou combate.
- Ao vencer o chefe, o Atlas gera uma nova expedição determinística com novos IDs e volta a ler os assets ativos do Criativo. O acampamento, o baralho, os relacionamentos e o restante do save permanecem.
- Personagens de relacionamento com afeição 40 ou mais podem ser convidados pelo diálogo para morar no acampamento. Os moradores ficam persistidos no save e aparecem nos eventos de descanso.
- Saves antigos migram para a versão 9, preservando o progresso, o acampamento e o fundo escolhido para a expedição atual.
- O painel inferior de combate usa 196 px e o diálogo usa 228 px de altura útil, mantendo o personagem visível.
- O fluxo iniciado em memória agora passa imediatamente à persistência normal; progresso feito depois de Novo Jogo ou Carregar Jogo é salvo corretamente.
- A entrega `DATING SIM.zip` do Grok foi comparada e fundida seletivamente: mapa do Atlas, cenários finais, Roan, Silas, Minotauro, hub do acampamento e utilitário de chroma foram incorporados. Arquivos mais antigos do ZIP não substituíram os sistemas atuais.
- O combate atual foi preservado. O Minotauro foi adicionado ao catálogo e pode aparecer nas expedições, usando as mesmas regras de cartas, HUD compacto e recompensa somente em Reais.
- A loja de cartas agora controla o próprio fechamento: **Fechar** e **Esc** removem a tela mesmo depois de uma compra e devolvem o mouse ao jogo. O retorno de saldo insuficiente usa uma linha larga e legível.
- Cada nova expedição seleciona e persiste outro fundo entre os cenários ativos do Criativo e os cenários internos. A seed ainda permite reproduzir a campanha, e o fundo anterior não se repete quando há outra opção disponível.
- A integração Groq está ativa nas três superfícies narrativas adequadas: introdução do mundo, eventos procedurais dos nós e diálogo dos personagens. Atlas, economia e combate usam geração local validada para não depender da rede; todos os três fluxos narrativos mantêm fallback offline.
- Roan, Silas e o Minotauro foram reconstruídos a partir das artes do Grok para remover os buracos verdes internos. Os PNGs agora possuem transparência alfa real e preservam os mesmos caminhos usados pelo jogo.

## Arquivos principais

- `Scripts/UI/MainMenuController.cs` — quatro ações principais.
- `Scripts/UI/NewGameController.cs` — introdução, lore e nome.
- `Scripts/WorldLoreProviders.cs` — introdução Groq/offline com JSON estrito e sanitização.
- `Scripts/UI/CreativeModeController.cs` — biblioteca criativa.
- `Scripts/CampaignServices.cs` — lore offline, geração do Atlas e economia centralizada.
- `Scripts/ProceduralEvents.cs` — modelos, geração local determinística, validação e aplicação de escolhas.
- `Scripts/EventProviders.cs` — provedores de evento Groq/offline e schema validado.
- `Scripts/UI/EventController.cs` — painel de narrativa e escolhas sobre o cenário.
- `Scripts/World/AtlasController.cs` — mapa 2D em rede.
- `Scripts/World/WorldController.cs` — orquestra Atlas, VN, diálogo e combate.
- `Scripts/Dialogue/DialogueController.cs` — diálogo online Groq ou fallback offline.
- `Scripts/Combat/CombatManager.Settle.cs` — recompensas de combate.

## Verificação desta etapa

- `dotnet build --no-restore`: concluído com **0 erros e 0 avisos**.
- Godot aberto em modo sem interface com a cena `Scenes/World/World.tscn`: sem erro de inicialização.
- Menu principal iniciado com o lote Grok: sem erros de carregamento no console.
- `Scenes/CampaignChecks.tscn`: `CAMPAIGN_CHECKS_PASS assets=45 nodes=10`, incluindo 24 variações de seed/camada, arte do Atlas, Minotauro, Roan, Silas, nova expedição e acampamento persistente.
- A verificação de campanha também confirma duas a quatro escolhas, persistência do resultado e ausência de recompensa indevida de carta.
- O teste HTTP simulado confirmou retry após 429, clamp de consequências, JSON válido e fallback diante de resposta inválida.
- O controlador do modo Criativo foi instanciado em árvore pelo teste sem erro de interface.
- O teste HTTP simulado da introdução confirmou JSON válido, limites de texto e carregamento do fluxo de Novo Jogo offline.
- A arena 2D foi instanciada pelo teste de campanha depois de iniciar um combate real de QA.
- A tela do mercador foi instanciada pelo teste e o parser confirmou estoque válido, deduplicação e rejeição de entradas incorretas.
- O teste do mercador agora efetua uma compra real, aciona o botão **Fechar** e confirma que a janela saiu da árvore.
- O teste de expedição confirma a troca do fundo do Atlas; o teste de arte confirma alfa transparente no Minotauro reparado.

## Próximas tarefas recomendadas

1. Testar visualmente o fluxo completo em 1280×720 no editor.
2. Ajustar os detalhes de layout encontrados nesse teste visual.

## Regras essenciais

- Nunca colocar `GROQ_API_KEY` no código, logs ou saves.
- Preservar `SaveManager`, personagens, importação de sprites, memórias, diálogo online/offline e compatibilidade de saves.
- Compilar após alterações relevantes.
