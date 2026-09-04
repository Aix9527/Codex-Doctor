@echo off
chcp 65001 >nul
title Codex Doctor V2
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Codex-Doctor-V2.ps1"
