#requires -Version 5.1
$ErrorActionPreference='Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$main = Join-Path $root 'Codex-Doctor-V7.ps1'
if (-not (Test-Path $main)) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("缺少运行文件：$main`n请保持 EXE 与 V7 完整目录在一起。",'Codex Doctor V7.1') | Out-Null
    exit 1
}
Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',('"'+$main+'"'))
