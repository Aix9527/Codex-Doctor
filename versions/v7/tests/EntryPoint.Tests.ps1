$ErrorActionPreference='Stop'
$root = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $root 'Codex-Doctor-V7.ps1'
if(-not(Test-Path $entry)){ throw 'V7 entrypoint is missing.' }

$required = @('Diagnosis.psm1','RepairPlan.psm1','HealthModel.psm1','RepairActions.psm1')
foreach($name in $required){
    $path=Join-Path $root ('lib\'+$name)
    if(-not(Test-Path $path)){throw "Missing module: $name"}
    Import-Module $path -Force
}

$tokens=$null;$errors=$null
[System.Management.Automation.Language.Parser]::ParseFile($entry,[ref]$tokens,[ref]$errors)|Out-Null
if($errors.Count -gt 0){ throw ('Entrypoint parse errors: '+($errors.Message -join '; ')) }

Write-Host 'EntryPoint tests passed.' -ForegroundColor Green
