$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sourceFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -in '.cs','.xaml' -and $_.FullName -notmatch '\\(bin|obj|artifacts|tests)\\' }
$commands = foreach ($file in $sourceFiles | Where-Object Extension -eq '.cs') {
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file.FullName), 'CommandMethod\("([^"]+)"')) { $match.Groups[1].Value }
}
$expected = @('Align_Den','AUTONUM','НУМ','WSELECT','SUMLENGTH','MULTICOPY','PLANPREP','CLEANDWG','CLEANUNSELECTED','STRIPTEXT','WEAM_STAMP','WEAM_BATCHPLOT','WPLOT','WEAM_CUSTOMERORDER','WEAM_TABLEEXCEL','WEAM_INFO','BOTPLUS_CHECKUPDATES','BOTPLUS_INSERTFORMAT','BOTPLUS_SETTINGS','BOTPLUS_CLASSIC','BOTPLUS_ADDITIONAL','BOTPLUS_EXPORTEXCEL','BOTPLUS_IMPORTEXCEL','BOTPLUS_CLEARTABLESTYLE')
if (@($expected | Where-Object { $_ -notin $commands }).Count -or @($commands | Group-Object | Where-Object Count -gt 1).Count) { throw 'Missing or duplicate commands.' }
$xmlFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -in '.xaml','.xml','.csproj','.props' -and $_.FullName -notmatch '\\(bin|obj|artifacts|tests)\\' }
foreach ($file in $xmlFiles) { $null = [xml][IO.File]::ReadAllText($file.FullName) }
foreach ($file in $sourceFiles) { if ([IO.File]::ReadAllText($file.FullName) -match 'WM_autocad') { throw "Old namespace: $($file.FullName)" } }
$app = [IO.File]::ReadAllText((Join-Path $root 'UI\CommandCatalog.cs'))
foreach ($match in [regex]::Matches($app, 'new ButtonDefinition\("[^"]+", "([^"]+\.png)", "([^"]+)"')) {
    if (!(Test-Path -LiteralPath (Join-Path $root ('Resources\' + $match.Groups[1].Value)))) { throw 'Missing ribbon icon.' }
    if ($match.Groups[2].Value -notin $commands) { throw 'Ribbon references an unregistered command.' }
}
if ([regex]::Matches($app, 'new ButtonDefinition\(').Count -ne 19) { throw 'Unexpected ribbon button count.' }
$vm = [IO.File]::ReadAllText((Join-Path $root 'ViewModels\StampPropsViewModel.cs'))
$xaml = [IO.File]::ReadAllText((Join-Path $root 'Views\StampPropsControl.xaml'))
foreach ($match in [regex]::Matches($xaml, 'FieldsMap\[([^\]]+)\]')) {
    if (!$vm.Contains('"' + $match.Groups[1].Value + '"')) { throw "Unmatched stamp binding: $($match.Groups[1].Value)" }
}
Write-Output "PASS: $(@($commands).Count) commands; $(@($xmlFiles).Count) XML/XAML; namespaces, ribbon icons and stamp bindings."
