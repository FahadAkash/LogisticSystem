@echo off
setlocal enabledelayedexpansion
title Distributed Logistics Platform - Launcher
cd /d "%~dp0"

echo.
echo Launching Distributed Logistics Platform via PowerShell runner...
echo (Pass -Windowed to launch each service in its own terminal window)
echo (Pass -NoFrontend to launch backend services only)
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-all.ps1" %*

exit /b %ERRORLEVEL%

