#requires -Version 5.1
[CmdletBinding()]
param(
  [ValidateSet("Menu","Diagnose","AutoFix","FixProxy","Migrate","Restore","Doctor")]
  [string]$Mode="Menu",
  [string]$TargetRoot="D:\Codex",
  [switch]$NoPause
)

$ErrorActionPreference="Stop"
$ProgressPreference="SilentlyContinue"
$CodexDir = Join-Path $env:USERPROFILE ".codex"
$StateRoot = Join-Path $env:LOCALAPPDATA "CodexDoctorV3"
$StateFile = Join-Path $StateRoot "state.json"
$LogDir = Join-Path $StateRoot "logs"
New-Item -ItemType Directory -Force -Path $StateRoot,$LogDir | Out-Null
$LogFile = Join-Path $LogDir ("doctor-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")

function Log([string]$Text,[ConsoleColor]$Color=[ConsoleColor]::Gray){ $line="[$(Get-Date -Format HH:mm:ss)] $Text"; Add-Content -Path $LogFile -Value $line -Encoding UTF8; Write-Host $line -ForegroundColor $Color }
function Test-Port([int]$Port){ try{$c=New-Object Net.Sockets.TcpClient;$ar=$c.BeginConnect("127.0.0.1",$Port,$null,$null);$ok=$ar.AsyncWaitHandle.WaitOne(350);if($ok -and $c.Connected){$c.EndConnect($ar);$c.Close();return $true};$c.Close()}catch{};return $false }
function Get-ListenerProcess([int]$Port){ try{$x=Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop|Select-Object -First 1;if($x){$p=Get-Process -Id $x.OwningProcess -ErrorAction SilentlyContinue;if($p){return $p.ProcessName}}}catch{};return "" }
function Get-SystemProxy { try{$k=Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings";return [pscustomobject]@{Enabled=[bool]$k.ProxyEnable;Server=$k.ProxyServer;AutoConfigURL=$k.AutoConfigURL}}catch{return [pscustomobject]@{Enabled=$false;Server="";AutoConfigURL=""}} }

function Find-ClashConfigs {
  $roots=@((Join-Path $env:APPDATA "io.github.clash-verge-rev.clash-verge-rev"),(Join-Path $env:APPDATA "clash-verge"),(Join-Path $env:APPDATA "Clash Verge"),(Join-Path $env:APPDATA "mihomo-party"),(Join-Path $env:USERPROFILE ".config\clash"),(Join-Path $env:USERPROFILE ".config\mihomo")) | Where-Object { $_ -and (Test-Path $_) }
  $files=@(); foreach($r in $roots){ try{$files += Get-ChildItem $r -Recurse -File -ErrorAction SilentlyContinue | Where-Object {$_.Extension -in @(".yaml",".yml",".json")} | Select-Object -First 80}catch{} }; return $files
}
function Get-ProxyPortsFromConfig {
  $ports=New-Object System.Collections.Generic.List[object]
  foreach($f in Find-ClashConfigs){ try{$txt=Get-Content $f.FullName -Raw -ErrorAction Stop;foreach($key in @("mixed-port","port","socks-port")){ $m=[regex]::Matches($txt,"(?im)^\s*"+[regex]::Escape($key)+"\s*:\s*(\d+)");foreach($x in $m){$p=[int]$x.Groups[1].Value;$ports.Add([pscustomobject]@{Port=$p;Source="$key in $($f.FullName)"})}}}catch{} }; return $ports | Sort-Object Port -Unique
}
function Test-HttpProxy([int]$Port){ foreach($u in @("https://chatgpt.com","https://api.openai.com")){ try{$resp=Invoke-WebRequest -Uri $u -Proxy "http://127.0.0.1:$Port" -Method Head -TimeoutSec 6 -UseBasicParsing;if($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500){return $true}}catch{if($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -in 400..499){return $true}}};return $false }
function Get-ProxyCandidates {
  $out=New-Object System.Collections.Generic.List[object];$sys=Get-SystemProxy
  if($sys.Enabled -and $sys.Server){foreach($m in [regex]::Matches($sys.Server,'(?:127\.0\.0\.1|localhost):(\d+)')){$p=[int]$m.Groups[1].Value;$out.Add([pscustomobject]@{Port=$p;Source="Windows system proxy"})}}
  foreach($x in Get-ProxyPortsFromConfig){$out.Add($x)};foreach($p in @(7897,7890,7891,10809,10808,1080,20171,20170)){$out.Add([pscustomobject]@{Port=$p;Source="common port scan"})}
  $uniq=@{};foreach($x in $out){if(-not $uniq.ContainsKey($x.Port)){$uniq[$x.Port]=$x}}
  $result=@();foreach($x in $uniq.Values){if(Test-Port $x.Port){$result += [pscustomobject]@{Port=$x.Port;Source=$x.Source;Process=(Get-ListenerProcess $x.Port);HttpOk=(Test-HttpProxy $x.Port)}}};return $result | Sort-Object @{Expression="HttpOk";Descending=$true},Port
}
function Select-BestProxy {$c=@(Get-ProxyCandidates);if($c.Count -eq 0){return $null};$ok=@($c | Where-Object {$_.HttpOk});if($ok.Count -gt 0){return $ok[0]};return $c[0]}
function Backup-File([string]$Path){if(Test-Path $Path){$b="$Path.backup_$(Get-Date -Format yyyyMMdd_HHmmss)";Copy-Item $Path $b -Force;return $b};return $null}
function Write-CodexProxy {
  $p=Select-BestProxy;if(-not $p){throw "No active local proxy was found."};Log "Selected proxy 127.0.0.1:$($p.Port)  HTTP-test=$($p.HttpOk)  process=$($p.Process)" Green;if(-not $p.HttpOk){Log "Warning: port is listening but HTTP proxy test did not pass." Yellow}
  if(-not(Test-Path $CodexDir)){New-Item -ItemType Directory -Force -Path $CodexDir|Out-Null};$envfile=Join-Path $CodexDir ".env";$b=Backup-File $envfile;if($b){Log "Backed up .env => $b" DarkGray}
  $old=@();if(Test-Path $envfile){$old=Get-Content $envfile -ErrorAction SilentlyContinue};$filtered=$old|Where-Object{$_ -notmatch '^\s*(HTTP_PROXY|HTTPS_PROXY|http_proxy|https_proxy|ALL_PROXY|all_proxy|NO_PROXY|no_proxy)\s*='};$url="http://127.0.0.1:$($p.Port)";$lines=@();if($filtered){$lines+=$filtered;$lines+=""};$lines+=@("# Managed by Codex Doctor V3","HTTP_PROXY=$url","HTTPS_PROXY=$url","http_proxy=$url","https_proxy=$url","NO_PROXY=localhost,127.0.0.1,::1","no_proxy=localhost,127.0.0.1,::1");Set-Content $envfile $lines -Encoding UTF8
  foreach($n in @("HTTP_PROXY","HTTPS_PROXY","http_proxy","https_proxy")){[Environment]::SetEnvironmentVariable($n,$url,"User")};foreach($n in @("NO_PROXY","no_proxy")){[Environment]::SetEnvironmentVariable($n,"localhost,127.0.0.1,::1","User")};Log "Updated $envfile" Green;return $p
}
function Get-CodexProcesses {Get-Process -ErrorAction SilentlyContinue|Where-Object{$_.ProcessName -match 'Codex|ChatGPT' -or ($_.Path -and $_.Path -match 'Codex|ChatGPT')}}
function Stop-CodexProcesses {$p=@(Get-CodexProcesses);if($p.Count){Log "Closing Codex/ChatGPT processes before repair/migration..." Yellow;$p|Stop-Process -Force -ErrorAction SilentlyContinue;Start-Sleep -Milliseconds 800}}
function Diagnose {
  Log "=== Codex Doctor V3 ===" Cyan;Log "Codex data: $CodexDir";if(Test-Path $CodexDir){$i=Get-Item $CodexDir -Force;Log "Exists=True ReparsePoint=$([bool]($i.Attributes -band [IO.FileAttributes]::ReparsePoint))";if($i.Attributes -band [IO.FileAttributes]::ReparsePoint){Log "Target=$($i.Target -join ', ')"}}else{Log "Codex data directory does not exist yet." Yellow}
  $sys=Get-SystemProxy;Log "WindowsProxy Enabled=$($sys.Enabled) Server=$($sys.Server) PAC=$($sys.AutoConfigURL)";$cfg=@(Get-ProxyPortsFromConfig);if($cfg.Count){Log "Proxy ports discovered from Clash/Mihomo configs:" Cyan;foreach($x in $cfg){Log "  $($x.Port) <- $($x.Source)"}}else{Log "No Clash/Mihomo port found in known config paths." Yellow}
  $c=@(Get-ProxyCandidates);if($c.Count){Log "Listening proxy candidates:" Cyan;foreach($x in $c){Log "  $($x.Port) process=$($x.Process) HTTP-test=$($x.HttpOk) source=$($x.Source)"}}else{Log "No listening proxy candidate found." Red};$ef=Join-Path $CodexDir ".env";if(Test-Path $ef){Log ".env exists: $ef" Green;Get-Content $ef|Where-Object{$_ -match 'proxy'}|ForEach-Object{Log "  $_"}}else{Log ".env not found." Yellow};try{$cmd=Get-Command codex -ErrorAction Stop;Log "Codex CLI: $($cmd.Source)" Green}catch{Log "Codex CLI not found in PATH." Yellow};Log "Report: $LogFile" Cyan
}
function Run-Doctor {try{$null=Get-Command codex -ErrorAction Stop;Log "Running official codex doctor..." Yellow;& codex doctor}catch{Log "codex CLI not found; skipping official doctor." Yellow}}
function Save-State($o){$o|ConvertTo-Json -Depth 5|Set-Content $StateFile -Encoding UTF8};function Load-State {if(Test-Path $StateFile){Get-Content $StateFile -Raw|ConvertFrom-Json}else{$null}}
function Migrate([string]$Root){Stop-CodexProcesses;New-Item -ItemType Directory -Force -Path $Root|Out-Null;$target=Join-Path $Root ".codex";if(Test-Path $CodexDir){$i=Get-Item $CodexDir -Force;if($i.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "$CodexDir is already linked."}};if(Test-Path $target -and @(Get-ChildItem $target -Force -ErrorAction SilentlyContinue).Count -gt 0){throw "Target is not empty: $target"};New-Item -ItemType Directory -Force -Path $target|Out-Null;$backup=$null;if(Test-Path $CodexDir){Log "Copying .codex to $target" Yellow;& robocopy $CodexDir $target /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP | Out-Null;if($LASTEXITCODE -ge 8){throw "Robocopy failed: $LASTEXITCODE"};$backup="$CodexDir.pre-migrate-$(Get-Date -Format yyyyMMdd-HHmmss)";Move-Item $CodexDir $backup};cmd.exe /c "mklink /J `"$CodexDir`" `"$target`"" | Out-Null;if(-not(Test-Path $CodexDir)){if($backup -and(Test-Path $backup)){Move-Item $backup $CodexDir};throw "Junction creation failed."};Save-State ([ordered]@{source=$CodexDir;target=$target;backup=$backup;migrated_at=(Get-Date).ToString("o")});Log "Migration complete: $CodexDir => $target" Green}
function Restore {Stop-CodexProcesses;$s=Load-State;if(-not $s){throw "No migration state found."};if(Test-Path $s.source){$i=Get-Item $s.source -Force;if(-not($i.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Source is not a Junction; aborting."};cmd.exe /c "rmdir `"$($s.source)`"" | Out-Null};if($s.backup -and(Test-Path $s.backup)){Move-Item $s.backup $s.source}else{New-Item -ItemType Directory -Force -Path $s.source|Out-Null};if(Test-Path $s.target){& robocopy $s.target $s.source /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP | Out-Null};Remove-Item $StateFile -Force -ErrorAction SilentlyContinue;Log "Restore complete." Green}
function AutoFix {Diagnose;Stop-CodexProcesses;try{Write-CodexProxy|Out-Null}catch{Log $_.Exception.Message Red};Run-Doctor;Log "Repair complete. Reopen ChatGPT/Codex Desktop." Green}
function Menu {while($true){Clear-Host;Write-Host "==============================================" -ForegroundColor Cyan;Write-Host " Codex Doctor V3 - Smart Proxy Edition" -ForegroundColor Cyan;Write-Host "==============================================" -ForegroundColor Cyan;Write-Host "1. Diagnose";Write-Host "2. Smart proxy repair / Reconnecting";Write-Host "3. One-click AutoFix";Write-Host "4. Migrate ~/.codex to D/E drive";Write-Host "5. Restore ~/.codex to C:";Write-Host "6. Run official codex doctor";Write-Host "0. Exit";$c=Read-Host "Select";try{switch($c){"1"{Diagnose};"2"{Stop-CodexProcesses;Write-CodexProxy|Out-Null};"3"{AutoFix};"4"{$r=Read-Host "Target root [$TargetRoot]";if([string]::IsNullOrWhiteSpace($r)){$r=$TargetRoot};Migrate $r};"5"{Restore};"6"{Run-Doctor};"0"{return}}}catch{Log ("ERROR: "+$_.Exception.Message) Red};Read-Host "Press Enter"}}
switch($Mode){"Diagnose"{Diagnose};"FixProxy"{Stop-CodexProcesses;Write-CodexProxy|Out-Null};"AutoFix"{AutoFix};"Migrate"{Migrate $TargetRoot};"Restore"{Restore};"Doctor"{Run-Doctor};default{Menu}}
