param(
    [Parameter(Mandatory)]
    [string]$CliPath,
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$cli = (Resolve-Path -LiteralPath $CliPath).Path
if ([IO.Path]::GetFileName($cli) -cne 'Pixelery.Cli.exe') { throw 'Expected the renamed Pixelery.Cli.exe executable.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$runDirectory = Join-Path $outputRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $ExpectedVersion = [string]$props.Project.PropertyGroup.Version.InnerText
}
$versionArguments = @('version')
$versionOutput = & $cli @versionArguments
if ($LASTEXITCODE -ne 0) { throw 'The published CLI could not report its version.' }
$versionPattern = '^Pixelery\.Cli ' + [regex]::Escape($ExpectedVersion) + ' '
if ($versionOutput[0] -notmatch $versionPattern) {
    throw ('Unexpected CLI version: {0}' -f $versionOutput[0])
}
$helpArguments = @('--help')
$helpOutput = & $cli @helpArguments
if ($LASTEXITCODE -ne 0 -or ($helpOutput -join [Environment]::NewLine) -notmatch 'Usage: Pixelery\.Cli\.exe <command>') {
    throw 'CLI help does not use the renamed executable.'
}

$commonArguments = @(
    '--styles', 'truchet', '--palettes', 'neon', '--tones', 'dark',
    '--detail', 'high', '--sizes', '320x180', '--seed', '42',
    '--no-grain', '--no-vignette', '--no-exif', '--no-watermark'
)

function Assert-Image {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw ('Image missing: {0}' -f $Path)
    }
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        if ($image.Width -ne 320 -or $image.Height -ne 180) {
            throw ('Unexpected image dimensions: {0}x{1}' -f $image.Width, $image.Height)
        }
    }
    finally {
        $image.Dispose()
    }
}

foreach ($name in @('first.png', 'second.png')) {
    $path = Join-Path $runDirectory $name
    $renderArguments = @('render', $path) + $commonArguments + @('--quiet')
    & $cli @renderArguments
    if ($LASTEXITCODE -ne 0) { throw ('Rendering failed: {0}' -f $name) }
    Assert-Image -Path $path
}
$firstPath = Join-Path $runDirectory 'first.png'
$secondPath = Join-Path $runDirectory 'second.png'
if ((Get-FileHash -LiteralPath $firstPath).Hash -ne (Get-FileHash -LiteralPath $secondPath).Hash) {
    throw 'The same seed produced different PNG files.'
}

$batchDirectory = Join-Path $runDirectory 'batch'
$batchArguments = @('generate') + $commonArguments + @(
    '--out', $batchDirectory, '--count', '2', '--threads', '2',
    '--formats', 'png', '--name', '{style}/{n:000}', '--no-spread-dates', '--json'
)
$batchOutput = & $cli @batchArguments
if ($LASTEXITCODE -ne 0) { throw 'Batch generation failed.' }
$summary = ($batchOutput -join [Environment]::NewLine) | ConvertFrom-Json
if ($summary.requested -ne 2 -or $summary.written -ne 2 -or $summary.failed -ne 0 -or $summary.cancelled) {
    throw 'The batch summary did not report two successful images.'
}
$batchFiles = @(Get-ChildItem -LiteralPath $batchDirectory -Filter '*.png' -File -Recurse)
if ($batchFiles.Count -ne 2) { throw 'Batch generation did not produce exactly two PNG files.' }
foreach ($name in @('001.png', '002.png')) {
    $path = Join-Path (Join-Path $batchDirectory 'truchet') $name
    Assert-Image -Path $path
}

$listArguments = @('list', 'styles')
$styleOutput = & $cli @listArguments
if ($LASTEXITCODE -ne 0) { throw 'Listing styles failed.' }
$styleIds = @($styleOutput | ForEach-Object {
    if ($_ -match '^  (\S+)\s+') { $Matches[1] }
})
if ($styleIds.Count -lt 37) { throw 'The style catalog is incomplete.' }
foreach ($style in $styleIds) {
    $path = Join-Path $runDirectory ($style + '.png')
    $styleArguments = @(
        'render', $path, '--styles', $style, '--sizes', '320x180', '--seed', '42',
        '--no-grain', '--no-vignette', '--no-watermark', '--quiet'
    )
    & $cli @styleArguments
    if ($LASTEXITCODE -ne 0) { throw ('Style rendering failed: {0}' -f $style) }
    Assert-Image -Path $path
}

$watermarkPath = Join-Path $runDirectory 'watermark.png'
$watermarkArguments = @(
    'render', $watermarkPath, '--styles', 'truchet', '--palettes', 'neon', '--tones', 'dark',
    '--detail', 'high', '--sizes', '320x180', '--seed', '42', '--no-grain', '--no-vignette',
    '--watermark-text', 'Pixelery العربية שלום', '--watermark-size', '15', '--quiet'
)
& $cli @watermarkArguments
if ($LASTEXITCODE -ne 0) { throw 'Text shaping and watermark rendering failed.' }
Assert-Image -Path $watermarkPath
if ((Get-FileHash -LiteralPath $firstPath).Hash -eq (Get-FileHash -LiteralPath $watermarkPath).Hash) {
    throw 'The watermark did not change the rendered image.'
}

$configPath = Join-Path $runDirectory 'generator.json'
$configArguments = @('generate') + $commonArguments + @(
    '--out', $batchDirectory, '--save-config', $configPath, '--dry-run', '--quiet'
)
& $cli @configArguments
if ($LASTEXITCODE -ne 0) { throw 'Saving generator settings failed.' }
$loadArguments = @('generate', '--config', $configPath, '--dry-run', '--quiet')
& $cli @loadArguments
if ($LASTEXITCODE -ne 0) { throw 'Loading generator settings failed.' }

Write-Output ('CLI smoke test passed: version {0}, deterministic render, parallel batch, {1} styles, text shaping, and settings round-trip.' -f $ExpectedVersion, $styleIds.Count)
Write-Output ('Test images: {0}' -f $runDirectory)
