<#
.SYNOPSIS
    Genera la build de la beta de NX-Suite lista para distribuir.

.DESCRIPTION
    1. Limpia las carpetas de salida anteriores.
    2. Publica NX-Suite (que a su vez publica el Updater al lado, gracias al target PublicarUpdater).
    3. Elimina archivos innecesarios (.pdb).
    4. Empaqueta todo en un .zip con el nombre NX-Suite-beta-<version>.zip listo para subir a GitHub Releases.

.EXAMPLE
    .\publish-beta.ps1
    .\publish-beta.ps1 -Configuration Release
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64"
)

$ErrorActionPreference = "Stop"

# --- Rutas ---
$RepoRoot   = $PSScriptRoot
$Project    = Join-Path $RepoRoot "NX-Suite\NX-Suite.csproj"
$DistDir    = Join-Path $RepoRoot "dist"
$PublishDir = Join-Path $DistDir  "beta"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " NX-Suite - Publicacion Beta" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# --- 1. Leer la version del .csproj para nombrar el zip ---
[xml]$csproj = Get-Content $Project
$Version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "0.0.0" }
Write-Host "Version detectada: $Version" -ForegroundColor Yellow

# --- 2. Limpiar carpetas anteriores ---
if (Test-Path $PublishDir) {
    Write-Host "Limpiando $PublishDir ..." -ForegroundColor DarkGray
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishDir | Out-Null

# --- 3. Publicar (NX-Suite + Updater via target PublicarUpdater) ---
Write-Host "Publicando NX-Suite ($Configuration / $Runtime) ..." -ForegroundColor Green
dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallado." }

# --- 4. Limpieza: borrar .pdb y otros archivos no necesarios para distribuir ---
Write-Host "Limpiando archivos innecesarios ..." -ForegroundColor DarkGray
Get-ChildItem -Path $PublishDir -Recurse -Include *.pdb, *.xml | Remove-Item -Force -ErrorAction SilentlyContinue

# --- 5. Verificar que estan los dos exe ---
$mainExe    = Join-Path $PublishDir "NX-Suite.exe"
$updaterExe = Join-Path $PublishDir "NX-Suite.Updater.exe"

if (-not (Test-Path $mainExe))    { throw "No se ha generado NX-Suite.exe" }
if (-not (Test-Path $updaterExe)) { throw "No se ha generado NX-Suite.Updater.exe" }

Write-Host ""
Write-Host "Archivos generados:" -ForegroundColor Cyan
Get-ChildItem $PublishDir | Select-Object Name, @{Name="MB";Expression={[math]::Round($_.Length/1MB,2)}} | Format-Table -AutoSize

# --- 6. Crear el ZIP final ---
$ZipName = "NX-Suite-beta-$Version.zip"
$ZipPath = Join-Path $DistDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Write-Host "Comprimiendo en $ZipName ..." -ForegroundColor Green
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal

$ZipSizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " LISTO" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Carpeta : $PublishDir"
Write-Host "ZIP     : $ZipPath  ($ZipSizeMb MB)"
Write-Host ""
Write-Host "Sube el ZIP a GitHub Releases marcandolo como 'Pre-release'." -ForegroundColor Yellow
