$ErrorActionPreference = 'Stop'
$module = Join-Path $PSScriptRoot '..\lib\Diagnosis.psm1'
Import-Module $module -Force

function Assert-Eq($Actual,$Expected,$Name){
  if($Actual -ne $Expected){ throw "$Name failed: expected '$Expected' got '$Actual'" }
}

Assert-Eq (Get-CodexFailureClass -DnsOk:$false -TlsOk:$false -ProxyOk:$false -TunDetected:$false -GitConflict:$false -NpmConflict:$false) 'DNS' 'DNS classification'
Assert-Eq (Get-CodexFailureClass -DnsOk:$true -TlsOk:$false -ProxyOk:$false -TunDetected:$false -GitConflict:$false -NpmConflict:$false) 'TLS' 'TLS classification'
Assert-Eq (Get-CodexFailureClass -DnsOk:$true -TlsOk:$true -ProxyOk:$false -TunDetected:$false -GitConflict:$false -NpmConflict:$false) 'PROXY' 'Proxy classification'
Assert-Eq (Get-CodexFailureClass -DnsOk:$true -TlsOk:$true -ProxyOk:$true -TunDetected:$true -GitConflict:$true -NpmConflict:$false) 'ENV_CONFLICT' 'Git conflict classification'
Assert-Eq (Get-CodexFailureClass -DnsOk:$true -TlsOk:$true -ProxyOk:$true -TunDetected:$true -GitConflict:$false -NpmConflict:$true) 'ENV_CONFLICT' 'npm conflict classification'
Assert-Eq (Get-CodexFailureClass -DnsOk:$true -TlsOk:$true -ProxyOk:$true -TunDetected:$true -GitConflict:$false -NpmConflict:$false) 'HEALTHY' 'Healthy classification'

Write-Host 'Diagnosis.Tests.ps1 passed' -ForegroundColor Green
