# STATUS DOS SISTEMAS

Tabela com a matriz do status das funcionalidades e módulos do código. A intenção disso é evitar recriar ou duplicar sistemas. 
**Importante:** Se está "WORKING", presuma que cumpre sua função sem bugar a build. "PARTIAL" significa que está incompleto ou frágil.

| Sistema | Estado | Notas |
|---|---|---|
| FPS Controller | **WORKING** | WASD normal, corre. Single jump incluído. |
| Dia / Noite | **WORKING** | Modifica clima e iluminação, avança relógio gradativo. |
| Sky / Iluminação | **WORKING** | Volumetric Fog adaptativa e triplanar textures implementados. |
| Floresta (Terrain) | **WORKING** | Usa FastNoiseLite. Grama ativada com partículas intensas. |
| Procedural Chunks | **WORKING** | Instancia blocos adjacentes e libera a memória dos longe. |
| POIs (Pontos Inter.) | **WORKING** | Ruínas, Fogueiras e Cabanas podem ser interagidas no mundo. |
| NPC Spawning / Rotina | **WORKING** | Baseado na agenda do json do personagem, se move entre chunks. |
| NPC Dialogue | **WORKING** | Misto de botões e input text. |
| Groq LLM API | **WORKING** | Conectado dinamicamente via httpClient. |
| Offline Fallback | **WORKING** | Gerador estático se não tiver internet ou API Key. |
| Memória & Emoção | **WORKING** | Salva logs das conversas no SaveData JSON para contexto no LLM. |
| Relações (Dating) | **WORKING** | Corações de afeto progridem se elogiados. |
| Importador de Sprites | **WORKING** | Backgrounds brancos são ignorados via Godot mask ou script. |
| Character Creator | **WORKING** | Permite alterar arquétipo do player no menu UI. |
| Inventário (Cartas) | **WORKING** | `DeckEditor` refatorado em Grid com Reais/Moedas visíveis. |
| Health / Mana / Level| **WORKING** | Stats puramente atrelados a cartas de consumo e UI inferior. |
| Animais Passivos | *PLANNED* | Não há gado ou pássaros físicos circulando. |
| Monstros/Enemies | **WORKING** | Sistema `EnemyVisual` gera custom Sprites 2D ou malhas primitivas. |
| Enemy Encounters | **WORKING** | Spawna no mundo, persegue o player ativando instâncias de arena. |
| Batalha por Cartas | **WORKING** | Combate em turnos por cartas com status e intent visual. |
| NPC Duels | **WORKING** | Pode-se lutar amistosamente sem penalidade mortal por coins. |
| Loja de NPCs | **WORKING** | Menu para trocar moedas por pacotes RNG de novas cartas. |
| Companion Cards | *PARTIAL* | Código base para jogar cartas de suporte dos NPCs, carece regras. |
| Saving / Loading | **WORKING** | Arquivo agnóstico local em disco. |
| Audio/SFX | *PARTIAL* | Usa ruído ambiente floresta, falta sons detalhados por ataques. |
| Testes Unitários | **WORKING** | Arquivos separados em `/Testing`. |
