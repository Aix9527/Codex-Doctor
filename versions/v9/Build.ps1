param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $taskOutput) -and (Get-ChildItem -LiteralPath $taskOutput -Force | Select-Object -First 1)) { throw '请提供空发布目录，避免混入旧构建文件。' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw '行为测试失败。' }
dotnet build (Join-Path $PSScriptRoot 'ui-tests/UiTests.csproj') -c Release -m:1
if ($LASTEXITCODE -ne 0) { throw 'UI 测试夹具构建失败。' }
$taskUiReport = Join-Path ([IO.Path]::GetTempPath()) ('CodexDoctorV9-ui-' + [guid]::NewGuid().ToString('N') + '.txt')
$taskUiExe = Join-Path $PSScriptRoot 'ui-tests/bin/Release/net8.0-windows/Codex.exe'
$taskUiProcess = Start-Process -FilePath $taskUiExe -ArgumentList ('"' + $taskUiReport + '"') -WindowStyle Hidden -PassThru
if (-not $taskUiProcess.WaitForExit(60000)) { $taskUiProcess.Kill(); throw 'UI 测试夹具超时。' }
if (Test-Path -LiteralPath $taskUiReport) { Get-Content -LiteralPath $taskUiReport }
if ($taskUiProcess.ExitCode -ne 0) { throw 'UI Automation 实际交互测试失败。' }
dotnet publish (Join-Path $PSScriptRoot 'CodexDoctor.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $taskOutput --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw '构建失败。' }
$taskExe = Join-Path $taskOutput 'CodexDoctorV9.exe'
$taskProcess = Start-Process -FilePath $taskExe -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
if ($taskProcess.ExitCode -ne 0) { throw '发布 EXE 自检失败。' }
$taskFiles = @(Get-ChildItem -LiteralPath $taskOutput -File)
if ($taskFiles.Count -ne 1 -or $taskFiles[0].Name -ne 'CodexDoctorV9.exe') { throw '发布目录不是单 EXE；请检查构建输出。' }
$taskHash = (Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $taskOutput 'SHA256.txt') -Value "$taskHash  CodexDoctorV9.exe" -Encoding ascii
Write-Output 'V9 行为测试、UI Automation 实际交互、构建、单 EXE 自检和 SHA256 已完成。'
