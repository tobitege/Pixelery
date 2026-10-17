param(
    [Parameter(Mandatory)]
    [string]$Directory,
    [Parameter(Mandatory)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $Directory).Path
$executablePath = Join-Path $root $Executable
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw ('Published executable missing: {0}' -f $Executable)
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse)
foreach ($file in $files) {
    if ($file.Name -in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll') -or $file.Name.EndsWith('.deps.json')) {
        throw ('Unexpected managed runtime file: {0}' -f $file.FullName)
    }
    if ($file.Extension -notin @('.exe', '.dll')) { continue }

    $stream = [IO.File]::OpenRead($file.FullName)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw ('Invalid PE image: {0}' -f $file.Name) }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadUInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw ('Invalid PE signature: {0}' -f $file.Name) }
        $machine = $reader.ReadUInt16()
        if ($file.FullName -eq $executablePath -and $machine -ne 0x8664) {
            throw ('Expected x64 executable: {0}' -f $file.Name)
        }
        $stream.Position = $peOffset + 24
        $optionalMagic = $reader.ReadUInt16()
        $directoryOffset = switch ($optionalMagic) {
            0x20B { 112 }
            0x10B { 96 }
            default { throw ('Invalid PE optional header: {0}' -f $file.Name) }
        }
        # The CLR runtime header is data directory 14. SDK resource DLLs can use other architectures.
        $stream.Position = $peOffset + 24 + $directoryOffset + 14 * 8
        if ($reader.ReadUInt32() -ne 0 -or $reader.ReadUInt32() -ne 0) {
            throw ('Managed assembly found in Native AOT publish: {0}' -f $file.Name)
        }
    }
    finally {
        $reader.Dispose()
    }
}
Write-Output ('Native publish verified: {0} ({1} files).' -f $Executable, $files.Count)
