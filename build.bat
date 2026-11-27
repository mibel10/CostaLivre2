@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" -Target %1
)
if %ERRORLEVEL% neq 0 (
    echo.
    echo Falha no build (codigo %ERRORLEVEL%).
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Build finalizado com sucesso!
if "%~1"=="" pause
