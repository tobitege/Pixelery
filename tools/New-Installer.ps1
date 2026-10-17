param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$CliDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$NsisPath,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$app = (Resolve-Path -LiteralPath $AppDirectory).Path
$cli = (Resolve-Path -LiteralPath $CliDirectory).Path
$compiler = (Resolve-Path -LiteralPath $NsisPath).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw
    $Version = [string]$props.Project.PropertyGroup.Version.InnerText
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a major.minor.patch version.' }
foreach ($entry in @(@($app, 'Pixelery.exe'), @($cli, 'Pixelery.Cli.exe'))) {
    if (-not (Test-Path -LiteralPath (Join-Path $entry[0] $entry[1]))) { throw ('Missing executable: {0}' -f $entry[1]) }
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$lockPath = Join-Path $output 'installer-build.lock'
$buildLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $build = Join-Path $output 'installer-build'
    New-Item -ItemType Directory -Path $build -Force | Out-Null

    function Escape-Nsis([string]$Value) {
        $Value.Replace('$', '$$').Replace('"', '$\"')
    }
    $payload = [Collections.Generic.SortedDictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in @($app, $cli)) {
        foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object Extension -ne '.pdb')) {
            $relative = [IO.Path]::GetRelativePath($root, $file.FullName)
            if ($relative -match '[*?:"<>|]' -or $relative.Contains('..') -or $relative -in @('Uninstall.exe', 'files.lst', 'files-app.lst', 'files-cli.lst')) { throw 'Unsafe payload filename.' }
            if ($payload.ContainsKey($relative)) {
                if ((Get-FileHash -LiteralPath $payload[$relative]).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
                    throw ('Conflicting shared payload file: {0}' -f $relative)
                }
            } else {
                $payload.Add($relative, $file.FullName)
            }
        }
    }
    $commands = [Collections.Generic.List[string]]::new()
    foreach ($file in $payload.GetEnumerator()) {
        $parent = Split-Path -Parent $file.Key
        $commands.Add('SetOutPath "$Stage\' + (Escape-Nsis $parent) + '"')
        $commands.Add('File "' + (Escape-Nsis $file.Value) + '"')
        $commands.Add('IfErrors install_failed')
    }
    [IO.File]::WriteAllLines((Join-Path $build 'payload.nsh'), $commands, [Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllLines((Join-Path $build 'payload.lst'), $payload.Keys, [Text.Encoding]::Unicode)
    $installer = Join-Path $output ('Pixelery-Setup-{0}-x64.exe' -f $Version)
    $script = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../installer/Pixelery.nsi')).Path
    $arguments = @('/V3', '/WX', '/INPUTCHARSET', 'UTF8', ('/DPRODUCT_VERSION=' + $Version), ('/DBUILD_DIR=' + $build), ('/DOUTPUT_FILE=' + $installer), $script)
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed.' }
    if (-not (Test-Path -LiteralPath $installer)) { throw 'NSIS did not create the installer.' }
    Write-Output $installer
}
finally { $buildLock.Dispose() }
