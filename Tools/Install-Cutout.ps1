param([string]$Python = "python")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtimeFolder = Join-Path $projectRoot '.tools/rembg'
$runtimePython = Join-Path $runtimeFolder 'Scripts/python.exe'
if (!(Test-Path -LiteralPath $runtimePython)) {
    & $Python -m venv $runtimeFolder
    if ($LASTEXITCODE -ne 0) { throw 'Instale Python 3.11 a 3.13 e execute novamente.' }
}
& $runtimePython -m pip install --disable-pip-version-check --only-binary=:all: 'rembg[cpu]==2.0.67'
if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar o recorte local.' }
Write-Host 'Recorte instalado. O primeiro uso baixa o modelo U2NETP; as fotos permanecem no computador.'
