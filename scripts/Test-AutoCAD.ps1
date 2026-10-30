param(
    [ValidateSet(2026,2027)][int]$Year = 2026,
    [switch]$Benchmark,
    [switch]$Plot,
    [switch]$Plot141,
    [switch]$PlotStyle17,
    [switch]$Units,
    [switch]$UnitInsert,
    [switch]$WidePlot,
    [switch]$IsolatedPlotters,
    [string]$ReferenceDrawing,
    [switch]$Word,
    [switch]$Library,
    [string]$Label = 'current',
    [string]$PluginPath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$hostPath = "C:\Program Files\Autodesk\AutoCAD $Year\accoreconsole.exe"
if (!(Test-Path -LiteralPath $hostPath)) { throw "AutoCAD $Year Core Console is not installed." }
if ($Label -notmatch '^[a-zA-Z0-9._-]+$') { throw 'Use a simple file label.' }
$framework = if ($Year -eq 2026) { 'net8.0-windows' } else { 'net10.0-windows' }
$reports = Join-Path $root 'artifacts\validation'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
if (!$PluginPath) {
    dotnet build (Join-Path $root 'BotPlusAutocadTool.csproj') -c Release "-p:AutoCADVersion=$Year" -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    $PluginPath = Join-Path $root "bin\Release\AutoCAD$Year\$framework\BotPlusAutocadTool.dll"
}
dotnet build (Join-Path $root 'tests\AutoCADSmoke\AutoCADSmoke.csproj') -c Release "-p:AutoCADVersion=$Year" -v quiet *> (Join-Path $reports "build-smoke-$Year-$Label.log")
if ($LASTEXITCODE -ne 0) { throw 'Smoke module build failed; see validation log.' }
$testDll = Join-Path $root "tests\AutoCADSmoke\bin\Release\$framework\AutoCADSmoke.dll"
$command = if ($PlotStyle17) { 'BOTPLUS_PLOTSTYLE17TEST' } elseif ($UnitInsert) { 'BOTPLUS_UNITINSERTTEST' } elseif ($Units) { 'BOTPLUS_UNITPLOTTEST' } elseif ($Plot141) { 'BOTPLUS_PLOT141TEST' } elseif ($WidePlot) { 'BOTPLUS_WIDEPLOTTEST' } elseif ($Library) { 'BOTPLUS_LIBRARYSETUP' } elseif ($Word) { 'BOTPLUS_WORDTRANSFERTEST' } elseif ($Benchmark) { 'BOTPLUS_BENCHMARK' } elseif ($Plot) { 'BOTPLUS_PLOTTEST' } else { 'BOTPLUS_SMOKETEST' }
$script = Join-Path $reports "test-$Year-$Label.scr"
$log = Join-Path $reports "test-$Year-$Label.log"
# The temporary Core Console profile cannot change SECURELOAD in the user's profile.
$lines = @('SECURELOAD','0','_NETLOAD',('"'+$PluginPath+'"'),'_NETLOAD',('"'+$testDll+'"'),$command,'_QUIT','_Y','')
if ($Library) {
    $dispatch = (Join-Path $reports 'library-dispatch.lsp').Replace('\','/')
    $lines = @('SECURELOAD','0','_NETLOAD',('"'+$PluginPath+'"'),'_NETLOAD',('"'+$testDll+'"'),$command,('(load "'+$dispatch+'")'),'(c:bp-library-insert)','0,0','BOTPLUS_LIBRARYVERIFY','_QUIT','_Y','')
}
if ($IsolatedPlotters) {
    $customPlotters = Join-Path $reports ('Плоттеры ' + $Year + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $customPlotters -Force | Out-Null
    $referencePlotter = Get-ChildItem -LiteralPath (Join-Path $env:APPDATA "Autodesk\AutoCAD $Year") -Filter 'DWG To PDF.pc3' -Recurse |
        Where-Object { $_.FullName -notmatch 'AutoIsolate' } | Select-Object -First 1
    if (!$referencePlotter) { throw 'A native PDF configuration is required to test an isolated printer search path.' }
    Copy-Item -LiteralPath $referencePlotter.FullName -Destination $customPlotters
    # SCRIPT input follows the host code page; construct Unicode through character
    # codes so the test exercises the real path instead of a misdecoded spelling.
    $pathCodes = ($customPlotters.Replace('\','/').ToCharArray() | ForEach-Object { [int]$_ }) -join ' '
    $lines = @('(setenv "PrinterConfigDir" (vl-list->string (list ' + $pathCodes + ')))') + $lines
}
[IO.File]::WriteAllLines($script, $lines, [Text.UTF8Encoding]::new($false))
$inputDrawing = Join-Path $reports "input-$Year-$Label.dwg"
if ($Plot) { $inputDrawing = Join-Path $reports "input-$Year-$Label-чертёж-图纸.dwg" }
if ($WidePlot -and !$ReferenceDrawing) { throw 'WidePlot requires the provided reference DWG via -ReferenceDrawing.' }
$sourceDrawing = if ($ReferenceDrawing) { $ReferenceDrawing } else { Join-Path $root 'Assets\WM_FORMAT.dwg' }
Copy-Item -LiteralPath $sourceDrawing -Destination $inputDrawing -Force
$profilePath = Join-Path $reports ('profile-'+[guid]::NewGuid().ToString('N'))
$dataPath = Join-Path $reports ('data-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $dataPath 'settings.json'), '{"AutoCheckUpdates":false}', [Text.UTF8Encoding]::new($false))
$priorDataPath = $env:BOTPLUS_DATA_DIRECTORY
try {
    $env:BOTPLUS_DATA_DIRECTORY = $dataPath
    & $hostPath /i $inputDrawing /s $script /l en-US /isolate BotPlusTest $profilePath *> $log
}
finally { $env:BOTPLUS_DATA_DIRECTORY = $priorDataPath }
$text = [IO.File]::ReadAllText($log, [Text.Encoding]::Unicode)
# Native wide stdout and PowerShell can leave mixed CR/LF code units in redirected logs.
$text = $text.Replace([string][char]0x0A0D, '').Replace([string][char]0x0D00, '')
[IO.File]::WriteAllText($log, $text, [Text.UTF8Encoding]::new($false))
$marker = if ($PlotStyle17) { 'BOTPLUS_STYLE17_PASS:' } elseif ($UnitInsert) { 'BOTPLUS_UNITINSERT_PASS:' } elseif ($Units) { 'BOTPLUS_UNITS_PASS:' } elseif ($Plot141) { 'BOTPLUS_PLOT141_PASS:' } elseif ($WidePlot) { 'BOTPLUS_WIDEPLOT_PASS:' } elseif ($Library) { 'LIBRARY_PASS:' } elseif ($Word) { 'WORD_TRANSFER_PASS:' } elseif ($Benchmark) { 'BOTPLUS_PERF_PASS:' } elseif ($Plot) { 'BOTPLUS_PLOT_PASS:' } else { 'BOTPLUS_SMOKE_PASS:' }
if ($text -notmatch $marker -or $text -match '(BOTPLUS_(SMOKE|PERF|STYLE17|PLOT141|PLOT|WIDEPLOT|UNITS|UNITINSERT)|WORD_TRANSFER|LIBRARY)_FAIL:') { throw "Native checks failed: $log" }
if ($IsolatedPlotters -and !(Test-Path -LiteralPath (Join-Path $customPlotters 'BotPlus PDF.pc3'))) { throw 'Plotter was installed outside the configured printer search path.' }
$text.Split("`n") | Where-Object { $_ -match 'PASS:' } | ForEach-Object { $_.Trim() }
Write-Output "Native report: $log"
