$ErrorActionPreference='Stop'
$module = Join-Path $PSScriptRoot '..\lib\RepairActions.psm1'
Import-Module $module -Force

function Assert-Equal($Actual,$Expected,$Name){ if($Actual -ne $Expected){ throw "$Name expected [$Expected] got [$Actual]" } }
function Assert-True($Value,$Name){ if(-not $Value){ throw "$Name expected true" } }

$root = Join-Path ([IO.Path]::GetTempPath()) ('CodexDoctorV7-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $envFile = Join-Path $root '.env'
    Set-Content $envFile @('FOO=bar','HTTP_PROXY=http://old:1','NO_PROXY=old') -Encoding UTF8
    $result = Set-CodexProxyEnvFile -EnvFile $envFile -ProxyUrl 'http://127.0.0.1:7897'
    Assert-True (Test-Path $result.BackupPath) 'backup exists'
    $text = Get-Content $envFile -Raw
    Assert-True ($text -match 'FOO=bar') 'preserves unrelated line'
    Assert-True ($text -match 'HTTP_PROXY=http://127\.0\.0\.1:7897') 'writes HTTP proxy'
    Assert-True ($text -match 'HTTPS_PROXY=http://127\.0\.0\.1:7897') 'writes HTTPS proxy'
    Assert-True ($text -notmatch 'http://old:1') 'removes old proxy'

    $read = Get-CodexEnvProxy -EnvFile $envFile
    Assert-Equal $read.HttpProxy 'http://127.0.0.1:7897' 'read HTTP proxy'
    Assert-Equal $read.HttpsProxy 'http://127.0.0.1:7897' 'read HTTPS proxy'
} finally {
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'RepairActions tests passed.' -ForegroundColor Green
