param([string]$ReferenceDrawing, [string]$Label='gost14', [switch]$SyntheticGost, [switch]$Transparency141, [ValidateSet(0,1,6)][int]$UnitCode=0, [ValidateSet(2026,2027)][int]$Year=2026)
$ErrorActionPreference = 'Stop'
function Invoke-Cad([scriptblock]$Operation) {
    $until = [DateTime]::UtcNow.AddSeconds(45)
    do {
        try { return (& $Operation) }
        catch {
            $code = $_.Exception.GetBaseException().HResult
            if ($code -notin @(-2147418111, -2147417846) -or [DateTime]::UtcNow -gt $until) { throw }
            Start-Sleep -Milliseconds 250
        }
    } while ($true)
}
$root = Split-Path $PSScriptRoot -Parent
$hostExe = "C:\Program Files\Autodesk\AutoCAD $Year\acad.exe"
if (Get-Process acad -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $hostExe }) { throw "Close user AutoCAD $Year before the isolated desktop test." }
$candidate = Join-Path $root "artifacts/validation/desktop-candidate14-$Year"
# A distinct assembly identity avoids accidentally testing an already autoloaded
# installed version. Production bundles keep their normal assembly name.
dotnet build (Join-Path $root 'BotPlusAutocadTool.csproj') -c Release "-p:AutoCADVersion=$Year" -p:AssemblyName=BotPlusAutocadTool.Candidate14 "-p:OutputPath=$candidate/" -p:AppendTargetFrameworkToOutputPath=false -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$plugin = Join-Path $candidate 'BotPlusAutocadTool.Candidate14.dll'
dotnet build (Join-Path $root 'tests/PublishDesktopChecks/PublishDesktopChecks.csproj') -c Release "-p:AutoCADVersion=$Year" "-p:TestPluginPath=$plugin" -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Desktop checks build failed.' }
$directory = Join-Path $root ('artifacts/validation/publish-desktop-' + $Label + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$drawing = Join-Path $directory 'fixture.dwg'
Copy-Item -LiteralPath $(if ($ReferenceDrawing) { $ReferenceDrawing } else { Join-Path $root 'Assets/WM_FORMAT.dwg' }) -Destination $drawing
$priorData = $env:BOTPLUS_DATA_DIRECTORY
$priorGost = $env:BOTPLUS_GOST_DRAWING
$priorUnit = $env:BOTPLUS_TEST_UNIT_CODE
$application = $null; $document = $null; $oldSecure = $null; $oldTransparency = $null
try {
    $env:BOTPLUS_DATA_DIRECTORY = Join-Path $directory 'data'
    New-Item -ItemType Directory -Path $env:BOTPLUS_DATA_DIRECTORY -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $env:BOTPLUS_DATA_DIRECTORY 'settings.json'), '{"AutoCheckUpdates":false}')
    $env:BOTPLUS_GOST_DRAWING = if ($Transparency141) { 'transparency' } elseif ($SyntheticGost) { 'matrix' } elseif ($ReferenceDrawing) { '1' } else { '0' }
    $env:BOTPLUS_TEST_UNIT_CODE = $UnitCode.ToString()
    $progid = if ($Year -eq 2026) { 'AutoCAD.Application.25.1' } else { 'AutoCAD.Application.26' }
    $application = New-Object -ComObject $progid
    Invoke-Cad { $application.Visible = $false }
    $document = Invoke-Cad { $application.Documents.Open($drawing, $false) }
    $oldSecure = Invoke-Cad { $document.GetVariable('SECURELOAD') }
    $oldTransparency = Invoke-Cad { $document.GetVariable('PLOTTRANSPARENCYOVERRIDE') }
    Invoke-Cad { $document.SetVariable('SECURELOAD', [int16]0) }
    $framework = if ($Year -eq 2026) { 'net8.0-windows' } else { 'net10.0-windows' }
    $check = Join-Path $root "tests/PublishDesktopChecks/bin/Release/$framework/PublishDesktopChecks.dll"
    Invoke-Cad { $document.SendCommand("(command `"_.NETLOAD`" `"$($plugin.Replace('\','/'))`" `"_.NETLOAD`" `"$($check.Replace('\','/'))`")`n") }
    Invoke-Cad { $document.SendCommand("BOTPLUS_PUBLISHPIPELINETEST`n") }
    $report = Join-Path $directory 'pipeline.log'
    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    $continued = $false
    do {
        if (Test-Path -LiteralPath $report) {
            $text = [IO.File]::ReadAllText($report)
            if ($text -match 'BOTPLUS_PIPELINE_FAIL:') { throw $text }
            if ($text -match 'BOTPLUS_PIPELINE_PASS:') { $text; return }
            if (!$continued -and $text -match 'BOTPLUS_PIPELINE_NEXT') {
                $continued = $true
                Invoke-Cad { $document.SendCommand("BOTPLUS_PUBLISHPIPELINECONTINUE`n") }
            }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Desktop publication timed out: $report"
}
finally {
    if ($document -and $null -ne $oldTransparency) { try { $document.SetVariable('PLOTTRANSPARENCYOVERRIDE', $oldTransparency) } catch {} }
    if ($document -and $null -ne $oldSecure) { try { $document.SetVariable('SECURELOAD', $oldSecure) } catch {} }
    if ($document) { try { $document.Close($false) } catch {} }
    if ($application) { try { $application.Quit() } catch {} }
    $env:BOTPLUS_DATA_DIRECTORY = $priorData
    $env:BOTPLUS_GOST_DRAWING = $priorGost
    $env:BOTPLUS_TEST_UNIT_CODE = $priorUnit
}
