Set-StrictMode -Version Latest

function Get-CodexOverallHealth {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][bool]$DnsOk,
        [Parameter(Mandatory)][bool]$TlsOk,
        [Parameter(Mandatory)][bool]$ProxyOk,
        [Parameter(Mandatory)][bool]$EnvConflict,
        [Parameter(Mandatory)][bool]$EnvPresent
    )

    if (-not $DnsOk -or -not $TlsOk -or -not $ProxyOk) { return 'Error' }
    if ($EnvConflict -or -not $EnvPresent) { return 'Warning' }
    return 'Healthy'
}

Export-ModuleMember -Function Get-CodexOverallHealth
