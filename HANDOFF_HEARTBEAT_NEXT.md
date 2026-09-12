# Heartbeat — estado para o próximo passe

## Estado atual

Projeto existente em Godot 4.7.2 Mono, C#/.NET 8 e Forward+. A exploração é exclusivamente em primeira pessoa. Save v5, NPCs 2D, criador, guarda-roupa, diálogo Groq, fallback offline, memória social, ciclo de horário, chunks e POIs continuam preservados.

## Fase A concluída

- HUD permanente reduzido a dia/hora, Vida, Mana, mira e interação.
- Menu de ações preservado e aberto por Tab.
- PlayerStats persistente ganhou MaxHealth, Mana, MaxMana, Level, Experience, Damage e Defense, com migração segura.
- Salto no Espaço com gravidade, aceleração no solo, controle reduzido no ar, buffer curto e tolerância em inclinações.
- Terreno procedural contínuo por seed, com relevo, depressões suaves, colisão e trilha adaptada à altura.
- Árvores, arbustos, pedras e NPCs são posicionados sobre o relevo.
- Solo, pedra com musgo e casca usam texturas PBR CC0 1K.
- Vegetação permanece agrupada por MultiMesh e recebeu distâncias de visibilidade por categoria.
- Folhas quadradas experimentais foram removidas após a captura visual.
- Atmosfera dinâmica permanece: ciclo dia/noite, sol, lua, duas camadas de nuvens, neblina e sombras seletivas. SSAO, SSIL e neblina volumétrica foram desligados após benchmark para reduzir custo em GPUs intermediárias.

## Assets

Poly Haven, CC0: Forest Floor por eye-candy.xyz; Mossy Rock e Bark Brown 01 por Rob Tuytel. Cor, normal OpenGL e roughness em JPG 1K. Detalhes e URLs estão em ASSET_SOURCES.md.

## Arquivos principais alterados

- Scripts/World/ChunkGenerator.cs
- Scripts/World/ChunkManager.cs
- Scripts/World/SurfaceMaterials.cs
- Scripts/World/PlayerController.cs
- Scripts/World/WorldController.cs
- Scripts/Models.cs
- Scripts/Repositories.cs
- Scripts/Testing/ImmersionChecks.cs
- Assets/Materials/
- ASSET_SOURCES.md
- README.md

## Validação

- dotnet build: 0 erros e 0 avisos.
- Scenes/ImmersionChecks.tscn: PASS.
- Scenes/SmokeTest.tscn: PASS.
- Scenes/WardrobeChecks.tscn: PASS.
- Scenes/SocialContinuityChecks.tscn: PASS.
- Scenes/PerformanceChecks.tscn: PASS; 41,1 FPS em 1280×720, Vulkan Forward+, RTX 2050, sem V-Sync; streaming para o chunk vizinho aprovado.
- Captura em .qa-cache/immersion-preview.png, produzida com Vulkan Forward+ em NVIDIA RTX 2050 e inspecionada visualmente.

## Limites atuais

- A Fase B ainda não foi iniciada: não há inventário, itens coletáveis, inimigo visível nem entrada/saída de batalha.
- A geometria dos POIs continua simples; os materiais PBR melhoram superfícies, mas o cenário ainda não usa modelos medievais detalhados.
- O benchmark é curto e representa este computador; ainda não há perfil longo em máquinas fracas.
- Vida e Mana persistem e aparecem no HUD, porém ainda não há consumo em combate.

## Próximo passo exato

Executar somente a Fase B do pedido: inventário básico em I, poucos itens simples e coletáveis, um inimigo visível no mundo, transição para uma batalha de cartas pequena e retorno seguro ao mesmo ponto da floresta. Integrar tudo ao save v5 e às barras já criadas. Não iniciar animais, múltiplos monstros ou duelo social, que pertencem à Fase C.
