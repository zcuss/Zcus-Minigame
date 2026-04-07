@echo off
setlocal
set MSG=%*
if "%MSG%"=="" set MSG=update: auto sync
powershell -ExecutionPolicy Bypass -File "%~dp0push-update.ps1" -Message "%MSG%"
endlocal
