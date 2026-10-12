param([Parameter(Mandatory=$true)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $root ('artifacts\installer-test\' + [Guid]::NewGuid().ToString('N'))
$installPath = Join-Path $testRoot 'BotPlusAutocadTool.bundle'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$sentinel = Join-Path $testRoot 'settings-outside-bundle.json'
'{"preserve":true}' | Set-Content -LiteralPath $sentinel -Encoding utf8
$sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
$userData = Join-Path $env:LOCALAPPDATA 'BotPlusAutocadTool'
function Get-SettingsSnapshot {
    if (!(Test-Path -LiteralPath $userData)) { return '' }
    return ((Get-ChildItem -LiteralPath $userData -File -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName + '|' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }) -join "`n")
}
$settingsBefore = Get-SettingsSnapshot
function Run-TestInstall($logName) {
    $logPath = Join-Path $testRoot $logName
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER',('/DIR="'+$installPath+'"'),('/LOG="'+$logPath+'"'))
    $process = Start-Process -FilePath $InstallerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode); $logPath" }
    foreach ($folder in @('2025-2026','2027')) {
        if (!(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\BotPlusAutocadTool.dll"))) { throw "Missing installed DLL: $folder" }
    }
    if (!(Test-Path -LiteralPath (Join-Path $installPath 'PackageContents.xml'))) { throw 'Manifest missing.' }
}
Run-TestInstall 'install.log'
# A controlled obsolete library verifies cleanup during upgrade.
$obsolete = Join-Path $installPath 'Contents\Windows\2027\obsolete-test.dll'
'test-only obsolete dependency' | Set-Content -LiteralPath $obsolete
Run-TestInstall 'upgrade.log'
if (Test-Path -LiteralPath $obsolete) { throw 'Obsolete DLL was not removed during update.' }
$uninstaller = Join-Path $installPath 'unins000.exe'
$process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $testRoot 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $installPath 'Contents\Windows\2027\BotPlusAutocadTool.dll')) { throw 'Plugin survived uninstall.' }
if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw 'External settings sentinel was modified.' }
if ((Get-SettingsSnapshot) -ne $settingsBefore) { throw 'Existing user settings were modified.' }
Write-Output "PASS: install, upgrade, obsolete dependency cleanup, uninstall, settings preservation. Logs: $testRoot"
