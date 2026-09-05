#requires -Version 5.1
$ErrorActionPreference='Stop'
$root = [System.AppContext]::BaseDirectory
if ([string]::IsNullOrWhiteSpace($root)) {
    $exePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    $root = Split-Path -Parent $exePath
}
if ([string]::IsNullOrWhiteSpace($root)) {
    throw 'Unable to determine Codex Doctor runtime directory.'
}
$main = Join-Path $root 'Codex-Doctor-V7.ps1'
if (-not (Test-Path $main)) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Missing runtime file: $main`nKeep the EXE inside the complete V7 release folder.",'Codex Doctor V7.1.1') | Out-Null
    exit 1
}
Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$main+'"'))
