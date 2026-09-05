$ErrorActionPreference='Stop'
$root = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $root 'Codex-Doctor-V7-Launcher.ps1'
$entry = Join-Path $root 'Codex-Doctor-V7.ps1'

if(-not(Test-Path $launcher)){throw 'Launcher missing.'}
if(-not(Test-Path $entry)){throw 'V7 entrypoint missing.'}

$launcherText = Get-Content $launcher -Raw
if($launcherText -match '\$MyInvocation\.MyCommand\.Path'){
    throw 'Launcher still depends on $MyInvocation.MyCommand.Path, which can be empty after ps2exe compilation.'
}
if($launcherText -notmatch 'AppContext\]::BaseDirectory'){
    throw 'Launcher does not use AppContext.BaseDirectory for compiled EXE location.'
}

$bytes=[IO.File]::ReadAllBytes($entry)
if($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF){
    throw 'Codex-Doctor-V7.ps1 must be UTF-8 with BOM for reliable Windows PowerShell 5.1 parsing of Chinese UI strings.'
}

$tokens=$null;$errors=$null
[System.Management.Automation.Language.Parser]::ParseFile($entry,[ref]$tokens,[ref]$errors)|Out-Null
if($errors.Count -gt 0){throw ('V7 entrypoint parse errors: '+($errors.Message -join '; '))}

Write-Host 'V7.1.1 compatibility tests passed.' -ForegroundColor Green
