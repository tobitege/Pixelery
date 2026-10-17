param([Parameter(Mandatory)][string]$Directory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$version = '3.13'
$expectedHash = 'BA63DFFC4410EE89193E1CB5A41989991BD77C61068DA17E3156D136B7B0B3D8'
$root = [IO.Path]::GetFullPath($Directory)
$archive = Join-Path $root ('nsis-{0}.zip' -f $version)
$compiler = Join-Path $root ('nsis-{0}/makensis.exe' -f $version)
New-Item -ItemType Directory -Path $root -Force | Out-Null
if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive).Hash -ne $expectedHash) {
    $uri = 'https://downloads.sourceforge.net/project/nsis/NSIS%203/3.13/nsis-3.13.zip'
    Invoke-WebRequest -Uri $uri -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive).Hash -ne $expectedHash) {
        # SourceForge can return an HTML mirror-selection page before the download.
        $html = Get-Content -LiteralPath $archive -Raw
        $pattern = 'https://downloads\.sourceforge\.net/project/nsis/NSIS%203/3\.13/nsis-3\.13\.zip\?[^"<> ]+'
        $match = [regex]::Match($html, $pattern)
        if (-not $match.Success) { throw 'The NSIS download did not match the pinned archive.' }
        Invoke-WebRequest -Uri ([Net.WebUtility]::HtmlDecode($match.Value)) -OutFile $archive
    }
}
if ((Get-FileHash -LiteralPath $archive).Hash -ne $expectedHash) { throw 'NSIS archive checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $root -Force
if (-not (Test-Path -LiteralPath $compiler)) { throw 'NSIS compiler missing from the archive.' }
$compiler
