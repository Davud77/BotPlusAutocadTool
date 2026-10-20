param([switch]$Native, [ValidateSet(2026,2027)][int]$Year = 2026)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Verify-Structure.ps1')
foreach ($profile in @(2025,2026,2027)) {
    dotnet build (Join-Path $root 'BotPlusAutocadTool.csproj') -c Release "-p:AutoCADVersion=$profile" -p:UseAutoCADNuGet=true -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $profile" }
}
foreach ($framework in @('net8.0','net10.0')) {
    dotnet run --project (Join-Path $root 'tests\UpdateChecks\UpdateChecks.csproj') -c Release -f $framework
    if ($LASTEXITCODE -ne 0) { throw "Update checks failed: $framework" }
    dotnet run --project (Join-Path $root 'tests\CoreChecks\CoreChecks.csproj') -c Release -f "$framework-windows"
    if ($LASTEXITCODE -ne 0) { throw "Core checks failed: $framework" }
}
dotnet run --project (Join-Path $root 'tests\PdfChecks\PdfChecks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'PDF processing checks failed.' }
if ($Native) {
    & (Join-Path $PSScriptRoot 'Test-AutoCAD.ps1') -Year $Year -Label candidate
    & (Join-Path $PSScriptRoot 'Test-AutoCAD.ps1') -Year $Year -Plot -Label candidate-pdf
    & (Join-Path $PSScriptRoot 'Test-AutoCAD.ps1') -Year $Year -Benchmark -Label candidate-performance
}
Write-Output 'PASS: all requested validation stages completed.'
