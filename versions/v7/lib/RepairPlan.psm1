Set-StrictMode -Version Latest

function New-CodexRepairPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('DNS','TLS','PROXY','ENV_CONFLICT','HEALTHY')][string]$FailureClass,
        [Parameter(Mandatory)][bool]$GitConflict,
        [Parameter(Mandatory)][bool]$NpmConflict,
        [Parameter(Mandatory)][bool]$ProxyAvailable
    )

    $actions = @()
    $advisory = @()
    $confirm = $false

    switch ($FailureClass) {
        'DNS' {
            $advisory += 'Fix DNS resolution before changing Codex proxy settings.'
        }
        'TLS' {
            $advisory += 'Check TLS interception, HTTPS inspection, system clock, and certificate chain.'
        }
        'PROXY' {
            if ($ProxyAvailable) {
                $actions += 'WRITE_CODEX_ENV'
                $confirm = $true
            } else {
                $advisory += 'Start or repair a local HTTP-compatible proxy before writing Codex proxy settings.'
            }
        }
        'ENV_CONFLICT' {
            if ($GitConflict) { $actions += 'CLEAR_GIT_PROXY' }
            if ($NpmConflict) { $actions += 'CLEAR_NPM_PROXY' }
            $confirm = $actions.Count -gt 0
        }
        'HEALTHY' {
            $advisory += 'No repair is required. Restart or retest if the desktop client still shows Reconnecting.'
        }
    }

    [pscustomobject]@{
        FailureClass=$FailureClass
        Automatic=$false
        ConfirmRequired=$confirm
        Actions=@($actions)
        Advisory=@($advisory)
    }
}

Export-ModuleMember -Function New-CodexRepairPlan
