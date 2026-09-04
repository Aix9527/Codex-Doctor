@echo off
chcp 65001 >nul
setlocal
set "SRC=%~dp0"
set "DST=%LOCALAPPDATA%\Programs\CodexDoctorV7"
if not exist "%DST%" mkdir "%DST%"
xcopy "%SRC%*" "%DST%\" /E /I /Y >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ws=New-Object -ComObject WScript.Shell;$d=[Environment]::GetFolderPath('Desktop');$s=$ws.CreateShortcut((Join-Path $d 'Codex Doctor V7.lnk'));if(Test-Path '%DST%\CodexDoctorV7.exe'){$s.TargetPath='%DST%\CodexDoctorV7.exe'}else{$s.TargetPath='powershell.exe';$s.Arguments='-NoProfile -ExecutionPolicy Bypass -STA -File ""%DST%\Codex-Doctor-V7.ps1""'};$s.WorkingDirectory='%DST%';$s.Save()"
echo.
echo Codex Doctor V7.1 已安装到：%DST%
if exist "%DST%\CodexDoctorV7.exe" (
  start "" "%DST%\CodexDoctorV7.exe"
) else (
  start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%DST%\Codex-Doctor-V7.ps1"
)
pause
