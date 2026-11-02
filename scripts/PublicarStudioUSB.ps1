param([string]$Destino = (Join-Path $PSScriptRoot "..\dist\NX-STUDIO-USB"), [switch]$Clonar)
# Prepara NX-STUDIO portable para USB: un unico .exe autocontenido + portable.txt.
# Uso: .\scripts\PublicarStudioUSB.ps1 -Destino E:\NX-STUDIO -Clonar
$ErrorActionPreference = "Stop"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..")
$proyecto = Join-Path $raiz "tools\NX-Suite.Studio\NX-Suite.Studio.csproj"
New-Item -ItemType Directory -Force -Path $Destino | Out-Null
$Destino = (Resolve-Path $Destino).Path
Write-Host "Publicando NX-STUDIO en $Destino ..." -ForegroundColor Cyan
dotnet publish $proyecto -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $Destino
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo." }
Set-Content -Path (Join-Path $Destino "portable.txt") -Value "NX-STUDIO portable: datos en .\datos" -Encoding utf8
if ($Clonar) {
    $repo = Join-Path $Destino "NX-SWITE"
    if (Test-Path (Join-Path $repo ".git")) { git -C $repo pull --ff-only }
    else { git clone (git -C $raiz remote get-url origin) $repo }
}
Write-Host "Listo: $Destino" -ForegroundColor Green
if (-not (Test-Path (Join-Path $Destino "NX-SWITE"))) { Write-Host "Aviso: falta la carpeta NX-SWITE junto al .exe (usa -Clonar)." -ForegroundColor Yellow }
Write-Host "Si el PC no tiene Git, copia PortableGit (git-scm.com) en la carpeta PortableGit\." -ForegroundColor Yellow
