# REGRAS UNIVERSAIS PARA AGENTES (LLMs)

Este arquivo define o protocolo padrão de trabalho aplicável ao **Codex, Gemini/Antigravity, Grok, Claude** e outros LLMs que assumirem o projeto Heartbeat.

## DIRETRIZES DE CONTINUIDADE
1. **Preservar Sistemas Existentes:** Não reescreva arquivos grandes sem necessidade e nunca substitua sistemas funcionais por outros (não recrie a roda).
2. **Analisar Antes de Editar:** Leia os scripts fundamentais afetados por um pedido antes de aplicar modificações usando ferramentas de regex/replace.
3. **Não Migrar de Godot:** O projeto é estritamente **Godot 4.7.2 Mono / C# / .NET 8**. Não sugira nem tente converter para GDScript, C++, Unity ou Unreal sem autorização explícita.
4. **Visão Fixa:** O jogo é **sempre** em primeira pessoa. Atualmente não há corpo/braços visíveis para o jogador. Preserve isso salvo pedido explícito para alterar.
5. **Alergia à Pirataria:** Não utilize assets (modelos 3D, sons, imagens) ripados de outros jogos comerciais. 
6. **Performance Sempre:** Priorize a performance em C#. Evite coletas de lixo excessivas em loops processuais, especialmente no `ChunkGenerator` e `EncounterManager`.

## ELEMENTOS INQUEBRÁVEIS
Você **deve obrigatoriamente preservar e não quebrar**:
- A compatibilidade do Save System (`SaveManager.cs`).
- O Criador de Personagens.
- Os NPCs, a importação de sprites estáticos/animados deles, e suas memórias estruturais.
- O diálogo misto (Online Groq / Offline Fallback).
- A geração de Chunks procedurais e Culling.
- O ciclo de tempo (Dia/Noite e Skyboxes/Fog associados).

## TESTES E INTEGRIDADE
- Sempre **compile** usando `dotnet build` após fazer mudanças relevantes.
- Não declare que algo "está concluído e funcionando" se você não tiver certeza técnica ou não tiver checado se o código compilou.
- Execute os testes automatizados ou carregue as cenas de QA quando alterar lógicas fundamentais.

## SEGURANÇA E SEGREDOS
- **NUNCA INSIRA API KEYS NO CÓDIGO.**
- Tokens (como o `GROQ_API_KEY` para os NPCs) são resgatados exclusivamente pelo ambiente operacional do usuário usando `System.Environment.GetEnvironmentVariable("GROQ_API_KEY")`. Não salve, faça log ou cache de chaves brutas.

## GERENCIAMENTO DE ASSETS
- Todo asset externo recém introduzido (textura, fonte, áudio) deve ser registrado no arquivo `ASSET_SOURCES.md` com origem, URL, licença e propósito.
- Prefira usar ferramentas geométricas primitivas ou shaders criativos quando os arquivos pesarem muito ou demandarem polígonos extras sem impacto visual significativo.
