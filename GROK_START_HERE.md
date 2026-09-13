# BEM-VINDO AO PROJETO HEARTBEAT

**Você está assumindo um projeto existente. Não recrie o projeto e não substitua sistemas funcionais sem primeiro analisar a implementação atual.**

## SOBRE O PROJETO
**Heartbeat** é uma Visual Novel / Dating Sim / RPG narrativo 2D com Card Game e Atlas procedural de expedições, ambientado em um mundo *dark fantasy*. O jogador existe como nome e escolhas; cenários e personagens são imagens 2D. Não há exploração 3D, FPS, WASD ou avatar.

## INFORMAÇÕES TÉCNICAS
- **Engine:** Godot 4.7.2 Forward+
- **Linguagem:** C# (.NET 8)
- **Diretório Principal:** `C:\Users\azhum\OneDrive\Documentos\ChatGPT\DATING SIM`
- **Cena Inicial:** `Scenes/MainMenu/MainMenu.tscn`

## ORDEM DE LEITURA OBRIGATÓRIA ANTES DE EDITAR
Para assumir o projeto de forma coesa e evitar refactors destrutivos, leia os seguintes arquivos na ordem:

1. `GROK_START_HERE.md` (Você está aqui)
2. `AGENTS.md` (Regras de desenvolvimento e continuidade)
3. `HEARTBEAT_HANDOFF.md` (Estado detalhado e vigente de todas as features)
4. `PROJECT_ARCHITECTURE.md` (Como os sistemas de mundo e UI se conectam)
5. `CURRENT_SYSTEMS.md` (Checklist rápido do status de cada funcionalidade)
6. `NEXT_TASKS.md` (A lista hierárquica do próximo trabalho que você deve executar)

## COMO COMPILAR E TESTAR
- **Build (Terminal):** `dotnet build "Heartbeat.csproj"`
- **Testes:** Há cenas de teste na pasta `Scenes/` que podem ser executadas, como `Scenes/ImmersionChecks.tscn`. Para executar o jogo inteiro a partir do menu, execute a engine Godot mirando o arquivo `project.godot`.

## PRÓXIMA TAREFA IMEDIATA
Consulte **`NEXT_TASKS.md`** para iniciar a tarefa **P1**. 

## REGRAS CRÍTICAS
- A API da Groq usa estritamente a variável de ambiente `GROQ_API_KEY`. **Nunca adicione chaves em código ou arquivos.**
- Tudo é construído para cenas 2D, narrativa por escolhas, combate de cartas e expedições geradas pelo `AtlasGenerator`.
- Leia o arquivo `HEARTBEAT_HANDOFF.md` para entender exatamente onde o agente anterior parou.
