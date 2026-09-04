@echo off
chcp 65001 >nul
title Codex Doctor V3
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Codex-Doctor-V3.ps1"
pause
