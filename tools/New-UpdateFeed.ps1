param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$KeyDirectory,
    [string]$PublicKeyPath = (Join-Path $PSScriptRoot '../Pixelery.Updates/UpdatePublicKey.txt')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a stable three-part version.' }
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$keys = (Resolve-Path -LiteralPath $KeyDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$expectedKey = (Get-Content -LiteralPath $PublicKeyPath -Raw).Trim()
$actualKey = (Get-Content -LiteralPath (Join-Path $keys 'NetSparkle_Ed25519.pub') -Raw).Trim()
if ($actualKey -ne $expectedKey) { throw 'The release signing key does not match the app public key.' }
if ([IO.Path]::GetFileName($installer) -ne ('Pixelery-Setup-{0}-x64.exe' -f $Version)) { throw 'Unexpected installer filename.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('tool', 'run', 'netsparkle-generate-appcast', '--single-file', $installer,
    '--file-version', $Version, '--os', 'windows-x64', '--product-name', 'Pixelery',
    '--base-url', ('https://github.com/tobitege/Pixelery/releases/download/v{0}' -f $Version),
    '--appcast-output-directory', $output, '--key-path', $keys, '--human-readable', 'true')
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Update feed generation failed.' }
$feed = Join-Path $output 'appcast.xml'
$signature = (Get-Content -LiteralPath ($feed + '.signature') -Raw).Trim()
$arguments = @('tool', 'run', 'netsparkle-generate-appcast', '--verify', $feed, '--signature', $signature, '--key-path', $keys)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Update feed signature verification failed.' }
[xml]$document = Get-Content -LiteralPath $feed -Raw
$enclosure = $document.rss.channel.item.enclosure
if ($enclosure.url -ne ('https://github.com/tobitege/Pixelery/releases/download/v{0}/Pixelery-Setup-{0}-x64.exe' -f $Version) -or
    [long]$enclosure.length -ne (Get-Item -LiteralPath $installer).Length -or $enclosure.os -ne 'windows-x64') {
    throw 'Generated update feed has unexpected metadata.'
}
$arguments = @('tool', 'run', 'netsparkle-generate-appcast', '--verify', $installer, '--signature', [string]$enclosure.signature, '--key-path', $keys)
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Installer signature verification failed.' }
