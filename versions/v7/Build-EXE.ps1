#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'CodexDoctorV7.exe')
)
$ErrorActionPreference='Stop'
$requiredVersion='1.0.18'
$module=Get-Module -ListAvailable ps2exe | Where-Object Version -eq ([version]$requiredVersion) | Select-Object -First 1
if(-not $module){
    throw "ps2exe $requiredVersion was not found. Install it with: Install-Module ps2exe -RequiredVersion $requiredVersion -Scope CurrentUser"
}
Import-Module ps2exe -RequiredVersion $requiredVersion -Force
$src=Join-Path $PSScriptRoot 'Codex-Doctor-V7-Launcher.ps1'
Invoke-ps2exe -inputFile $src -outputFile $OutputPath -noConsole -STA -title 'Codex Doctor V7.1' -product 'Codex Doctor' -version '7.1.0.0'
if(-not(Test-Path $OutputPath)){throw 'EXE build failed: output file was not created.'}
Write-Host "Generated: $OutputPath" -ForegroundColor Green
