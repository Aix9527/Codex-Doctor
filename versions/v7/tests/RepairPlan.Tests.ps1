$ErrorActionPreference='Stop'
$module = Join-Path $PSScriptRoot '..\lib\RepairPlan.psm1'
Import-Module $module -Force

function Assert-Equal($Actual,$Expected,$Name){
    if($Actual -ne $Expected){ throw "$Name expected [$Expected] but got [$Actual]" }
}
function Assert-Contains($Items,$Expected,$Name){
    if(-not (@($Items) -contains $Expected)){ throw "$Name expected to contain [$Expected]" }
}

$dns = New-CodexRepairPlan -FailureClass DNS -GitConflict:$false -NpmConflict:$false -ProxyAvailable:$false
Assert-Equal $dns.Automatic $false 'DNS automatic'
Assert-Equal $dns.ConfirmRequired $false 'DNS confirm'
Assert-Equal @($dns.Actions).Count 0 'DNS actions count'

$tls = New-CodexRepairPlan -FailureClass TLS -GitConflict:$false -NpmConflict:$false -ProxyAvailable:$false
Assert-Equal $tls.Automatic $false 'TLS automatic'
Assert-Equal @($tls.Actions).Count 0 'TLS actions count'

$proxy = New-CodexRepairPlan -FailureClass PROXY -GitConflict:$false -NpmConflict:$false -ProxyAvailable:$true
Assert-Equal $proxy.ConfirmRequired $true 'PROXY confirm'
Assert-Contains $proxy.Actions 'WRITE_CODEX_ENV' 'PROXY actions'

$conflict = New-CodexRepairPlan -FailureClass ENV_CONFLICT -GitConflict:$true -NpmConflict:$true -ProxyAvailable:$true
Assert-Equal $conflict.ConfirmRequired $true 'ENV_CONFLICT confirm'
Assert-Contains $conflict.Actions 'CLEAR_GIT_PROXY' 'ENV conflict actions'
Assert-Contains $conflict.Actions 'CLEAR_NPM_PROXY' 'ENV conflict actions'

$healthy = New-CodexRepairPlan -FailureClass HEALTHY -GitConflict:$false -NpmConflict:$false -ProxyAvailable:$true
Assert-Equal @($healthy.Actions).Count 0 'HEALTHY actions count'
Assert-Equal $healthy.Automatic $false 'HEALTHY automatic'

Write-Host 'RepairPlan tests passed.' -ForegroundColor Green
