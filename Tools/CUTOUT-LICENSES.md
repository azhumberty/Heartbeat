# Recorte local

O ambiente auxiliar fica em `.tools/rembg`, separado do jogo e ignorado pelo Git.
Usa rembg 2.0.67 (MIT, Daniel Gatis) e o modelo pequeno U2NETP, do projeto
U-2-Net (Apache 2.0, Xuebin Qin e colaboradores). Não usa serviço de fotos na nuvem.
O primeiro processamento baixa os pesos pelo mecanismo do rembg; os demais
reutilizam `.tools/models`. Não se altera o modelo.

Fontes e licenças dos projetos:
- https://github.com/danielgatis/rembg/blob/main/LICENSE.txt
- https://github.com/xuebinqin/U-2-Net/blob/master/LICENSE

Os pacotes instalados retêm suas licenças no ambiente Python. Ao distribuir
o runtime/modelo junto de um executável, incluir também seus textos de licença
e avisos; este protótipo inclui o instalador, não os binários no controle de versão.

Instalação Windows: executar `Tools/Install-Cutout.ps1` com Python 3.11–3.13.
É possível fornecer `-Python 'C:\caminho\python.exe'`.
Um runtime alternativo pode ser indicado pela variável `HEARTBEAT_PYTHON`.
As dependências são baixadas na instalação; uma imagem enviada ao criador nunca
é transmitida para um serviço externo pelo removedor.
