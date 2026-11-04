@echo off
chcp 65001 >nul
title NX-SWITE - Herramientas de desarrollo
cd /d "%~dp0"

:menu
cls
echo ============================================
echo   NX-SWITE - Herramientas de desarrollo
echo ============================================
if exist "%APPDATA%\NX-Swite\actualizacion-prueba.json" (echo   Simulacion de actualizacion: ACTIVADA) else (echo   Simulacion de actualizacion: desactivada)
echo.
echo   1. Publicar beta (ZIP para GitHub Releases)
echo   2. Probar actualizacion (activa simulacion v9.9.9)
echo   3. Probar actualizacion (forzar error / rollback)
echo   4. DESACTIVAR simulacion y limpiar temporales
echo   5. Abrir carpeta dist
echo   6. Publicar NX-STUDIO (ZIP independiente)
echo   7. Publicar NX-STUDIO portable (USB)
echo   0. Salir
echo.
set /p op="Elige una opcion: "

if "%op%"=="1" pwsh -NoProfile -ExecutionPolicy Bypass -File "publish-beta.ps1" & goto fin
if "%op%"=="2" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" & goto fin
if "%op%"=="3" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" -ForzarError & goto fin
if "%op%"=="4" pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts\ProbarActualizacion.ps1" -Limpiar & goto fin
if "%op%"=="5" (if not exist dist mkdir dist) & start "" "dist" & goto menu
if "%op%"=="6" goto studio
"scripts\PublicarStudioUSB.ps1" -Clonar & goto fin
if "%op%"=="0" exit /b
goto menu

:studio
set "ver="
set /p ver="Version de NX-STUDIO (Enter = fecha de hoy): "
if "%ver%"=="" (pwsh -NoProfile -ExecutionPolicy Bypass -File "publish-beta.ps1" -App Studio) else (pwsh -NoProfile -ExecutionPolicy Bypass -File "publish-beta.ps1" -App Studio -Version "%ver%")
goto fin

:fin
echo.
pause
goto menu
