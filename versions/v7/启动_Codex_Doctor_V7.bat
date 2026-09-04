@echo off
chcp 65001 >nul
title Codex Doctor V7
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Codex-Doctor-V7.ps1"
if errorlevel 1 pause
