<#
.SYNOPSIS
    Simula una actualización de NX-SWITE sin publicar release ni tocar el catálogo.

.EXAMPLE
    .\scripts\ProbarActualizacion.ps1            # actualización normal
    .\scripts\ProbarActualizacion.ps1 -ForzarError     # fuerza un fallo para probar el rollback
    .\scripts\ProbarActualizacion.ps1 -Limpiar   # desactiva la simulación y borra temporales
#>
param(
    [switch]$ForzarError,
    [switch]$Limpiar,
    [switch]$Estado
)

$ErrorActionPreference = 'Stop'
$raiz   = Split-Path $PSScriptRoot -Parent
$bin    = Join-Path $raiz 'NX-Suite\bin\Debug\net8.0-windows'
$test   = Join-Path $env:TEMP 'NXTest'
$zip    = Join-Path $env:TEMP 'NX-Prueba.zip'
$json   = Join-Path $env:APPDATA 'NX-Swite\actualizacion-prueba.json'
$log    = Join-Path $env:TEMP 'NX-Suite-Updater.log'

if ($Estado) {
    if (Test-Path $json) { Write-Host 'Simulación ACTIVADA (solo afecta a la copia de prueba en' $test ')' -ForegroundColor Yellow }
    else { Write-Host 'Simulación DESACTIVADA' -ForegroundColor Green }
    return
}

if ($Limpiar) {
    Get-Process NX-Swite -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$test*" } | Stop-Process -Force
    Remove-Item $json, $zip -Force -ErrorAction SilentlyContinue
    Remove-Item $test -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Simulación desactivada y temporales eliminados.' -ForegroundColor Green
    return
}

Write-Host '1/4 Compilando...' -ForegroundColor Cyan
dotnet build (Join-Path $raiz 'NX-Suite\NX-Suite.csproj') -nologo -v:q
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }

Write-Host '2/4 Preparando instalación de prueba en' $test -ForegroundColor Cyan
Remove-Item $test -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $bin $test -Recurse

Write-Host '3/4 Creando ZIP de actualización (incluye PRUEBA.txt como marcador)...' -ForegroundColor Cyan
$staging = Join-Path $env:TEMP 'NX-Prueba-staging'
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $bin $staging -Recurse
"Actualización simulada $(Get-Date)" | Set-Content (Join-Path $staging 'PRUEBA.txt')
Compress-Archive "$staging\*" $zip -Force
Remove-Item $staging -Recurse -Force

Write-Host '4/4 Activando simulación (v9.9.9)...' -ForegroundColor Cyan
New-Item -ItemType Directory (Split-Path $json) -Force | Out-Null
@{ version = '9.9.9'; url = $zip; notas = 'Actualización de prueba interna.'; rutaApp = $test } |
    ConvertTo-Json | Set-Content $json -Encoding UTF8

$lock = $null
if ($ForzarError) {
    # Bloquea un archivo que el ZIP intentará sobrescribir: debe hacer rollback
    $lock = [IO.File]::Open((Join-Path $test 'NX-Swite.dll'), 'Open', 'Read', 'None')
    Write-Host 'MODO ERROR: NX-Swite.dll bloqueado para forzar el rollback.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Abriendo NX-SWITE de prueba. Pulsa "Actualizar ahora" en el aviso.' -ForegroundColor Green
Start-Process (Join-Path $test 'NX-Swite.exe')

Read-Host 'Cuando el updater termine, pulsa Enter para ver el resultado'
if ($lock) { $lock.Close() }

Write-Host ''
Write-Host '--- Resultado ---' -ForegroundColor Cyan
$marcador = Test-Path (Join-Path $test 'PRUEBA.txt')
$backups  = @(Get-ChildItem $test -Recurse -Filter '*.nxbak' -ErrorAction SilentlyContinue)
Write-Host ("PRUEBA.txt presente : {0}  (esperado: {1})" -f $marcador, (-not $ForzarError))
Write-Host ("Archivos *.nxbak    : {0}  (esperado: 0)" -f $backups.Count)
Write-Host "Log del updater     : $log"
Write-Host ''
Write-Host 'Para desactivar la simulación: .\scripts\ProbarActualizacion.ps1 -Limpiar' -ForegroundColor Yellow
