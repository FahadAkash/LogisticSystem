@echo off
setlocal enabledelayedexpansion
title Distributed Logistics Platform - Stopper
cd /d "%~dp0"

echo.
echo Stopping all running Distributed Logistics Platform services...
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop-all.ps1" %*

exit /b %ERRORLEVEL%

