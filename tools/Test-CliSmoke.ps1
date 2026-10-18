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
    param([string]$Path, [int]$ExpectedWidth = 320, [int]$ExpectedHeight = 180)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw ('Image missing: {0}' -f $Path)
    }
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        if ($image.Width -ne $ExpectedWidth -or $image.Height -ne $ExpectedHeight) {
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

$sizeDirectory = Join-Path $runDirectory 'size-cycle'
$sizeConfig = Join-Path $runDirectory 'size-settings.json'
$sizeArguments = @(
    'generate', '--config', $configPath, '--multiple-sizes', '--out', $sizeDirectory,
    '--styles', 'truchet', '--sizes', '320x180,640x360',
    '--orientation', 'portrait', '--start', '17', '--count', '4', '--threads', '3',
    '--formats', 'png', '--name', '{n:000}', '--seed', '42', '--no-random-sizes',
    '--save-config', $sizeConfig, '--quiet'
)
& $cli @sizeArguments
if ($LASTEXITCODE -ne 0) { throw 'Ordered size generation failed.' }
for ($index = 0; $index -lt 4; $index++) {
    $path = Join-Path $sizeDirectory ('{0:000}.png' -f (17 + $index))
    $factor = 1 + ($index % 2)
    Assert-Image -Path $path -ExpectedWidth (180 * $factor) -ExpectedHeight (320 * $factor)
}
$sizeSettings = Get-Content -LiteralPath $sizeConfig -Raw | ConvertFrom-Json
if ($sizeSettings.FormatVersion -ne 1 -or -not $sizeSettings.MultipleSizes -or $sizeSettings.RandomSizes) {
    throw 'The settings did not preserve the ordered multiple-size mode and format version.'
}
$randomArguments = @('generate', '--config', $sizeConfig, '--random-sizes', '--save-config', $sizeConfig, '--dry-run', '--quiet')
& $cli @randomArguments
if ($LASTEXITCODE -ne 0) { throw 'Random size selection failed.' }
$sizeSettings = Get-Content -LiteralPath $sizeConfig -Raw | ConvertFrom-Json
if (-not $sizeSettings.RandomSizes) { throw 'Random size selection was not saved.' }
$rememberedConfig = Join-Path $runDirectory 'remembered-size-settings.json'
$rememberArguments = @(
    'generate', '--config', $sizeConfig, '--no-multiple-sizes',
    '--save-config', $rememberedConfig, '--dry-run', '--quiet'
)
& $cli @rememberArguments
if ($LASTEXITCODE -ne 0) { throw 'Explicit single-size mode failed.' }
$remembered = Get-Content -LiteralPath $rememberedConfig -Raw | ConvertFrom-Json
if ($remembered.MultipleSizes -or $remembered.RandomSizes -or
    ($remembered.FixedSizes -join ',') -ne '320x180' -or ($remembered.Orientations -join ',') -ne 'Landscape') {
    throw 'Single-size mode did not restore the remembered landscape size and disable random selection.'
}
$restoreArguments = @(
    'generate', '--config', $rememberedConfig, '--multiple-sizes',
    '--save-config', $rememberedConfig, '--dry-run', '--quiet'
)
& $cli @restoreArguments
if ($LASTEXITCODE -ne 0) { throw 'Restoring explicit multiple-size mode failed.' }
$remembered = Get-Content -LiteralPath $rememberedConfig -Raw | ConvertFrom-Json
if (-not $remembered.MultipleSizes -or $remembered.RandomSizes -or
    ($remembered.FixedSizes -join ',') -ne '320x180,640x360' -or ($remembered.Orientations -join ',') -ne 'Portrait') {
    throw 'Multiple-size mode did not restore the remembered portrait sizes.'
}

$randomSequences = @()
foreach ($runNumber in 1..2) {
    $randomFolder = Join-Path $runDirectory ('random-sizes-{0}' -f $runNumber)
    $randomArguments = @(
        'generate', '--config', $rememberedConfig, '--random-sizes', '--count', '12',
        '--name', '{n:000}_{w}x{h}', '--out', $randomFolder, '--quiet'
    )
    & $cli @randomArguments
    if ($LASTEXITCODE -ne 0) { throw 'Random-size batch generation failed.' }
    $files = @(Get-ChildItem -LiteralPath $randomFolder -Filter '*.png' -File | Sort-Object Name)
    if ($files.Count -ne 12) { throw 'Random-size batch did not produce twelve images.' }
    $sizes = @()
    foreach ($file in $files) {
        $size = $file.BaseName.Split('_')[1]
        if ($size -notin @('180x320', '360x640')) { throw ('Unexpected random size: {0}' -f $size) }
        $dimensions = $size.Split('x')
        Assert-Image -Path $file.FullName -ExpectedWidth ([int]$dimensions[0]) -ExpectedHeight ([int]$dimensions[1])
        $sizes += $size
    }
    $ordered = @(0..11 | ForEach-Object { if ($_ % 2 -eq 0) { '180x320' } else { '360x640' } })
    if (($sizes | Select-Object -Unique).Count -ne 2 -or ($sizes -join ',') -eq ($ordered -join ',')) {
        throw 'Random size selection did not differ from ordered cycling.'
    }
    $randomSequences += ,@($files | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash })
}
if (($randomSequences[0] -join ',') -ne ($randomSequences[1] -join ',')) {
    throw 'Random sizes and image bytes were not reproducible with the same seed.'
}

foreach ($invalidMode in @(
    @('--no-multiple-sizes', '--sizes', '320x180,640x360'),
    @('--no-multiple-sizes', '--random-sizes'),
    @('--multiple-sizes', '--no-multiple-sizes'),
    @('--random-sizes', '--no-random-sizes')
)) {
    $invalidArguments = @('generate', '--dry-run', '--quiet') + $invalidMode
    & $cli @invalidArguments 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 2) { throw ('Conflicting size options were accepted: {0}' -f ($invalidMode -join ' ')) }
}
$singleArguments = @(
    'generate', '--config', $sizeConfig, '--sizes', '320x180', '--orientation', 'landscape',
    '--save-config', $sizeConfig, '--dry-run', '--quiet'
)
& $cli @singleArguments
if ($LASTEXITCODE -ne 0) { throw 'Replacing multiple sizes with one size failed.' }
$sizeSettings = Get-Content -LiteralPath $sizeConfig -Raw | ConvertFrom-Json
if ($sizeSettings.MultipleSizes -or $sizeSettings.RandomSizes) { throw 'A single size did not disable both size options.' }
$obsoleteConfig = Join-Path $runDirectory 'unversioned-settings.json'
$sizeSettings.PSObject.Properties.Remove('FormatVersion')
$sizeSettings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $obsoleteConfig -Encoding utf8
$obsoleteArguments = @('generate', '--config', $obsoleteConfig, '--dry-run', '--quiet')
& $cli @obsoleteArguments 2>&1 | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'An unversioned settings file was accepted.' }

$dateConfig = Join-Path $runDirectory 'timestamp-settings.json'
$dateFolder = Join-Path $runDirectory 'timestamps'
$dateArguments = @(
    'generate', '--out', $dateFolder, '--count', '3', '--threads', '2',
    '--styles', 'truchet', '--sizes', '96x64', '--seed', '42', '--formats', 'jpeg',
    '--name', '{n:00}_{date:yyyy-MM-dd}',
    '--spread-from', '2024-02', '--spread-to', '2024-02', '--save-config', $dateConfig, '--quiet'
)
& $cli @dateArguments
if ($LASTEXITCODE -ne 0) { throw 'Timestamp-range generation failed.' }
$dateFiles = @(Get-ChildItem -LiteralPath $dateFolder -File)
if ($dateFiles.Count -ne 3) { throw 'Timestamp-range generation did not produce three images.' }
foreach ($file in $dateFiles) {
    Assert-Image -Path $file.FullName -ExpectedWidth 96 -ExpectedHeight 64
    $nameDate = [DateTime]::ParseExact($file.BaseName.Substring(3), 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    if ($nameDate.Year -ne 2024 -or $nameDate.Month -ne 2 -or
        $file.CreationTime.Date -ne $nameDate -or $file.LastWriteTime.Date -ne $nameDate) {
        throw ('File timestamps do not match the selected month: {0}' -f $file.Name)
    }
}
$disableArguments = @('generate', '--config', $dateConfig, '--no-spread-dates', '--save-config', $dateConfig, '--dry-run', '--quiet')
& $cli @disableArguments
if ($LASTEXITCODE -ne 0) { throw 'Disabling timestamp distribution failed.' }
$dateSettings = Get-Content -LiteralPath $dateConfig -Raw | ConvertFrom-Json
if ($dateSettings.SpreadDates -or $dateSettings.SpreadFrom -ne '2024-02-01' -or $dateSettings.SpreadTo -ne '2024-02-01') {
    throw 'The selected period was lost when timestamp distribution was disabled.'
}
foreach ($invalidFrom in @('2024-03', '2024-13', '02/2024', '1600-01')) {
    $invalidArguments = @('generate', '--spread-from', $invalidFrom, '--spread-to', '2024-02', '--dry-run', '--quiet')
    & $cli @invalidArguments 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 2) { throw ('Invalid timestamp period was accepted: {0}' -f $invalidFrom) }
}

Write-Output ('CLI smoke test passed: version {0}, deterministic render, parallel batch, {1} styles, text shaping, size modes, timestamp periods, and versioned settings.' -f $ExpectedVersion, $styleIds.Count)
Write-Output ('Test images: {0}' -f $runDirectory)
exit 0
