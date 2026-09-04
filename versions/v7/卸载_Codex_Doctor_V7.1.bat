@echo off
chcp 65001 >nul
setlocal
set "DST=%LOCALAPPDATA%\Programs\CodexDoctorV7"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$d=[Environment]::GetFolderPath('Desktop');$lnk=Join-Path $d 'Codex Doctor V7.lnk';if(Test-Path $lnk){Remove-Item $lnk -Force}"
if exist "%DST%" rmdir /S /Q "%DST%"
echo Codex Doctor V7.1 已卸载。
echo 诊断日志与报告仍保留在 %%LOCALAPPDATA%%\CodexDoctorV7。
pause
