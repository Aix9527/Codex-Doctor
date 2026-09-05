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
            $advisory += '请先修复 DNS 解析，再修改 Codex 代理设置。'
        }
        'TLS' {
            $advisory += '请检查 TLS/HTTPS 检查、系统时间和证书链。'
        }
        'PROXY' {
            if ($ProxyAvailable) {
                $actions += 'WRITE_CODEX_ENV'
                $confirm = $true
            } else {
                $advisory += '请先启动或修复兼容 HTTP 的本地代理，再写入 Codex 代理设置。'
            }
        }
        'ENV_CONFLICT' {
            if ($GitConflict) { $actions += 'CLEAR_GIT_PROXY' }
            if ($NpmConflict) { $actions += 'CLEAR_NPM_PROXY' }
            $confirm = $actions.Count -gt 0
        }
        'HEALTHY' {
            $advisory += '当前不需要自动修复。如果桌面端仍显示“正在重新连接”，请重启 Codex 后再次检测。'
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
