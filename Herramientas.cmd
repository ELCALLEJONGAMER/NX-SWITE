@echo off
chcp 65001 >nul
title NX-SWITE - Herramientas de desarrollo
cd /d "%~dp0"

:menu
cls
echo ============================================
echo   NX-SWITE - Herramientas de desarrollo
echo ============================================
echo.
echo   1. Publicar beta (ZIP para GitHub Releases)
echo   2. Probar actualizacion (normal)
echo   3. Probar actualizacion (forzar error / rollback)
echo   4. Desactivar simulacion y limpiar temporales
echo   5. Abrir carpeta dist
echo   0. Salir
echo.
set /p op="Elige una opcion: "

if "%op%"=="1" pwsh -NoProfile -ExecutionPolicy Bypass -File "publish-beta.ps1" & goto fin
if "%op%"=="2" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" & goto fin
if "%op%"=="3" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" -ForzarError & goto fin
if "%op%"=="4" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" -Limpiar & goto fin
if "%op%"=="5" (if not exist dist mkdir dist) & start "" "dist" & goto menu
if "%op%"=="0" exit /b
goto menu

:fin
echo.
pause
goto menu
