# Guarda-roupa e recorte local

No jogo: **Personagens → Guarda-roupa · criar com 1 a 3 imagens**.

1. Selecione o personagem ou crie um novo, informando nome e idade.
2. Abra o guarda-roupa, clique em **Adicionar** e dê um nome à roupa.
3. Envie **Parado** (corpo inteiro). Opcionalmente envie **Passo esquerdo**
   e **Passo direito**, com o mesmo enquadramento e roupa.
4. Imagens opacas iniciam o recorte automaticamente quando o runtime está
   instalado. PNGs com transparência podem ser mantidos. O botão de remoção
   permite repetir o processo. A instalação inicial está em `Tools/Install-Cutout.ps1`.
5. Confira **No mundo**. Em **Retocar recorte**, use Apagar, Restaurar,
   Desfazer e Resetar. Clique em **Aplicar retoque ao preview**.
6. Ajuste altura e apoio dos pés. Correções por pose e ritmo ficam em
   **Ajustes avançados**. Os passos usam a referência da pose parada;
   cada pose não é recortada nem esticada individualmente.
7. Clique em **Usar esta roupa** e **Salvar guarda-roupa**. Feche o criador
   para atualizar o mundo. Salvar no mundo mantém a roupa atual no slot.

A roupa Padrão preserva o visual antigo, inclusive sprite sheets direcionais.
As roupas novas usam recortes que acompanham a câmera no eixo vertical.
Uma imagem oferece respiração e deslocamento estilizado. Os dois passos
oferecem alternância simples: não são uma caminhada 3D reconstruída.

Originais ficam intactos no pacote do personagem. Recortes e máscaras são
arquivos separados identificados por conteúdo. Excluir roupa não apaga imagens.
Os arquivos de trabalho são reduzidos a até 1600 pixels no lado maior para
retocar sem carregar fotos gigantes; o original permanece na resolução recebida.
Alterações não salvas no editor podem ser descartadas ao fechar o criador.

## Diálogo

Não foi encontrado filtro local geral de palavras adultas no provider existente.
Havia bloqueio de envio quando acabavam as ações e fallback genérico sem
tratamento específico de flerte. Agora a conversa continua sem ações, mas
sem progresso adicional. Flertes offline consideram confiança, afeto, energia
e stress. Recusas estruturadas do provedor aparecem identificadas e recebem
alternativa offline. Os limites do serviço online continuam válidos.

## Cenário

Madeira, alvenaria, asfalto, piso e vegetação usam materiais procedurais
compartilhados, em coordenadas do mundo. Não houve download de assets,
mudança da seed nem mudança das colisões por causa das texturas.

## Verificação

`Scenes/WardrobeChecks.tscn` usa um personagem temporário e verifica migração,
alpha, máscara, desfazer/restaurar, original intacto, reabertura de pacote e save,
preview/mundo, alternância de passos, fallback de uma imagem e conversa sem ações.
Os testes de 429, JSON inválido e recusa de IA usam HTTP simulado, sem consumir API.
Executar com `-- --capture` salva a captura em `.qa-cache/wardrobe-preview.png`.

Validação concluída em 12/09/2026: compilação sem erros/avisos, todos os testes
do guarda-roupa aprovados e inspeção visual do editor. Runtime rembg e modelo
U2NETP instalados neste computador. O teste de recorte recortou uma foto existente
do projeto com alpha e confirmou reutilização de cache. O original foi preservado.
As texturas renderizaram no GL Compatibility; saídas da rua e colisão do café
foram verificadas. Não houve chamada real à IA online nesta rodada, nem medição
comparativa de desempenho.

A foto solta em Characters, os artefatos temporários de validação e a cópia
antiga do projeto foram removidos na limpeza solicitada.
Para repetir o teste de recorte com outra imagem, use `--cutout=caminho-da-imagem`
após `--`. Nenhuma foto de personagem é necessária para compilar o projeto.

Principais arquivos: `Scripts/Characters/Wardrobe.cs`, `LocalCutoutService.cs`,
`Scripts/UI/WardrobeEditor.cs`, `CutoutCanvas.cs`, `Scripts/World/NpcAnimator.cs`,
`SurfaceMaterials.cs`, `Scripts/GameplayServices.cs` e `Scripts/Dialogue/DialogueController.cs`.

Limites: qualidade do recorte varia por imagem; sem geração de poses faltantes,
sem reconstrução de costas/perfil e sem sincronização labial. O runtime externo
precisa ser distribuído/configurado separadamente para exportar o jogo a outro PC.
