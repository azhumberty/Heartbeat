# Handoff do projeto Heartbeat

Use este documento como contexto para outro chat criar o próximo prompt de desenvolvimento do jogo. Não trate este texto como prova de que funcionalidades futuras já existem; ele descreve o que foi solicitado, implementado e testado até 12/09/2026.

## Pedido para o outro chat

Com base neste documento, gere um prompt único, claro e econômico para continuar o desenvolvimento do projeto existente **Heartbeat**. O prompt deve orientar o agente a primeiro analisar os arquivos atuais, preservar sistemas que funcionam, implementar um conjunto pequeno e coerente de melhorias, compilar e testar antes de concluir. Não peça para recriar o projeto do zero.

Priorize o próximo passo visual e jogável mais importante. Evite solicitar dezenas de sistemas ao mesmo tempo. Separe claramente requisitos obrigatórios, critérios de aceitação, testes e itens que devem ficar para etapas futuras.

## Visão do jogo

Heartbeat é um RPG social/dating sim para desktop feito no Godot 4 com C#/.NET. O jogador explora um mundo 3D em primeira pessoa e encontra NPCs adultos fictícios representados por sprites 2D animados dentro do ambiente 3D. Os NPCs possuem personalidade, rotina, desejos, memórias, relacionamento, roupas e diálogos livres.

O objetivo visual é natural, realista e levemente cinematográfico. O projeto deve continuar viável para um protótipo independente, usando soluções simples e eficientes em vez de sistemas AAA.

## Ambiente técnico

- Godot 4.7.2 Mono.
- C# e .NET 8.
- Renderização Forward+ no desktop.
- Projeto localizado em `C:\Users\azhum\OneDrive\Documentos\ChatGPT\DATING SIM`.
- Cena inicial configurada: `Scenes/MainMenu/MainMenu.tscn`.
- Mundo: `Scenes/World/World.tscn`.
- O projeto deve ser compilado com `dotnet build`.
- Não migrar para Unreal ou Unity nesta fase.

## Sistemas já existentes e que devem ser preservados

- Menu principal separado.
- Save version 5, migração e backup de saves.
- Mundo determinístico dividido em chunks com carregamento ao redor do jogador.
- Ciclo contínuo de dia e noite, velocidades de tempo e pausa.
- Sol, lua, estrelas, nuvens e iluminação que respondem ao horário.
- NPCs com estado lógico separado da representação visual.
- Rotinas, pontos de atividade e movimentação de NPCs.
- NPCs 2D dentro do mundo 3D usando imagens e sprite sheets.
- Criador de personagens e editor de guarda-roupa.
- Importação de uma a três imagens por roupa.
- Recorte de fundo local com edição de máscara, preview e preservação do original.
- Importador de sprite sheets com preview animado.
- Diálogo online por Groq quando configurado.
- Diálogo procedural offline como fallback.
- Memórias, desejos, afeição, confiança, romance, atração, energia e estresse.
- Galeria e evento social existente.

## Segurança da IA

- A chave da Groq nunca deve ser colocada em código, documentação, save ou prompt.
- A chave deve vir somente da variável de ambiente `GROQ_API_KEY`.
- Uma chave foi compartilhada anteriormente no chat e não foi copiada para este documento. É recomendável revogá-la e gerar outra.
- O jogo não deve prometer IA online quando não houver chave válida.
- Erros, timeout, resposta inválida e limite de requisições devem cair automaticamente no diálogo offline.
- Personagens devem ser adultos fictícios. Não identificar pessoas reais nem inferir atributos pessoais pela aparência.

## Estado atual do mundo

A antiga cidade deixou de ser o cenário inicial. O jogo agora carrega uma floresta procedural determinística contendo:

- trilha contínua entre chunks;
- árvores de diferentes alturas;
- árvores de copa larga e coníferas;
- arbustos, grama, pedras e elevações visuais;
- troncos caídos com colisão;
- vegetação agrupada com MultiMesh;
- colisões concentradas em objetos próximos;
- neblina normal e volumétrica;
- SSAO e SSIL;
- vento sutil na vegetação;
- som ambiente de vento sintetizado localmente;
- horizonte florestal instanciado;
- iluminação adaptada ao ciclo de horário.

Não foram usados assets externos nessa etapa. O registro está em `ASSET_SOURCES.md`.

## Primeira pessoa

O jogo agora funciona somente em primeira pessoa:

- WASD movimenta;
- Shift corre;
- clique captura o mouse;
- mouse controla a câmera;
- Escape libera o mouse ou fecha interfaces;
- E interage com o NPC para o qual o jogador está olhando;
- não existe câmera de terceira pessoa;
- não aparecem corpo, braços ou mãos;
- existe raycast central para interação.

## Locais de interesse

O procedural não monta mais edifícios incompletos parede por parede. Ele escolhe onde posicionar cenas reutilizáveis completas:

- cabana acessível no chunk inicial;
- acampamento;
- pequena ruína;
- abrigo.

Esses locais possuem colisão, chão e objetos próprios. Cabana e abrigo possuem cobertura e iluminação. Cada POI recebe metadados `poi_id` e `event_hook`, preparando a relação:

`POI → NPC possível → evento → diálogo → recompensa ou relacionamento`

Não existem mais nomes de prédios ou lugares flutuando no mundo. O nome acima de um NPC ainda pode aparecer como parte da apresentação do personagem.

## Arquivos centrais da etapa da floresta

- `Scripts/World/ChunkGenerator.cs`: geração da floresta, distribuição de vegetação e escolha de POIs.
- `Scripts/World/ChunkManager.cs`: streaming, MultiMesh, colisões e instanciação dos POIs.
- `Scripts/World/ForestPoi.cs`: conteúdo das quatro cenas reutilizáveis.
- `Scripts/World/ForestAmbience.cs`: camada simples de vento sintetizado.
- `Scripts/World/PlayerController.cs`: controlador exclusivo de primeira pessoa.
- `Scripts/World/SkyController.cs`: atmosfera, neblina, céu e horizonte florestal.
- `Scripts/World/SurfaceMaterials.cs`: materiais procedurais e vento da vegetação.
- `Scripts/World/WorldController.cs`: integração do mundo, NPCs, UI e interação.
- `Scenes/World/POI/ForestCabin.tscn`.
- `Scenes/World/POI/ForestCamp.tscn`.
- `Scenes/World/POI/ForestRuin.tscn`.
- `Scenes/World/POI/ForestShelter.tscn`.
- `Scripts/Testing/ImmersionChecks.cs`.
- `project.godot`.

## Situação visual real

A floresta está jogável, densa e atmosférica, mas as árvores, pedras e construções ainda são formadas principalmente por primitivas e shaders internos. O resultado é adequado para validar o gameplay, porém ainda possui aparência de protótipo. O próximo salto visual deve vir de assets CC0 realistas, terreno melhor e materiais PBR.

Evite aumentar muito o tamanho do mundo antes de melhorar a qualidade do trecho inicial. É preferível aprimorar uma área pequena e demonstrável.

## Assets externos permitidos

É permitido procurar e integrar assets gratuitos, priorizando:

1. CC0 e domínio público;
2. assets explicitamente liberados para uso comercial;
3. Godot Asset Library;
4. Poly Haven e fontes confiáveis semelhantes.

Nunca usar conteúdo pirateado ou extraído de outros jogos. Todo asset adicionado precisa ser registrado em `ASSET_SOURCES.md` com nome, origem, link e licença. Prefira poucos assets coerentes e otimizados.

## Melhor próximo milestone sugerido

Criar uma **clareira vertical slice realista** ao redor da cabana, preservando o procedural ao redor. Um bom escopo seria:

- integrar um pequeno conjunto coerente de árvores, pedras e vegetação CC0;
- criar LOD ou impostores simples para árvores distantes;
- melhorar o terreno com relevo suave, folhas e materiais PBR;
- melhorar a cabana com materiais, porta, janelas e interior mais convincente;
- criar um evento social curto na cabana ou no acampamento;
- manter FPS estável e sem travamentos de streaming.

Não incluir clima complexo, combate, inventário amplo, crafting, mapa gigantesco ou dezenas de eventos nesse mesmo passe.

## Critérios recomendados para o próximo prompt

O agente que receber o prompt deve:

1. Ler os arquivos relevantes antes de editar.
2. Explicar em poucas linhas o que encontrou e qual conjunto pequeno será implementado.
3. Preservar saves, criador de personagens, guarda-roupa, NPCs e diálogo.
4. Não reescrever arquivos inteiros sem necessidade.
5. Registrar licenças de qualquer asset externo.
6. Compilar com `dotnet build` e corrigir todos os erros.
7. Executar `Scenes/ImmersionChecks.tscn`, `Scenes/SmokeTest.tscn` e `Scenes/WardrobeChecks.tscn`.
8. Fazer uma captura visual e inspecioná-la.
9. Não afirmar que algo funciona sem ter sido testado.
10. Entregar resumo curto dos arquivos, testes, limitações e três próximos passos.

## Verificações concluídas na versão atual

- `dotnet build`: passou com zero erros e zero avisos.
- `Scenes/ImmersionChecks.tscn`: passou.
- `Scenes/SmokeTest.tscn`: passou.
- `Scenes/WardrobeChecks.tscn`: passou.
- Forward+ foi executado com Vulkan em uma NVIDIA GeForce RTX 2050.
- A captura visual está em `.qa-cache/immersion-preview.png` e serve somente para revisão local.

## Limitações conhecidas

- Não houve benchmark prolongado de FPS durante exploração extensa.
- O áudio ambiental atual é simples e sintetizado.
- Os POIs ainda precisam de acabamento visual e narrativo.
- O terreno é funcional, mas ainda não possui um sistema avançado de heightmap ou navegação.
- Não foi feita uma chamada real à IA online nesta última etapa; os testes de provedor usaram respostas simuladas e o fallback offline.
- Rotinas antigas ainda usam nomes lógicos como `Cafe`, `Park` e `Street` internamente para compatibilidade, embora agora representem cabana, clareira e trilha.
