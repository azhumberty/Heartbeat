# Avaliação curta de provedores de personagens

## Decisão atual

O Heartbeat continua usando **Groq + memória persistente local**. `IDialogueProvider` já permite trocar o backend sem acoplar o estado do NPC ao serviço. Personalidade, emoções, fatos, relacionamento e resumos pertencem ao save; o provedor recebe apenas o contexto relevante.

## Convai

A documentação oficial descreve memória entre sessões, personalidade, estado mental, contexto dinâmico e APIs de interação. Isso combina com o conceito do jogo. Porém, a documentação de plugins destaca Unity, Unreal e Web; uma integração Godot exigiria trabalho direto com a API. Alguns recursos de inspeção são ligados a planos específicos e os limites de Speaker IDs variam por plano. Portanto, Convai fica como experimento futuro opcional e não como dependência do protótipo.

Fontes oficiais:

- Memória: https://docs.convai.com/api-docs/convai-playground/character-customization/memory
- Personalização: https://docs.convai.com/api-docs/convai-playground/character-customization
- API principal: https://docs.convai.com/api-docs/api-reference/core-api-reference
- Limites de memória/identidade: https://docs.convai.com/api-docs/plugins-and-integrations/unity-plugin/utilities/long-term-memory

Nenhuma conta, assinatura ou chave da Convai foi criada ou adicionada ao projeto.
