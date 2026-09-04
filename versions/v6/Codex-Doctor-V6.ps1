#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ProxyUrl='',
    [switch]$Json
)

$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'lib\Diagnosis.psm1') -Force

function Get-ConfiguredProxy {
    if($ProxyUrl){ return $ProxyUrl }
    foreach($name in @('HTTPS_PROXY','HTTP_PROXY','https_proxy','http_proxy')){
        $value=[Environment]::GetEnvironmentVariable($name,'User')
        if($value){ return $value }
    }
    $envFile=Join-Path $env:USERPROFILE '.codex\.env'
    if(Test-Path $envFile){
        foreach($line in Get-Content $envFile){
            if($line -match '^\s*(?:HTTPS_PROXY|HTTP_PROXY|https_proxy|http_proxy)\s*=\s*(.+?)\s*$'){
                return $matches[1]
            }
        }
    }
    try{
        $k=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
        if($k.ProxyEnable -and $k.ProxyServer){
            $m=[regex]::Match([string]$k.ProxyServer,'(?:127\.0\.0\.1|localhost):(\d+)')
            if($m.Success){ return "http://127.0.0.1:$($m.Groups[1].Value)" }
        }
    }catch{}
    return ''
}

$proxy=Get-ConfiguredProxy
$result=Invoke-CodexNetworkDiagnosis -ProxyUrl $proxy

if($Json){
    $result | ConvertTo-Json -Depth 8
    exit $(if($result.Class -eq 'HEALTHY'){0}else{2})
}

Write-Host '=== Codex Doctor V6 Connectivity Diagnosis ===' -ForegroundColor Cyan
Write-Host "Proxy: $(if($proxy){$proxy}else{'<none>'})"
Write-Host "Class: $($result.Class)" -ForegroundColor $(if($result.Class -eq 'HEALTHY'){'Green'}elseif($result.Class -in @('DNS','TLS','PROXY')){'Red'}else{'Yellow'})
Write-Host "DNS: $($result.Dns.Ok)"
Write-Host "TLS: $($result.Tls.Ok) $($result.Tls.Protocol)"
Write-Host "Proxy route: $($result.Proxy.Ok) status=$($result.Proxy.StatusCode)"
Write-Host "TUN detected: $($result.Tun.TunDetected)"
Write-Host "Git proxy conflict: $($result.Git.Conflict)"
Write-Host "npm proxy conflict: $($result.Npm.Conflict)"
Write-Host "Recommendation: $($result.Recommendation)" -ForegroundColor Yellow

if($result.Class -ne 'HEALTHY'){ exit 2 }
