param([Parameter(Mandatory=$true)][string]$ArchivePath, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$archiveHash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
$marker = Join-Path $Destination '.archive-sha256'
if ((Test-Path -LiteralPath $marker) -and ([IO.File]::ReadAllText($marker) -eq $archiveHash)) { return }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $Destination -Force
if (!(Test-Path -LiteralPath (Join-Path $Destination 'catalog.json'))) { throw 'Bundled LISP catalogue missing.' }
[IO.File]::WriteAllText($marker, $archiveHash)
