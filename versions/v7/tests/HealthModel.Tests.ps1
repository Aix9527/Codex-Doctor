$ErrorActionPreference='Stop'
$module = Join-Path $PSScriptRoot '..\lib\HealthModel.psm1'
Import-Module $module -Force

function Assert-Equal($Actual,$Expected,$Name){
    if($Actual -ne $Expected){ throw "$Name expected [$Expected] but got [$Actual]" }
}

Assert-Equal (Get-CodexOverallHealth -DnsOk:$false -TlsOk:$false -ProxyOk:$false -EnvConflict:$false -EnvPresent:$false) 'Error' 'DNS failure health'
Assert-Equal (Get-CodexOverallHealth -DnsOk:$true -TlsOk:$false -ProxyOk:$false -EnvConflict:$false -EnvPresent:$false) 'Error' 'TLS failure health'
Assert-Equal (Get-CodexOverallHealth -DnsOk:$true -TlsOk:$true -ProxyOk:$false -EnvConflict:$false -EnvPresent:$true) 'Error' 'Proxy failure health'
Assert-Equal (Get-CodexOverallHealth -DnsOk:$true -TlsOk:$true -ProxyOk:$true -EnvConflict:$true -EnvPresent:$true) 'Warning' 'Environment conflict health'
Assert-Equal (Get-CodexOverallHealth -DnsOk:$true -TlsOk:$true -ProxyOk:$true -EnvConflict:$false -EnvPresent:$false) 'Warning' 'Missing env health'
Assert-Equal (Get-CodexOverallHealth -DnsOk:$true -TlsOk:$true -ProxyOk:$true -EnvConflict:$false -EnvPresent:$true) 'Healthy' 'Healthy state'

Write-Host 'HealthModel tests passed.' -ForegroundColor Green
