#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Gui','Diagnose')][string]$Mode='Gui',
    [string]$ProxyUrl='',
    [switch]$Json
)

$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$Root=$PSScriptRoot
Import-Module (Join-Path $Root 'lib\Diagnosis.psm1') -Force
Import-Module (Join-Path $Root 'lib\RepairPlan.psm1') -Force
Import-Module (Join-Path $Root 'lib\HealthModel.psm1') -Force
Import-Module (Join-Path $Root 'lib\RepairActions.psm1') -Force

$CodexDir=Join-Path $env:USERPROFILE '.codex'
$EnvFile=Join-Path $CodexDir '.env'
$StateRoot=Join-Path $env:LOCALAPPDATA 'CodexDoctorV7'
$StateFile=Join-Path $StateRoot 'migration-state.json'
$LogDir=Join-Path $StateRoot 'logs'
$ReportDir=Join-Path $StateRoot 'reports'
New-Item -ItemType Directory -Force -Path $StateRoot,$LogDir,$ReportDir|Out-Null
$LogFile=Join-Path $LogDir ('v7-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log')

function Write-V7Log([string]$Text){
    $line='['+(Get-Date -Format 'HH:mm:ss')+'] '+$Text
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    if($script:LogBox){$script:LogBox.AppendText($line+[Environment]::NewLine);$script:LogBox.SelectionStart=$script:LogBox.Text.Length;$script:LogBox.ScrollToCaret();[Windows.Forms.Application]::DoEvents()}
}

function Test-LocalPort([int]$Port){
    try{$c=New-Object Net.Sockets.TcpClient;$a=$c.BeginConnect('127.0.0.1',$Port,$null,$null);$ok=$a.AsyncWaitHandle.WaitOne(300);if($ok -and $c.Connected){$c.EndConnect($a);$c.Close();return $true};$c.Close()}catch{}
    return $false
}

function Get-SystemProxyUrl {
    try{
        $k=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
        if($k.ProxyEnable -and $k.ProxyServer){
            $m=[regex]::Match([string]$k.ProxyServer,'(?:127\.0\.0\.1|localhost):(\d+)')
            if($m.Success){return 'http://127.0.0.1:'+$m.Groups[1].Value}
        }
    }catch{}
    return ''
}

function Get-ValidatedLocalProxy {
    $candidates=New-Object System.Collections.Generic.List[string]
    $sys=Get-SystemProxyUrl;if($sys){$candidates.Add($sys)}
    $existing=Get-CodexEnvProxy -EnvFile $EnvFile
    if($existing.HttpProxy){$candidates.Add($existing.HttpProxy)}
    foreach($p in @(7897,7890,7891,10809,10808,20171,20170)){if(Test-LocalPort $p){$candidates.Add('http://127.0.0.1:'+$p)}}
    foreach($u in @($candidates|Select-Object -Unique)){
        $r=Test-HttpProxyEndpoint -ProxyUrl $u
        if($r.Ok){return $u}
    }
    return ''
}

function Get-CodexProcesses { @(Get-Process -ErrorAction SilentlyContinue|Where-Object{$_.ProcessName -match 'Codex|ChatGPT'}) }
function Stop-CodexProcesses { Get-CodexProcesses|Stop-Process -Force -ErrorAction SilentlyContinue;Start-Sleep -Milliseconds 700 }
function Start-CodexDesktop {
    foreach($p in @((Join-Path $env:LOCALAPPDATA 'Programs\ChatGPT\ChatGPT.exe'),(Join-Path $env:LOCALAPPDATA 'Programs\Codex\Codex.exe'))){if(Test-Path $p){Start-Process $p;return}}
    try{Start-Process 'chatgpt:';return}catch{}
    try{Start-Process 'shell:AppsFolder'}catch{}
}
function Restart-CodexDesktop { Stop-CodexProcesses;Start-Sleep -Milliseconds 800;Start-CodexDesktop }

function Invoke-V7Diagnosis([string]$RequestedProxy=''){
    $effective=$RequestedProxy
    if(-not $effective){$envProxy=Get-CodexEnvProxy -EnvFile $EnvFile;if($envProxy.HttpProxy){$effective=$envProxy.HttpProxy}}
    if(-not $effective){$effective=Get-ValidatedLocalProxy}
    $d=Invoke-CodexNetworkDiagnosis -ProxyUrl $effective
    $envState=Get-CodexEnvProxy -EnvFile $EnvFile
    $health=Get-CodexOverallHealth -DnsOk:$d.Dns.Ok -TlsOk:$d.Tls.Ok -ProxyOk:$d.Proxy.Ok -EnvConflict:($d.Git.Conflict -or $d.Npm.Conflict) -EnvPresent:$envState.Exists
    $plan=New-CodexRepairPlan -FailureClass $d.Class -GitConflict:$d.Git.Conflict -NpmConflict:$d.Npm.Conflict -ProxyAvailable:([bool]$effective)
    [pscustomobject]@{Version='7.0';Health=$health;ProxyUrl=$effective;Diagnosis=$d;Env=$envState;Plan=$plan;CodexProcesses=@(Get-CodexProcesses).Count}
}

function Save-MigrationState($State){$State|ConvertTo-Json -Depth 5|Set-Content $StateFile -Encoding UTF8}
function Load-MigrationState {if(Test-Path $StateFile){Get-Content $StateFile -Raw|ConvertFrom-Json}else{$null}}

function Move-CodexData([string]$TargetRoot){
    Stop-CodexProcesses
    if(-not $TargetRoot){throw 'Migration target is empty.'}
    New-Item -ItemType Directory -Force -Path $TargetRoot|Out-Null
    $target=Join-Path $TargetRoot '.codex'
    if(Test-Path $CodexDir){$item=Get-Item $CodexDir -Force;if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw '.codex is already a reparse point.'}}
    if((Test-Path $target) -and @(Get-ChildItem $target -Force -ErrorAction SilentlyContinue).Count -gt 0){throw 'Migration target is not empty: '+$target}
    New-Item -ItemType Directory -Force -Path $target|Out-Null
    $backup=$null
    if(Test-Path $CodexDir){
        & robocopy $CodexDir $target /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP|Out-Null
        if($LASTEXITCODE -ge 8){throw 'Robocopy failed: '+$LASTEXITCODE}
        $backup=$CodexDir+'.pre-migrate-'+(Get-Date -Format 'yyyyMMdd-HHmmss');Move-Item $CodexDir $backup
    }
    cmd.exe /c "mklink /J `"$CodexDir`" `"$target`""|Out-Null
    if(-not(Test-Path $CodexDir)){if($backup -and(Test-Path $backup)){Move-Item $backup $CodexDir};throw 'Failed to create Junction.'}
    Save-MigrationState ([ordered]@{source=$CodexDir;target=$target;backup=$backup;created=(Get-Date).ToString('o')})
}

function Restore-CodexData {
    Stop-CodexProcesses;$s=Load-MigrationState;if(-not $s){throw 'No migration state found.'}
    if(Test-Path $s.source){$i=Get-Item $s.source -Force;if(-not($i.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Source is not a Junction; refusing destructive restore.'};cmd.exe /c "rmdir `"$($s.source)`""|Out-Null}
    if($s.backup -and(Test-Path $s.backup)){Move-Item $s.backup $s.source}else{New-Item -ItemType Directory -Force -Path $s.source|Out-Null}
    if(Test-Path $s.target){& robocopy $s.target $s.source /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XJ /NFL /NDL /NP|Out-Null;if($LASTEXITCODE -ge 8){throw 'Restore robocopy failed: '+$LASTEXITCODE}}
    Remove-Item $StateFile -Force -ErrorAction SilentlyContinue
}

function Export-V7Report($Result){
    $path=Join-Path $ReportDir ('CodexDoctorV7-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json')
    $Result|ConvertTo-Json -Depth 8|Set-Content $path -Encoding UTF8
    return $path
}

if($Mode -eq 'Diagnose'){
    $r=Invoke-V7Diagnosis -RequestedProxy $ProxyUrl
    if($Json){$r|ConvertTo-Json -Depth 8}else{$r|Format-List; $r.Diagnosis|Format-List}
    return
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form=New-Object Windows.Forms.Form
$form.Text='Codex Doctor V7 — Unified GUI'
$form.Size=New-Object Drawing.Size(1000,760)
$form.MinimumSize=New-Object Drawing.Size(920,700)
$form.StartPosition='CenterScreen'

$title=New-Object Windows.Forms.Label;$title.Text='Codex Doctor V7';$title.Font=New-Object Drawing.Font('Segoe UI',22,[Drawing.FontStyle]::Bold);$title.Location=New-Object Drawing.Point(25,18);$title.Size=New-Object Drawing.Size(500,45);$form.Controls.Add($title)
$subtitle=New-Object Windows.Forms.Label;$subtitle.Text='统一诊断 · 安全修复 · .codex 迁移/恢复';$subtitle.Location=New-Object Drawing.Point(30,65);$subtitle.Size=New-Object Drawing.Size(700,25);$form.Controls.Add($subtitle)

$script:HealthLabel=New-Object Windows.Forms.Label;$script:HealthLabel.Text='● 尚未诊断';$script:HealthLabel.Font=New-Object Drawing.Font('Segoe UI',14,[Drawing.FontStyle]::Bold);$script:HealthLabel.Location=New-Object Drawing.Point(30,100);$script:HealthLabel.Size=New-Object Drawing.Size(850,35);$form.Controls.Add($script:HealthLabel)
$script:DetailLabel=New-Object Windows.Forms.Label;$script:DetailLabel.Location=New-Object Drawing.Point(30,140);$script:DetailLabel.Size=New-Object Drawing.Size(900,58);$form.Controls.Add($script:DetailLabel)

$proxyLabel=New-Object Windows.Forms.Label;$proxyLabel.Text='代理：';$proxyLabel.Location=New-Object Drawing.Point(30,202);$proxyLabel.Size=New-Object Drawing.Size(60,25);$form.Controls.Add($proxyLabel)
$proxyBox=New-Object Windows.Forms.TextBox;$proxyBox.Location=New-Object Drawing.Point(90,199);$proxyBox.Size=New-Object Drawing.Size(300,25);$proxyBox.PlaceholderText='留空则自动检测';$form.Controls.Add($proxyBox)
$writeUserEnv=New-Object Windows.Forms.CheckBox;$writeUserEnv.Text='修复时同时写入 Windows 用户环境变量';$writeUserEnv.Checked=$false;$writeUserEnv.Location=New-Object Drawing.Point(410,199);$writeUserEnv.Size=New-Object Drawing.Size(330,28);$form.Controls.Add($writeUserEnv)

$targetLabel=New-Object Windows.Forms.Label;$targetLabel.Text='迁移目标：';$targetLabel.Location=New-Object Drawing.Point(30,237);$targetLabel.Size=New-Object Drawing.Size(80,25);$form.Controls.Add($targetLabel)
$targetBox=New-Object Windows.Forms.TextBox;$targetBox.Text='D:\Codex';$targetBox.Location=New-Object Drawing.Point(110,234);$targetBox.Size=New-Object Drawing.Size(280,25);$form.Controls.Add($targetBox)

function New-V7Button([string]$Text,[int]$X,[int]$Y,[int]$W=145){$b=New-Object Windows.Forms.Button;$b.Text=$Text;$b.Location=New-Object Drawing.Point($X,$Y);$b.Size=New-Object Drawing.Size($W,42);$form.Controls.Add($b);return $b}
$btnDiag=New-V7Button '一键诊断' 30 280 130
$btnRepair=New-V7Button '修复建议项' 175 280 145
$btnRestart=New-V7Button '重启 Codex' 335 280 130
$btnMigrate=New-V7Button '迁移 .codex' 480 280 130
$btnRestore=New-V7Button '恢复 .codex' 625 280 130
$btnReport=New-V7Button '导出报告' 770 280 130
$btnDoctor=New-V7Button '运行 codex doctor' 30 332 165
$btnClearGit=New-V7Button '清理 Git 代理' 210 332 145
$btnClearNpm=New-V7Button '清理 npm 代理' 370 332 145

$script:LogBox=New-Object Windows.Forms.TextBox;$script:LogBox.Multiline=$true;$script:LogBox.ScrollBars='Vertical';$script:LogBox.ReadOnly=$true;$script:LogBox.Font=New-Object Drawing.Font('Consolas',9);$script:LogBox.Location=New-Object Drawing.Point(30,395);$script:LogBox.Size=New-Object Drawing.Size(910,285);$script:LogBox.Anchor='Top,Bottom,Left,Right';$form.Controls.Add($script:LogBox)
$script:LastResult=$null

function Update-V7Ui($r){
    $script:LastResult=$r
    switch($r.Health){'Healthy'{$script:HealthLabel.ForeColor=[Drawing.Color]::ForestGreen;$script:HealthLabel.Text='● 健康'}'Warning'{$script:HealthLabel.ForeColor=[Drawing.Color]::DarkOrange;$script:HealthLabel.Text='● 需要检查'}default{$script:HealthLabel.ForeColor=[Drawing.Color]::Firebrick;$script:HealthLabel.Text='● 连接故障'}}
    $d=$r.Diagnosis
    $script:DetailLabel.Text="原因：$($d.Class)    DNS=$($d.Dns.Ok)    TLS=$($d.Tls.Ok)    Proxy=$($d.Proxy.Ok)    TUN=$($d.Tun.TunDetected)`nGitConflict=$($d.Git.Conflict)    npmConflict=$($d.Npm.Conflict)    .env=$($r.Env.Exists)    Proxy=$($r.ProxyUrl)"
    if($r.ProxyUrl -and -not $proxyBox.Text){$proxyBox.Text=$r.ProxyUrl}
    Write-V7Log ('Diagnosis: '+$d.Class+' | health='+$r.Health+' | proxy='+$r.ProxyUrl)
}

$btnDiag.Add_Click({try{Update-V7Ui (Invoke-V7Diagnosis -RequestedProxy $proxyBox.Text.Trim())}catch{Write-V7Log ('ERROR '+$_.Exception.Message);[Windows.Forms.MessageBox]::Show($_.Exception.Message,'诊断失败')}})
$btnRepair.Add_Click({
    try{
        $r=Invoke-V7Diagnosis -RequestedProxy $proxyBox.Text.Trim();Update-V7Ui $r
        if(@($r.Plan.Actions).Count -eq 0){[Windows.Forms.MessageBox]::Show(($r.Plan.Advisory -join "`n"),'当前无自动修复动作');return}
        $message="诊断：$($r.Diagnosis.Class)`n建议动作：$($r.Plan.Actions -join ', ')`n是否执行？"
        if([Windows.Forms.MessageBox]::Show($message,'确认修复',[Windows.Forms.MessageBoxButtons]::YesNo) -ne [Windows.Forms.DialogResult]::Yes){return}
        foreach($a in $r.Plan.Actions){
            switch($a){
                'WRITE_CODEX_ENV' { if(-not $r.ProxyUrl){throw '没有已验证代理。'};Set-CodexProxyEnvFile -EnvFile $EnvFile -ProxyUrl $r.ProxyUrl|Out-Null;if($writeUserEnv.Checked){Set-CodexUserProxyEnvironment -ProxyUrl $r.ProxyUrl -Confirm:$false};Write-V7Log 'Updated .codex/.env.' }
                'CLEAR_GIT_PROXY' { Clear-GitProxyConfig -Confirm:$false;Write-V7Log 'Cleared Git global proxy.' }
                'CLEAR_NPM_PROXY' { Clear-NpmProxyConfig -Confirm:$false;Write-V7Log 'Cleared npm proxy.' }
            }
        }
        Update-V7Ui (Invoke-V7Diagnosis -RequestedProxy $r.ProxyUrl)
    }catch{Write-V7Log ('ERROR '+$_.Exception.Message);[Windows.Forms.MessageBox]::Show($_.Exception.Message,'修复失败')}
})
$btnRestart.Add_Click({try{Restart-CodexDesktop;Write-V7Log 'Restart requested.'}catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'重启失败')}})
$btnMigrate.Add_Click({try{if([Windows.Forms.MessageBox]::Show('迁移 .codex 到 '+$targetBox.Text+' 并创建 Junction？','确认迁移',[Windows.Forms.MessageBoxButtons]::YesNo) -eq [Windows.Forms.DialogResult]::Yes){Move-CodexData $targetBox.Text.Trim();Write-V7Log 'Migration complete.'}}catch{Write-V7Log ('ERROR '+$_.Exception.Message);[Windows.Forms.MessageBox]::Show($_.Exception.Message,'迁移失败')}})
$btnRestore.Add_Click({try{if([Windows.Forms.MessageBox]::Show('解除 Junction 并恢复 .codex？','确认恢复',[Windows.Forms.MessageBoxButtons]::YesNo) -eq [Windows.Forms.DialogResult]::Yes){Restore-CodexData;Write-V7Log 'Restore complete.'}}catch{Write-V7Log ('ERROR '+$_.Exception.Message);[Windows.Forms.MessageBox]::Show($_.Exception.Message,'恢复失败')}})
$btnReport.Add_Click({try{$r=if($script:LastResult){$script:LastResult}else{Invoke-V7Diagnosis -RequestedProxy $proxyBox.Text.Trim()};$p=Export-V7Report $r;Write-V7Log ('Report: '+$p);Start-Process explorer.exe "/select,`"$p`""}catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'导出失败')}})
$btnDoctor.Add_Click({try{$null=Get-Command codex -ErrorAction Stop;$out=& codex doctor 2>&1|Out-String;Write-V7Log $out.Trim()}catch{Write-V7Log 'codex CLI not found or doctor failed.'}})
$btnClearGit.Add_Click({if([Windows.Forms.MessageBox]::Show('清理 Git 全局 http.proxy / https.proxy？','确认',[Windows.Forms.MessageBoxButtons]::YesNo) -eq [Windows.Forms.DialogResult]::Yes){Clear-GitProxyConfig -Confirm:$false;Write-V7Log 'Cleared Git proxy by explicit action.'}})
$btnClearNpm.Add_Click({if([Windows.Forms.MessageBox]::Show('清理 npm proxy / https-proxy？','确认',[Windows.Forms.MessageBoxButtons]::YesNo) -eq [Windows.Forms.DialogResult]::Yes){Clear-NpmProxyConfig -Confirm:$false;Write-V7Log 'Cleared npm proxy by explicit action.'}})
$form.Add_Shown({Write-V7Log 'Codex Doctor V7 started. Diagnosis is read-only until a repair action is explicitly confirmed.'})
[void]$form.ShowDialog()
