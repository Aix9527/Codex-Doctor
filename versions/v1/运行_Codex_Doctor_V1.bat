@echo off
chcp 65001 >nul
title Codex Doctor V1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Codex-Doctor-V1.ps1"
pause
