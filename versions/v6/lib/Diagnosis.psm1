Set-StrictMode -Version Latest

function Get-CodexFailureClass {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][bool]$DnsOk,
        [Parameter(Mandatory)][bool]$TlsOk,
        [Parameter(Mandatory)][bool]$ProxyOk,
        [Parameter(Mandatory)][bool]$TunDetected,
        [Parameter(Mandatory)][bool]$GitConflict,
        [Parameter(Mandatory)][bool]$NpmConflict
    )

    if (-not $DnsOk) { return 'DNS' }
    if (-not $TlsOk) { return 'TLS' }
    if (-not $ProxyOk) { return 'PROXY' }
    if ($GitConflict -or $NpmConflict) { return 'ENV_CONFLICT' }
    return 'HEALTHY'
}

function Test-CodexDns {
    [CmdletBinding()]
    param([string[]]$Hosts = @('chatgpt.com','api.openai.com'))

    $results = foreach ($hostName in $Hosts) {
        $ok = $false
        $addresses = @()
        $errorText = $null
        try {
            $addresses = @([System.Net.Dns]::GetHostAddresses($hostName) | ForEach-Object IPAddressToString)
            $ok = $addresses.Count -gt 0
        } catch {
            $errorText = $_.Exception.Message
        }
        [pscustomobject]@{ Host=$hostName; Ok=$ok; Addresses=$addresses; Error=$errorText }
    }
    [pscustomobject]@{ Ok=(@($results | Where-Object {-not $_.Ok}).Count -eq 0); Results=@($results) }
}

function Test-CodexTls {
    [CmdletBinding()]
    param([string]$HostName='chatgpt.com',[int]$Port=443,[int]$TimeoutMs=5000)

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect($HostName,$Port,$null,$null)
        if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs)) { throw 'TCP connect timeout' }
        $client.EndConnect($iar)
        $ssl = New-Object System.Net.Security.SslStream($client.GetStream(),$false,({$true}))
        try {
            $ssl.AuthenticateAsClient($HostName)
            return [pscustomobject]@{ Ok=$true; Protocol=$ssl.SslProtocol.ToString(); Error=$null }
        } finally { $ssl.Dispose() }
    } catch {
        return [pscustomobject]@{ Ok=$false; Protocol=$null; Error=$_.Exception.Message }
    } finally { $client.Dispose() }
}

function Test-HttpProxyEndpoint {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProxyUrl,[string]$Uri='https://chatgpt.com')
    try {
        $r = Invoke-WebRequest -Uri $Uri -Proxy $ProxyUrl -Method Head -UseBasicParsing -TimeoutSec 8
        return [pscustomobject]@{ Ok=($r.StatusCode -ge 200 -and $r.StatusCode -lt 500); StatusCode=[int]$r.StatusCode; Error=$null }
    } catch {
        $code = $null
        try { if ($_.Exception.Response) { $code=[int]$_.Exception.Response.StatusCode } } catch {}
        if ($code -ge 400 -and $code -lt 500) {
            return [pscustomobject]@{ Ok=$true; StatusCode=$code; Error=$null }
        }
        return [pscustomobject]@{ Ok=$false; StatusCode=$code; Error=$_.Exception.Message }
    }
}

function Get-ClashTunState {
    [CmdletBinding()]
    param()
    $names = @('clash-verge','clash-verge-service','mihomo','clash','sing-box')
    $proc = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.ProcessName })
    $adapters = @()
    try {
        $adapters = @(Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
            $_.Status -eq 'Up' -and ($_.InterfaceDescription -match 'TUN|Wintun|Mihomo|Clash|sing-box' -or $_.Name -match 'TUN|Mihomo|Clash')
        })
    } catch {}
    [pscustomobject]@{
        ProcessDetected=($proc.Count -gt 0)
        AdapterDetected=($adapters.Count -gt 0)
        TunDetected=($adapters.Count -gt 0)
        Processes=@($proc | Select-Object -ExpandProperty ProcessName -Unique)
        Adapters=@($adapters | Select-Object -ExpandProperty Name)
    }
}

function Get-GitProxyState {
    [CmdletBinding()]
    param([string]$ExpectedProxy='')
    $http=''; $https=''
    try { $http = (& git config --global --get http.proxy 2>$null | Select-Object -First 1) } catch {}
    try { $https = (& git config --global --get https.proxy 2>$null | Select-Object -First 1) } catch {}
    $vals=@($http,$https)|Where-Object{$_}
    $conflict=$false
    if($vals.Count -gt 0 -and $ExpectedProxy){ $conflict = @($vals | Where-Object { $_ -ne $ExpectedProxy }).Count -gt 0 }
    [pscustomobject]@{ Http=$http; Https=$https; Conflict=$conflict }
}

function Get-NpmProxyState {
    [CmdletBinding()]
    param([string]$ExpectedProxy='')
    $proxy=''; $httpsProxy=''
    try {
        $null=Get-Command npm -ErrorAction Stop
        $proxy=(& npm config get proxy 2>$null | Select-Object -First 1)
        $httpsProxy=(& npm config get https-proxy 2>$null | Select-Object -First 1)
        if($proxy -eq 'null'){$proxy=''}
        if($httpsProxy -eq 'null'){$httpsProxy=''}
    } catch {}
    $vals=@($proxy,$httpsProxy)|Where-Object{$_}
    $conflict=$false
    if($vals.Count -gt 0 -and $ExpectedProxy){ $conflict = @($vals | Where-Object { $_ -ne $ExpectedProxy }).Count -gt 0 }
    [pscustomobject]@{ Proxy=$proxy; HttpsProxy=$httpsProxy; Conflict=$conflict }
}

function Invoke-CodexNetworkDiagnosis {
    [CmdletBinding()]
    param([string]$ProxyUrl='')

    $dns=Test-CodexDns
    $tls=if($dns.Ok){Test-CodexTls}else{[pscustomobject]@{Ok=$false;Protocol=$null;Error='Skipped because DNS failed'}}
    $proxy=if($ProxyUrl){Test-HttpProxyEndpoint -ProxyUrl $ProxyUrl}else{[pscustomobject]@{Ok=$false;StatusCode=$null;Error='No proxy configured'}}
    $tun=Get-ClashTunState
    $git=Get-GitProxyState -ExpectedProxy $ProxyUrl
    $npm=Get-NpmProxyState -ExpectedProxy $ProxyUrl
    $class=Get-CodexFailureClass -DnsOk:$dns.Ok -TlsOk:$tls.Ok -ProxyOk:$proxy.Ok -TunDetected:$tun.TunDetected -GitConflict:$git.Conflict -NpmConflict:$npm.Conflict

    $recommendation = switch($class){
        'DNS' {'Fix DNS resolution before changing Codex proxy settings.'}
        'TLS' {'Check TLS interception, antivirus HTTPS inspection, clock, and certificate chain.'}
        'PROXY' {'Local proxy is missing, stale, or cannot reach ChatGPT/OpenAI.'}
        'ENV_CONFLICT' {'Align or clear stale Git/npm proxy settings that disagree with the Codex proxy.'}
        default {'Connectivity checks passed.'}
    }

    [pscustomobject]@{
        Class=$class
        Dns=$dns
        Tls=$tls
        Proxy=$proxy
        Tun=$tun
        Git=$git
        Npm=$npm
        Recommendation=$recommendation
    }
}

Export-ModuleMember -Function Get-CodexFailureClass,Test-CodexDns,Test-CodexTls,Test-HttpProxyEndpoint,Get-ClashTunState,Get-GitProxyState,Get-NpmProxyState,Invoke-CodexNetworkDiagnosis
