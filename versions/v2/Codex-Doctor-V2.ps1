#requires -Version 5.1
<#
Codex Doctor V2
Windows Codex migration + proxy diagnostics + rollback
Safe-by-default:
- Does NOT move the installed ChatGPT/Codex app package.
- Can move %USERPROFILE%\.codex to another drive and create a Junction.
- Detects common local proxy ports and writes ~/.codex/.env.
- Backs up existing .env and records migration state.
- Can restore .codex back to the user profile.
#>

[CmdletBinding()]
param(
    [ValidateSet("Menu","Diagnose","FixProxy","Migrate","Restore","Doctor")]
    [string]$Mode = "Menu",
    [string]$TargetRoot = "D:\Codex",
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$UserCodex = Join-Path $env:USERPROFILE ".codex"
$StateRoot = Join-Path $env:LOCALAPPDATA "CodexDoctorV2"
$StateFile = Join-Path $StateRoot "state.json"
$LogDir = Join-Path $StateRoot "logs"
New-Item -ItemType Directory -Force -Path $StateRoot,$LogDir | Out-Null
$LogFile = Join-Path $LogDir ("codex-doctor-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")

function Log([string]$Text, [ConsoleColor]$Color = [ConsoleColor]::Gray) {
    $stamp = Get-Date -Format "HH:mm:ss"
    $line = "[$stamp] $Text"
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    Write-Host $line -ForegroundColor $Color
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-Port([int]$Port) {
    try {
        $c = New-Object Net.Sockets.TcpClient
        $ar = $c.BeginConnect("127.0.0.1",$Port,$null,$null)
        $ok = $ar.AsyncWaitHandle.WaitOne(350)
        if ($ok -and $c.Connected) { $c.EndConnect($ar); $c.Close(); return $true }
        $c.Close()
    } catch {}
    return $false
}

function Get-ListenerProcess([int]$Port) {
    try {
        $x = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop | Select-Object -First 1
        if ($x) {
            $p = Get-Process -Id $x.OwningProcess -ErrorAction SilentlyContinue
            if ($p) { return $p.ProcessName }
        }
    } catch {}
    return ""
}

function Get-SystemProxy {
    try {
        $k = Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
        [pscustomobject]@{
            Enabled = [bool]$k.ProxyEnable
            Server  = $k.ProxyServer
            AutoConfigURL = $k.AutoConfigURL
        }
    } catch {
        [pscustomobject]@{ Enabled=$false; Server=""; AutoConfigURL="" }
    }
}

function Get-CandidateProxy {
    $ports = @(7897,7890,7891,10809,10808,1080,20171,20170)
    $sys = Get-SystemProxy
    if ($sys.Enabled -and $sys.Server) {
        $matches = [regex]::Matches($sys.Server, '(?:127\.0\.0\.1|localhost):(\d+)')
        foreach($m in $matches) {
            $p = [int]$m.Groups[1].Value
            if (Test-Port $p) {
                return [pscustomobject]@{Port=$p; Source="Windows system proxy"; Process=(Get-ListenerProcess $p)}
            }
        }
    }
    foreach($p in $ports) {
        if(Test-Port $p) {
            return [pscustomobject]@{Port=$p; Source="common local proxy port"; Process=(Get-ListenerProcess $p)}
        }
    }
    return $null
}

function Backup-File([string]$Path) {
    if(Test-Path $Path) {
        $b = "$Path.backup_" + (Get-Date -Format "yyyyMMdd_HHmmss")
        Copy-Item $Path $b -Force
        return $b
    }
    return $null
}

function Set-CodexProxy {
    Log "Scanning local proxy..." Yellow
    $proxy = Get-CandidateProxy
    if(-not $proxy) { throw "No active local proxy found. Start Clash Verge/Mihomo/v2rayN/sing-box, or add its port to the script." }
    $url = "http://127.0.0.1:$($proxy.Port)"
    Log "Detected $url  source=$($proxy.Source) process=$($proxy.Process)" Green
    if(-not (Test-Path $UserCodex)) { New-Item -ItemType Directory -Force -Path $UserCodex | Out-Null }
    $envFile = Join-Path $UserCodex ".env"
    $backup = Backup-File $envFile
    if($backup) { Log "Backed up .env => $backup" DarkGray }
    $old = @()
    if(Test-Path $envFile) { $old = Get-Content $envFile -ErrorAction SilentlyContinue }
    $filtered = $old | Where-Object { $_ -notmatch '^\s*(HTTP_PROXY|HTTPS_PROXY|http_proxy|https_proxy|ALL_PROXY|all_proxy|NO_PROXY|no_proxy)\s*=' }
    $lines = @()
    if($filtered) { $lines += $filtered; $lines += "" }
    $lines += @("# Managed by Codex Doctor V2","HTTP_PROXY=$url","HTTPS_PROXY=$url","http_proxy=$url","https_proxy=$url","NO_PROXY=localhost,127.0.0.1,::1","no_proxy=localhost,127.0.0.1,::1")
    Set-Content -Path $envFile -Value $lines -Encoding UTF8
    foreach($name in @("HTTP_PROXY","HTTPS_PROXY","http_proxy","https_proxy")) { [Environment]::SetEnvironmentVariable($name,$url,"User") }
    foreach($name in @("NO_PROXY","no_proxy")) { [Environment]::SetEnvironmentVariable($name,"localhost,127.0.0.1,::1","User") }
    Log "Updated $envFile" Green
    Log "Proxy environment written for current user." Green
    return $url
}

function Get-CodexProcesses {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(Codex|ChatGPT|codex|chatgpt)$' -or ($_.Path -and $_.Path -match 'Codex|ChatGPT') }
}

function Stop-CodexProcessesInteractive {
    $p = @(Get-CodexProcesses)
    if($p.Count -eq 0) { return }
    Log ("Codex/ChatGPT related processes detected: " + (($p | Select-Object -Expand ProcessName -Unique) -join ", ")) Yellow
    Log "Closing them before migration..." Yellow
    $p | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

function Save-State($obj) { $obj | ConvertTo-Json -Depth 8 | Set-Content -Path $StateFile -Encoding UTF8 }
function Load-State { if(Test-Path $StateFile) { return Get-Content $StateFile -Raw | ConvertFrom-Json }; return $null }

function Migrate-CodexData([string]$Root) {
    Stop-CodexProcessesInteractive
    if(-not (Test-Path $Root)) { New-Item -ItemType Directory -Force -Path $Root | Out-Null }
    $target = Join-Path $Root ".codex"
    if(Test-Path $UserCodex) {
        $item = Get-Item $UserCodex -Force
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { Log "$UserCodex is already a Junction/Symlink." Yellow; return }
    }
    if(Test-Path $target) {
        $nonempty = @(Get-ChildItem $target -Force -ErrorAction SilentlyContinue).Count -gt 0
        if($nonempty) { throw "Target already exists and is not empty: $target" }
    } else { New-Item -ItemType Directory -Force -Path $target | Out-Null }
    if(Test-Path $UserCodex) {
        Log "Copying $UserCodex => $target" Yellow
        & robocopy $UserCodex $target /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP | Out-Null
        if($LASTEXITCODE -ge 8) { throw "Robocopy failed with exit code $LASTEXITCODE" }
        $backup = "$UserCodex.pre-migrate-" + (Get-Date -Format "yyyyMMdd-HHmmss")
        Move-Item $UserCodex $backup
    } else { $backup = $null }
    Log "Creating Junction: $UserCodex => $target" Yellow
    $cmd = "mklink /J `"$UserCodex`" `"$target`""
    cmd.exe /c $cmd | Out-Null
    if(-not (Test-Path $UserCodex)) { if($backup -and (Test-Path $backup)) { Move-Item $backup $UserCodex }; throw "Failed to create Junction." }
    $state = [ordered]@{ version = 2; migrated_at = (Get-Date).ToString("o"); source = $UserCodex; target = $target; backup = $backup }
    Save-State $state
    Log "Migration complete: $UserCodex => $target" Green
    if($backup) { Log "Original backup retained at: $backup" DarkGray }
}

function Restore-CodexData {
    Stop-CodexProcessesInteractive
    $state = Load-State
    if(-not $state) { throw "No Codex Doctor migration state found." }
    $source = [string]$state.source; $target = [string]$state.target; $backup = [string]$state.backup
    if(Test-Path $source) {
        $item = Get-Item $source -Force
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { cmd.exe /c "rmdir `"$source`"" | Out-Null } else { throw "$source is not a Junction. Restore aborted for safety." }
    }
    if($backup -and (Test-Path $backup)) {
        Log "Restoring original directory from $backup" Yellow
        Move-Item $backup $source
        if(Test-Path $target) { Log "Merging any newer target files back into restored .codex..." Yellow; & robocopy $target $source /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP | Out-Null }
    } else {
        Log "No original backup found; copying target back to user profile..." Yellow
        New-Item -ItemType Directory -Force -Path $source | Out-Null
        & robocopy $target $source /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP | Out-Null
        if($LASTEXITCODE -ge 8) { throw "Robocopy restore failed with exit code $LASTEXITCODE" }
    }
    Remove-Item $StateFile -Force -ErrorAction SilentlyContinue
    Log "Restore complete: $source is a normal directory again." Green
}

function Diagnose {
    Log "=== Codex Doctor V2 diagnostics ===" Cyan
    Log "User profile: $env:USERPROFILE"; Log "Codex data: $UserCodex"; Log ("PowerShell: " + $PSVersionTable.PSVersion); Log ("Admin: " + (Test-Admin))
    if(Test-Path $UserCodex) { $i = Get-Item $UserCodex -Force; $isRp = [bool]($i.Attributes -band [IO.FileAttributes]::ReparsePoint); Log ("Codex data exists. ReparsePoint=" + $isRp); if($isRp) { Log ("Link target: " + ($i.Target -join ", ")) } } else { Log "Codex data directory does not exist yet." Yellow }
    $proxy = Get-CandidateProxy
    if($proxy) { Log "Active local proxy: http://127.0.0.1:$($proxy.Port) process=$($proxy.Process)" Green } else { Log "No common local proxy found." Yellow }
    $sys = Get-SystemProxy; Log "Windows proxy enabled=$($sys.Enabled) server=$($sys.Server) PAC=$($sys.AutoConfigURL)"
    $envFile = Join-Path $UserCodex ".env"
    if(Test-Path $envFile) { Log ".env exists: $envFile" Green; Get-Content $envFile | Where-Object {$_ -match 'proxy'} | ForEach-Object { Log ("  " + $_) } } else { Log ".env not found." Yellow }
    $procs = @(Get-CodexProcesses); Log ("Codex/ChatGPT processes: " + $procs.Count)
    try { $cmd = Get-Command codex -ErrorAction Stop; Log "Codex CLI: $($cmd.Source)" Green } catch { Log "Codex CLI command not found in PATH." Yellow }
    Log "Log file: $LogFile" Cyan
}

function Run-CodexDoctor { try { $null = Get-Command codex -ErrorAction Stop; Log "Running official: codex doctor" Yellow; & codex doctor } catch { Log "codex CLI is not available in PATH; skipping official codex doctor." Yellow } }
function Full-Fix { Diagnose; try { Set-CodexProxy | Out-Null } catch { Log $_.Exception.Message Red }; Run-CodexDoctor; Log "Full fix finished. Fully exit and reopen ChatGPT/Codex Desktop." Green }

function Show-Menu {
    while($true) {
        Clear-Host
        Write-Host "==================================================" -ForegroundColor Cyan
        Write-Host " Codex Doctor V2 - Migration + Proxy Repair" -ForegroundColor Cyan
        Write-Host "==================================================" -ForegroundColor Cyan
        Write-Host "1. Diagnose"; Write-Host "2. Fix Codex proxy / Reconnecting"; Write-Host "3. Migrate ~/.codex to another drive"; Write-Host "4. Restore ~/.codex to C: profile"; Write-Host "5. Run official codex doctor"; Write-Host "6. Diagnose + proxy fix + codex doctor"; Write-Host "0. Exit"; Write-Host ""
        $c = Read-Host "Select"
        try {
            switch($c) { "1" { Diagnose }; "2" { Set-CodexProxy | Out-Null }; "3" { $r = Read-Host "Target root [$TargetRoot]"; if([string]::IsNullOrWhiteSpace($r)) { $r = $TargetRoot }; Migrate-CodexData $r }; "4" { Restore-CodexData }; "5" { Run-CodexDoctor }; "6" { Full-Fix }; "0" { return }; default { Write-Host "Invalid choice." -ForegroundColor Red } }
        } catch { Log ("ERROR: " + $_.Exception.Message) Red }
        Write-Host ""; Read-Host "Press Enter to continue"
    }
}

switch($Mode) { "Diagnose" { Diagnose }; "FixProxy" { Set-CodexProxy | Out-Null }; "Migrate" { Migrate-CodexData $TargetRoot }; "Restore" { Restore-CodexData }; "Doctor" { Run-CodexDoctor }; default { Show-Menu } }
if(-not $NoPause -and $Mode -ne "Menu") { Read-Host "Press Enter to exit" }
