@echo off
chcp 65001 >nul
title Codex Doctor V6
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Codex-Doctor-V6.ps1"
pause
