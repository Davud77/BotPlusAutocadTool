param([string]$Label='v16')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$hostExe='C:\Program Files\Autodesk\AutoCAD 2026\acad.exe'
if (Get-Process acad -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $hostExe -or [string]::IsNullOrWhiteSpace($_.Path)}) { throw 'Close user AutoCAD 2026 before the isolated interface test.' }
function Invoke-Cad([scriptblock]$Operation) {
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    do {
        try { return (& $Operation) }
        catch {
            if ($_.Exception.GetBaseException().HResult -notin @(-2147418111,-2147417846) -or [DateTime]::UtcNow -gt $deadline) { throw }
            Start-Sleep -Milliseconds 250
        }
    } while ($true)
}
dotnet build (Join-Path $root 'tests/DesktopChecks/DesktopChecks.csproj') -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Interface module build failed.' }
$folder=Join-Path $root ('artifacts/validation/interface-'+$Label+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$drawing=Join-Path $folder 'fixture.dwg'
Copy-Item -LiteralPath (Join-Path $root 'Assets/WM_FORMAT.dwg') -Destination $drawing
$priorData=$env:BOTPLUS_DATA_DIRECTORY
$app=$null; $doc=$null; $oldSecure=$null; $oldRibbon=$null
try {
    $env:BOTPLUS_DATA_DIRECTORY=$folder
    [IO.File]::WriteAllText((Join-Path $folder 'settings.json'),'{"AutoCheckUpdates":false}')
    $app=New-Object -ComObject 'AutoCAD.Application.25.1'
    Invoke-Cad { $app.Visible=$false }
    $doc=Invoke-Cad { $app.Documents.Open($drawing,$false) }
    $oldSecure=Invoke-Cad { $doc.GetVariable('SECURELOAD') }
    $oldRibbon=Invoke-Cad { $doc.GetVariable('RIBBONSTATE') }
    Invoke-Cad { $doc.SetVariable('SECURELOAD',[int16]0) }
    $dll=(Join-Path $root 'tests/DesktopChecks/bin/Release/net8.0-windows/DesktopChecks.dll').Replace('\','/')
    Invoke-Cad { $doc.SendCommand("_.RIBBON`n(command `"_.NETLOAD`" `"$dll`")`nBOTPLUS_DESKTOPRECHECK`n") }
    $report=Join-Path $folder 'desktop-check.log'
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    do {
        if (Test-Path -LiteralPath $report) {
            $text=Get-Content -LiteralPath $report -Raw
            if ($text -match 'FAIL:') { throw $text }
            if ($text -match 'PASS: desktop') { break }
        }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($text -notmatch 'PASS: desktop') { throw "Interface test timed out: $report" }
    Invoke-Cad { $doc.SendCommand("BOTPLUS_DESKTOPFINISH`n") }
    $text
    "Interface report: $report"
} finally {
    if ($doc -and $null -ne $oldSecure) { try { Invoke-Cad { $doc.SetVariable('SECURELOAD',$oldSecure) } } catch {} }
    if ($doc -and $oldRibbon -eq 0) { try { Invoke-Cad { $doc.SendCommand("_.RIBBONCLOSE`n") } } catch {} }
    if ($doc) { try { Invoke-Cad { $doc.Close($false) } } catch {} }
    if ($app) { try { Invoke-Cad { $app.Quit() } } catch {} }
    $env:BOTPLUS_DATA_DIRECTORY=$priorData
}
