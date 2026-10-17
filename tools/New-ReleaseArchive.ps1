param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,
    [Parameter(Mandatory)]
    [string]$Executable,
    [Parameter(Mandatory)]
    [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $PublishDirectory).Path
$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object Extension -ne '.pdb')
if (-not (Test-Path -LiteralPath (Join-Path $root $Executable) -PathType Leaf)) {
    throw ('Published executable missing: {0}' -f $Executable)
}
$destination = [IO.Path]::GetFullPath($ArchivePath)
$parent = [IO.Path]::GetDirectoryName($destination)
[IO.Directory]::CreateDirectory($parent) | Out-Null

$stream = [IO.File]::Open($destination, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $name = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$check = [IO.Compression.ZipFile]::OpenRead($destination)
try {
    if ($check.Entries.Count -ne $files.Count -or -not $check.GetEntry($Executable)) {
        throw 'Release archive is incomplete.'
    }
}
finally {
    $check.Dispose()
}
Write-Output ('Release archive verified: {0} ({1} files).' -f $destination, $files.Count)
