param([Parameter(Mandatory=$true)][string]$InstallerPath, [string]$PreviousInstallerPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$existing = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{98BED5F2-3CAB-47DD-8344-E681FEA048DB}_is1' -ErrorAction SilentlyContinue
$temporaryTestPrefix = Join-Path ([IO.Path]::GetTempPath()) 'BotPlusInstallerTest-'
if ($existing.InstallLocation -and
    !$existing.InstallLocation.StartsWith((Join-Path $root 'artifacts\installer-test\'), [StringComparison]::OrdinalIgnoreCase) -and
    !$existing.InstallLocation.StartsWith($temporaryTestPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'A real per-user installation exists; isolated test will not overwrite its registration.' }
# Keep long library asset paths below the installer's Windows path limit.
$testRoot = $temporaryTestPrefix + [Guid]::NewGuid().ToString('N').Substring(0,8)
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
function Run-TestInstall($logName, $executable = $InstallerPath) {
    $logPath = Join-Path $testRoot $logName
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER',('/DIR="'+$installPath+'"'),('/LOG="'+$logPath+'"'))
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode); $logPath" }
    foreach ($folder in @('2025-2026','2027')) {
        if (!(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\BotPlusAutocadTool.dll"))) { throw "Missing installed DLL: $folder" }
        if ($executable -eq $InstallerPath -and !(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\Assets\WM_FORMAT.dwg"))) { throw "Missing installed format template: $folder" }
        if ($executable -eq $InstallerPath) {
            foreach ($plotter in @('BotPlus PDF.pc3', 'PMP Files\BotPlus PDF.pmp')) {
                if (!(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\Assets\Plotters\$plotter"))) { throw "Missing installed PDF plotter: $folder/$plotter" }
            }
            if (!(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\Assets\AdditionalFunctions\catalog.json"))) { throw "Missing installed LISP catalogue: $folder" }
            foreach ($dependency in @('BotPlusAutocadTool.ExcelRuntime.dll','ClosedXML.dll','DocumentFormat.OpenXml.dll')) {
                if (!(Test-Path -LiteralPath (Join-Path $installPath "Contents\Windows\$folder\ExcelRuntime\$dependency"))) { throw "Missing isolated XLSX dependency: $folder/$dependency" }
            }
        }
    }
    if (!(Test-Path -LiteralPath (Join-Path $installPath 'PackageContents.xml'))) { throw 'Manifest missing.' }
    $installedManifest = [xml](Get-Content -LiteralPath (Join-Path $installPath 'PackageContents.xml') -Raw)
    foreach ($folder in @('2025-2026','2027')) {
        $dllPath = Join-Path $installPath "Contents\Windows\$folder\BotPlusAutocadTool.dll"
        $dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath).ProductVersion.Split('+')[0]
        if ($dllVersion -ne $installedManifest.ApplicationPackage.AppVersion) { throw 'Installed DLL and manifest versions differ.' }
    }
    if ($executable -eq $InstallerPath -and !(Test-Path -LiteralPath (Join-Path $installPath 'Documentation\docs\USAGE-1.4.md'))) {
        throw 'Candidate documentation missing.'
    }
}
if ($PreviousInstallerPath) { Run-TestInstall 'previous-install.log' $PreviousInstallerPath }
else { Run-TestInstall 'install.log' }
# A controlled obsolete library verifies cleanup during upgrade.
$obsolete = Join-Path $installPath 'Contents\Windows\2027\obsolete-test.dll'
'test-only obsolete dependency' | Set-Content -LiteralPath $obsolete
Run-TestInstall 'upgrade.log'
if ($PreviousInstallerPath) {
    $log = Get-Content -LiteralPath (Join-Path $testRoot 'upgrade.log') -Raw
    if ($log -notmatch 'BotPlus: uninstall previous version') { throw 'Previous version uninstaller was not invoked.' }
}
if (Test-Path -LiteralPath $obsolete) { throw 'Obsolete DLL was not removed during update.' }
$uninstaller = Get-ChildItem -LiteralPath $installPath -Filter 'unins*.exe' | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
if (!$uninstaller) { throw 'New version uninstaller was not created.' }
$process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $testRoot 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $installPath 'Contents\Windows\2027\BotPlusAutocadTool.dll')) { throw 'Plugin survived uninstall.' }
if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw 'External settings sentinel was modified.' }
if ((Get-SettingsSnapshot) -ne $settingsBefore) { throw 'Existing user settings were modified.' }
Write-Output "PASS: install, upgrade, obsolete dependency cleanup, uninstall, settings preservation. Logs: $testRoot"
