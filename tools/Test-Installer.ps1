param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$CliDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ExpectedVersion,
    [switch]$LaunchGui
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$appSource = (Resolve-Path -LiteralPath $AppDirectory).Path
$cliSource = (Resolve-Path -LiteralPath $CliDirectory).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoots = @((Join-Path $env:LOCALAPPDATA 'Pixelery/Tests'))
if ($env:RUNNER_TEMP) { $allowedRoots += Join-Path $env:RUNNER_TEMP 'Pixelery/Tests' }
if (-not ($allowedRoots | Where-Object { $root.StartsWith([IO.Path]::GetFullPath($_) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $root -eq [IO.Path]::GetFullPath($_) })) {
    throw 'Installer tests must stay in the Pixelery test directory.'
}
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    [xml]$props = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw
    $ExpectedVersion = [string]$props.Project.PropertyGroup.Version.InnerText
}
$registryPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Pixelery'
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
$existing = $registry.OpenSubKey($registryPath)
if ($existing) { $existing.Dispose(); $registry.Dispose(); throw 'An installed Pixelery already exists. Use a clean test account.' }
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Pixelery/Pixelery.lnk'
if (Test-Path -LiteralPath $shortcut) { $registry.Dispose(); throw 'An existing Pixelery shortcut must not be replaced by this test.' }
$run = Join-Path $root ([Guid]::NewGuid().ToString('N'))
$install = Join-Path $run 'Application Files'
$state = Join-Path $run 'State'
New-Item -ItemType Directory -Path $state -Force | Out-Null
$sentinel = Join-Path $state 'settings-preserved.txt'
Set-Content -LiteralPath $sentinel -Value 'Keep these user settings.'
$results = [Collections.Generic.List[string]]::new()
$installed = $false
$initialStateRoot = $env:PIXELERY_STATE_ROOT

function Install([Nullable[int]]$Cli, [int]$Language = 0) {
    $arguments = '/S '
    if ($null -ne $Cli) { $arguments += '/CLI={0} ' -f $Cli }
    if ($Language -gt 0) { $arguments += '/LANGUAGE={0} ' -f $Language }
    # NSIS requires /D last, without quotes, including when its value contains spaces.
    $arguments += '/D=' + $install
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { throw 'Installer did not finish within 60 seconds.' }
    if ($process.ExitCode -ne 0) { throw ('Installer failed with exit code {0}.' -f $process.ExitCode) }
    $script:installed = $true
}

function Assert-Payload([string]$Source, [string]$Component) {
    foreach ($file in (Get-ChildItem -LiteralPath $Source -Recurse -File | Where-Object Extension -ne '.pdb')) {
        $relative = [IO.Path]::GetRelativePath($Source, $file.FullName)
        $target = Join-Path (Join-Path $install $Component) $relative
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            throw ('Installed payload mismatch: {0}' -f $target)
        }
    }
    if (Get-ChildItem -LiteralPath $install -Filter '*.pdb' -Recurse -File) { throw 'Debug symbols were installed.' }
}

function Assert-Registration([int]$Cli) {
    $key = $registry.OpenSubKey($registryPath)
    if (-not $key) { throw 'Per-user uninstall registration missing.' }
    try {
        if ($key.GetValue('InstallLocation') -ne $install -or $key.GetValue('DisplayVersion') -ne $ExpectedVersion -or $key.GetValue('CliInstalled') -ne $Cli) {
            throw 'Unexpected per-user installation metadata.'
        }
    }
    finally { $key.Dispose() }
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut missing.' }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if ($link.TargetPath -ne (Join-Path $install 'app/Pixelery.exe')) { throw 'Start menu shortcut points to the wrong executable.' }
}

function Uninstall {
    $exe = Join-Path $install 'Uninstall.exe'
    $process = Start-Process -FilePath $exe -ArgumentList '/S' -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { throw 'Uninstaller did not finish within 60 seconds.' }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $key = $registry.OpenSubKey($registryPath)
        $registered = $null -ne $key
        if ($key) { $key.Dispose() }
        if (-not $registered -and -not (Test-Path -LiteralPath $exe)) { break }
        Start-Sleep -Milliseconds 200
    } while ($watch.Elapsed.TotalSeconds -lt 15)
    if ($registered -or (Test-Path -LiteralPath $exe)) { throw 'Uninstall did not finish removing its registration and executable.' }
    $script:installed = $false
}

try {
    $env:PIXELERY_STATE_ROOT = $state
    Install 0
    Assert-Payload $appSource 'app'
    Assert-Registration 0
    if (Test-Path -LiteralPath (Join-Path $install 'cli/Pixelery.Cli.exe')) { throw 'CLI was installed without selection.' }
    $results.Add('GUI-only installation and per-user shortcut/registration passed.')
    $custom = Join-Path $install 'app/user-file.txt'
    Set-Content -LiteralPath $custom -Value 'This file does not belong to the installer.'
    $obsolete = Join-Path $install 'app/obsolete-owned-file.txt'
    Set-Content -LiteralPath $obsolete -Value 'An obsolete file from a previous version.'
    Add-Content -LiteralPath (Join-Path $install 'files-app.lst') -Value 'app\obsolete-owned-file.txt' -Encoding Unicode

    Install 1
    Assert-Payload $appSource 'app'
    Assert-Payload $cliSource 'cli'
    Assert-Registration 1
    if (Test-Path -LiteralPath $obsolete) { throw 'Upgrade retained an obsolete owned file.' }
    if (-not (Test-Path -LiteralPath $custom)) { throw 'Upgrade removed an unowned file.' }
    $results.Add('Upgrade adds the optional CLI, removes obsolete payload, and preserves unowned files.')

    $previousState = $env:PIXELERY_STATE_ROOT
    try {
        $env:PIXELERY_STATE_ROOT = $state
        & (Join-Path $PSScriptRoot 'Test-CliSmoke.ps1') -CliPath (Join-Path $install 'cli/Pixelery.Cli.exe') -OutputDirectory (Join-Path $run 'CliSmoke') -ExpectedVersion $ExpectedVersion
        if ($LaunchGui) {
            $exe = Join-Path $install 'app/Pixelery.exe'
            $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
            try {
                $watch = [Diagnostics.Stopwatch]::StartNew()
                do { Start-Sleep -Milliseconds 200; $process.Refresh() } while (-not $process.HasExited -and $process.MainWindowHandle -eq 0 -and $watch.Elapsed.TotalSeconds -lt 20)
                if ($process.HasExited -or $process.MainWindowHandle -eq 0 -or -not $process.Responding) { throw 'Installed GUI did not open a responsive window.' }
                $results.Add('Installed GUI opened a responsive main window.')
            }
            finally {
                $process.Refresh()
                if (-not $process.HasExited) {
                    [void]$process.CloseMainWindow()
                    if (-not $process.WaitForExit(10000)) { throw 'Installed test GUI did not close normally.' }
                }
            }
        }
    }
    finally { $env:PIXELERY_STATE_ROOT = $previousState }
    Install $null
    Assert-Registration 1
    Assert-Payload $cliSource 'cli'
    $results.Add('Upgrade remembers the selected CLI component.')

    # Keep a process alive so the updater must wait before removing any payload.
    $startInfo = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    foreach ($argument in @('-NoProfile', '-Command', '[Console]::ReadLine() | Out-Null')) { $startInfo.ArgumentList.Add($argument) }
    $parent = [Diagnostics.Process]::Start($startInfo)
    try {
        $arguments = '/S /UPDATEPID={0} /D={1}' -f $parent.Id, $install
        $updater = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
        if ($updater.WaitForExit(1500)) { throw 'Updater did not wait for its parent process.' }
        if (-not (Test-Path -LiteralPath (Join-Path $install 'app/Pixelery.exe'))) { throw 'Updater removed files while its parent was running.' }
        $parent.StandardInput.WriteLine('exit')
        if (-not $parent.WaitForExit(10000)) { throw 'Update test parent did not exit.' }
        if (-not $updater.WaitForExit(60000) -or $updater.ExitCode -ne 0) { throw 'Update did not complete after the parent exited.' }
        Assert-Registration 1
        Assert-Payload $appSource 'app'
        Assert-Payload $cliSource 'cli'
        $results.Add('Update waits for the running parent and preserves the optional CLI selection.')
    }
    finally {
        if (-not $parent.HasExited) { $parent.StandardInput.WriteLine('exit'); [void]$parent.WaitForExit(10000) }
        $parent.Dispose()
    }

    Install 0
    Assert-Registration 0
    if (Test-Path -LiteralPath (Join-Path $install 'cli/Pixelery.Cli.exe')) { throw 'Deselected CLI remained installed.' }
    $results.Add('Upgrade removes the deselected CLI component.')

    $manifestPath = Join-Path $install 'files-app.lst'
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    try {
        [IO.File]::WriteAllText($manifestPath, "app\..\..\State\settings-preserved.txt`r`n", [Text.Encoding]::Unicode)
        $arguments = '/S _?=' + $install
        $process = Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList $arguments -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit(15000) -or $process.ExitCode -ne 1) { throw 'Uninstaller accepted an unsafe manifest path.' }
        if (-not (Test-Path -LiteralPath $sentinel) -or -not (Test-Path -LiteralPath (Join-Path $install 'app/Pixelery.exe'))) { throw 'Unsafe manifest changed files.' }
    }
    finally { [IO.File]::WriteAllBytes($manifestPath, $manifestBytes) }
    $results.Add('Uninstaller rejects path traversal in a modified payload manifest.')

    $languages = @{
        1033 = 'en'; 1031 = 'de'; 1036 = 'fr'; 1034 = 'es'; 1040 = 'it'; 1041 = 'ja'
        1042 = 'ko'; 2052 = 'zh-CN'; 1025 = 'ar'; 1055 = 'tr'; 1058 = 'uk'; 1037 = 'he'
    }
    foreach ($language in $languages.GetEnumerator()) {
        $env:PIXELERY_STATE_ROOT = Join-Path $run ('Languages/' + $language.Value)
        Install 0 $language.Key
        $preference = Join-Path $env:PIXELERY_STATE_ROOT 'language.state'
        if ((Get-Content -LiteralPath $preference -Raw).Trim() -ne $language.Value) { throw 'Installer did not initialize the selected language.' }
    }
    $preference = Join-Path $env:PIXELERY_STATE_ROOT 'language.state'
    $before = (Get-FileHash -LiteralPath $preference).Hash
    Install 0 1036
    if ((Get-FileHash -LiteralPath $preference).Hash -ne $before) { throw 'Installer overwrote an existing language preference.' }
    $results.Add('All 12 installer languages initialize new preferences; existing preferences remain unchanged.')
    Uninstall
    if ((Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath (Join-Path $install 'app/Pixelery.exe'))) { throw 'Uninstall retained a shortcut or application executable.' }
    if (-not (Test-Path -LiteralPath $custom) -or -not (Test-Path -LiteralPath $sentinel)) { throw 'Uninstall removed user files or settings.' }
    $results.Add('Uninstall removed the payload and registration, preserving user files and settings.')
}
finally {
    $results | Set-Content -LiteralPath (Join-Path $run 'results.txt')
    if ($installed -and (Test-Path -LiteralPath (Join-Path $install 'Uninstall.exe'))) { Uninstall }
    $env:PIXELERY_STATE_ROOT = $initialStateRoot
    $registry.Dispose()
}
$results
Write-Output ('Installer smoke test passed: {0}' -f $run)
