Set-StrictMode -Version Latest

function Get-CodexEnvProxy {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$EnvFile)

    $http=''; $https=''
    if(Test-Path $EnvFile){
        foreach($line in Get-Content $EnvFile -ErrorAction SilentlyContinue){
            if($line -match '^\s*HTTP_PROXY\s*=\s*(.+)\s*$'){ $http=$Matches[1].Trim() }
            elseif($line -match '^\s*HTTPS_PROXY\s*=\s*(.+)\s*$'){ $https=$Matches[1].Trim() }
        }
    }
    [pscustomobject]@{ HttpProxy=$http; HttpsProxy=$https; Exists=(Test-Path $EnvFile) }
}

function Set-CodexProxyEnvFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$EnvFile,
        [Parameter(Mandatory)][string]$ProxyUrl
    )

    $dir=Split-Path -Parent $EnvFile
    if($dir -and -not(Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $backup=$null
    $old=@()
    if(Test-Path $EnvFile){
        $backup="$EnvFile.backup_$(Get-Date -Format yyyyMMdd_HHmmssfff)"
        Copy-Item $EnvFile $backup -Force
        $old=@(Get-Content $EnvFile -ErrorAction SilentlyContinue)
    }
    $filtered=@($old | Where-Object { $_ -notmatch '^\s*(HTTP_PROXY|HTTPS_PROXY|http_proxy|https_proxy|ALL_PROXY|all_proxy|NO_PROXY|no_proxy)\s*=' })
    $lines=@()
    if($filtered.Count){ $lines += $filtered; $lines += '' }
    $lines += @(
        '# 由 Codex Doctor V7 管理',
        "HTTP_PROXY=$ProxyUrl",
        "HTTPS_PROXY=$ProxyUrl",
        "http_proxy=$ProxyUrl",
        "https_proxy=$ProxyUrl",
        'NO_PROXY=localhost,127.0.0.1,::1',
        'no_proxy=localhost,127.0.0.1,::1'
    )
    Set-Content -Path $EnvFile -Value $lines -Encoding UTF8
    [pscustomobject]@{ EnvFile=$EnvFile; BackupPath=$backup; ProxyUrl=$ProxyUrl }
}

function Clear-GitProxyConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if($PSCmdlet.ShouldProcess('Git 全局代理设置','清理')){
        try { & git config --global --unset-all http.proxy 2>$null | Out-Null } catch {}
        try { & git config --global --unset-all https.proxy 2>$null | Out-Null } catch {}
    }
}

function Clear-NpmProxyConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if($PSCmdlet.ShouldProcess('npm 全局代理设置','清理')){
        try { & npm config delete proxy 2>$null | Out-Null } catch {}
        try { & npm config delete https-proxy 2>$null | Out-Null } catch {}
    }
}

function Set-CodexUserProxyEnvironment {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$ProxyUrl)
    if($PSCmdlet.ShouldProcess('Windows 用户环境变量','写入 Codex 兼容代理变量')){
        foreach($n in @('HTTP_PROXY','HTTPS_PROXY','http_proxy','https_proxy')){
            [Environment]::SetEnvironmentVariable($n,$ProxyUrl,'User')
        }
        foreach($n in @('NO_PROXY','no_proxy')){
            [Environment]::SetEnvironmentVariable($n,'localhost,127.0.0.1,::1','User')
        }
    }
}

Export-ModuleMember -Function Get-CodexEnvProxy,Set-CodexProxyEnvFile,Clear-GitProxyConfig,Clear-NpmProxyConfig,Set-CodexUserProxyEnvironment
