param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$CliDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ExpectedVersion,
    [switch]$LaunchGui,
    [int]$WindowX = 20,
    [int]$WindowY = 20
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
Set-Content -LiteralPath (Join-Path $state 'window.state') -Value @($WindowX, $WindowY, 1400, 900)
$sentinel = Join-Path $state 'settings-preserved.txt'
Set-Content -LiteralPath $sentinel -Value 'Keep these user settings.'
$results = [Collections.Generic.List[string]]::new()
$installed = $false
$initialStateRoot = $env:PIXELERY_STATE_ROOT

# Read and dismiss only the error dialog belonging to the installer under test.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class InstallerDialog {
    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDlgItemText(IntPtr window, int id, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    public static IntPtr FindError(int process) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != process || !IsWindowVisible(window)) return true;
            var text = new StringBuilder(512);
            GetDlgItemText(window, 65535, text, text.Capacity);
            if (!text.ToString().Contains("Cannot update or remove the installed files.")) return true;
            result = window;
            return false;
        }, IntPtr.Zero);
        return result;
    }
    public static void Dismiss(IntPtr window, int x, int y) {
        SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, 0x15); // No resize, activation or Z-order change.
        PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE, same as the dialog's Close button.
    }
}
'@

function Install([int]$Language = 0) {
    $arguments = '/S '
    if ($Language -gt 0) { $arguments += '/LANGUAGE={0} ' -f $Language }
    # NSIS requires /D last, without quotes, including when its value contains spaces.
    $arguments += '/D=' + $install
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { throw 'Installer did not finish within 60 seconds.' }
    if ($process.ExitCode -ne 0) { throw ('Installer failed with exit code {0}.' -f $process.ExitCode) }
    $script:installed = $true
}

function Assert-Payload([string]$Source) {
    foreach ($file in (Get-ChildItem -LiteralPath $Source -Recurse -File | Where-Object Extension -ne '.pdb')) {
        $relative = [IO.Path]::GetRelativePath($Source, $file.FullName)
        $target = Join-Path $install $relative
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            throw ('Installed payload mismatch: {0}' -f $target)
        }
    }
    if (Get-ChildItem -LiteralPath $install -Filter '*.pdb' -Recurse -File) { throw 'Debug symbols were installed.' }
}

function Assert-Registration {
    $key = $registry.OpenSubKey($registryPath)
    if (-not $key) { throw 'Per-user uninstall registration missing.' }
    try {
        if ($key.GetValue('InstallLocation') -ne $install -or $key.GetValue('DisplayVersion') -ne $ExpectedVersion -or $null -ne $key.GetValue('CliInstalled')) {
            throw 'Unexpected per-user installation metadata.'
        }
    }
    finally { $key.Dispose() }
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut missing.' }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if ($link.TargetPath -ne (Join-Path $install 'Pixelery.exe')) { throw 'Start menu shortcut points to the wrong executable.' }
}

function Get-InstalledHashes {
    $hashes = @{}
    foreach ($file in Get-ChildItem -LiteralPath $install -Recurse -File) {
        $hashes[[IO.Path]::GetRelativePath($install, $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName).Hash
    }
    return $hashes
}

function Assert-FailedUpdatePreservedFiles([hashtable]$Before) {
    $arguments = '/S /D=' + $install
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000) -or $process.ExitCode -ne 1) { throw 'Blocked update did not fail with exit code 1.' }
    Assert-InstalledHashes $Before
}

function Assert-InstalledHashes([hashtable]$Before) {
    $after = Get-InstalledHashes
    if ($after.Count -ne $Before.Count) { throw 'Failed update changed the installed file set.' }
    foreach ($entry in $Before.GetEnumerator()) {
        if ($after[$entry.Key] -ne $entry.Value) { throw ('Failed update changed: {0}' -f $entry.Key) }
    }
    Assert-Registration
}

function Start-UpdateParent {
    $startInfo = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    foreach ($argument in @('-NoProfile', '-Command', '[Console]::ReadLine() | Out-Null')) { $startInfo.ArgumentList.Add($argument) }
    return [Diagnostics.Process]::Start($startInfo)
}

function Assert-RelaunchedApplication([int]$InstallerId) {
    $filter = 'ParentProcessId = {0} AND Name = ''Pixelery.exe''' -f $InstallerId
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $app = $null
    try {
        do {
            $child = Get-CimInstance -ClassName Win32_Process -Filter $filter
            if ($child -and $child.ExecutablePath -eq (Join-Path $install 'Pixelery.exe')) {
                $app = Get-Process -Id $child.ProcessId
                break
            }
            Start-Sleep -Milliseconds 100
        } while ($watch.Elapsed.TotalSeconds -lt 20)
        if (-not $app) { throw 'Update did not relaunch the installed application.' }
        do { Start-Sleep -Milliseconds 100; $app.Refresh() } while (-not $app.HasExited -and $app.MainWindowHandle -eq 0 -and $watch.Elapsed.TotalSeconds -lt 30)
        if ($app.HasExited -or $app.MainWindowHandle -eq 0 -or -not $app.Responding) { throw 'Relaunched application did not open a responsive window.' }
    }
    finally {
        if ($app) {
            $app.Refresh()
            if (-not $app.HasExited) {
                [void]$app.CloseMainWindow()
                if (-not $app.WaitForExit(10000)) { throw 'Relaunched test application did not close normally.' }
            }
            $app.Dispose()
        }
    }
}

function Stage-PreviousLayout([bool]$WithCli) {
    # Reproduce the former manifests and folder layout without retaining an old installer in CI.
    foreach ($component in @(@($appSource, 'app'), @($cliSource, 'cli'))) {
        if ($component[1] -eq 'cli' -and -not $WithCli) { continue }
        $manifest = [Collections.Generic.List[string]]::new()
        foreach ($file in (Get-ChildItem -LiteralPath $component[0] -Recurse -File | Where-Object Extension -ne '.pdb')) {
            $relative = Join-Path $component[1] ([IO.Path]::GetRelativePath($component[0], $file.FullName))
            $destination = Join-Path $install $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
            $manifest.Add($relative)
        }
        [IO.File]::WriteAllLines((Join-Path $install ('files-{0}.lst' -f $component[1])), $manifest, [Text.Encoding]::Unicode)
    }
    $key = $registry.CreateSubKey($registryPath)
    try {
        $key.SetValue('InstallLocation', $install)
        $key.SetValue('CliInstalled', [int]$WithCli, [Microsoft.Win32.RegistryValueKind]::DWord)
    }
    finally { $key.Dispose() }
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
    Install
    Assert-Payload $appSource
    Assert-Payload $cliSource
    Assert-Registration
    if ((Test-Path -LiteralPath (Join-Path $install 'app')) -or (Test-Path -LiteralPath (Join-Path $install 'cli'))) { throw 'Installation created component subdirectories.' }
    $entries = [IO.File]::ReadAllLines((Join-Path $install 'files.lst'))
    if (@($entries | Sort-Object -Unique).Count -ne $entries.Count) { throw 'Shared payload files were recorded more than once.' }
    $results.Add('GUI and CLI share the installation root, with a single manifest and per-user shortcut/registration.')
    $custom = Join-Path $install 'user-file.txt'
    Set-Content -LiteralPath $custom -Value 'This file does not belong to the installer.'
    $obsolete = Join-Path $install 'obsolete-owned-file.txt'
    Set-Content -LiteralPath $obsolete -Value 'An obsolete file from a previous version.'
    Add-Content -LiteralPath (Join-Path $install 'files.lst') -Value 'obsolete-owned-file.txt' -Encoding Unicode

    Install
    Assert-Payload $appSource
    Assert-Payload $cliSource
    Assert-Registration
    if (Test-Path -LiteralPath $obsolete) { throw 'Upgrade retained an obsolete owned file.' }
    if (-not (Test-Path -LiteralPath $custom)) { throw 'Upgrade removed an unowned file.' }
    $results.Add('Upgrade installs both applications, removes obsolete payload, and preserves unowned files.')

    # A recorded relative path must not traverse a junction into unrelated data.
    $external = Join-Path $run 'ExternalFiles'
    $linked = Join-Path $install 'linked-data'
    $retiredLink = Join-Path $run 'retired-link'
    foreach ($path in @($external, $linked, $retiredLink)) {
        if (-not ([IO.Path]::GetFullPath($path)).StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Link fixture escaped the test run.' }
        if (Test-Path -LiteralPath $path) { throw 'Link fixture already exists.' }
    }
    New-Item -ItemType Directory -Path (Join-Path $external 'nested') -Force | Out-Null
    $externalFile = Join-Path $external 'nested/preserve.txt'
    Set-Content -LiteralPath $externalFile -Value 'This data is outside the installation.'
    $externalHash = (Get-FileHash -LiteralPath $externalFile).Hash
    New-Item -ItemType Junction -Path $linked -Target $external | Out-Null
    $manifestPath = Join-Path $install 'files.lst'
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    try {
        $entries = @('linked-data\nested\preserve.txt') + [IO.File]::ReadAllLines($manifestPath)
        [IO.File]::WriteAllLines($manifestPath, $entries, [Text.Encoding]::Unicode)
        $before = Get-InstalledHashes
        $commands = @(@($installer, ('/S /D=' + $install)), @((Join-Path $install 'Uninstall.exe'), ('/S _?=' + $install)))
        foreach ($command in $commands) {
            $process = Start-Process -FilePath $command[0] -ArgumentList $command[1] -PassThru -WindowStyle Hidden
            try {
                if (-not $process.WaitForExit(60000) -or $process.ExitCode -ne 1) { throw 'Setup/removal accepted a manifest path through a junction.' }
            }
            finally { $process.Dispose() }
            if (-not (Test-Path -LiteralPath $externalFile) -or (Get-FileHash -LiteralPath $externalFile).Hash -ne $externalHash) { throw 'Setup/removal changed data outside the installation.' }
            if ((Get-Item -LiteralPath $linked).LinkType -ne 'Junction') { throw 'Setup/removal changed the junction.' }
            Assert-InstalledHashes $before
        }
    }
    finally {
        [IO.File]::WriteAllBytes($manifestPath, $manifestBytes)
        if (Test-Path -LiteralPath $linked) { Move-Item -LiteralPath $linked -Destination $retiredLink }
    }
    $results.Add('Setup and removal reject junction traversal, preserving external data, the link, and the installed payload.')

    # A late lock must restore the earlier renamed files, including their original bytes.
    $oldAsset = Join-Path $install 'Assets/app_icon.png'
    Add-Content -LiteralPath $oldAsset -Value 'previous installed version'
    $before = Get-InstalledHashes
    $locked = [IO.File]::Open((Join-Path $install 'Pixelery.Cli.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try { Assert-FailedUpdatePreservedFiles $before }
    finally { $locked.Dispose() }
    Copy-Item -LiteralPath (Join-Path $appSource 'Assets/app_icon.png') -Destination $oldAsset -Force
    $results.Add('A locked CLI aborts the update and restores every installed file and manifest byte-for-byte.')

    # This unowned file blocks a late destination directory, after payload publication has started.
    $culture = Join-Path $install 'zh-CN'
    $savedCulture = Join-Path $run 'saved-zh-CN'
    Move-Item -LiteralPath $culture -Destination $savedCulture
    Set-Content -LiteralPath $culture -Value 'Preserve the unowned destination blocker.'
    Add-Content -LiteralPath $oldAsset -Value 'previous installed version'
    try {
        $before = Get-InstalledHashes
        Assert-FailedUpdatePreservedFiles $before
    }
    finally {
        Move-Item -LiteralPath $culture -Destination (Join-Path $run 'destination-blocker.txt')
        Move-Item -LiteralPath $savedCulture -Destination $culture
        Copy-Item -LiteralPath (Join-Path $appSource 'Assets/app_icon.png') -Destination $oldAsset -Force
    }
    Assert-Payload $appSource
    Assert-Payload $cliSource
    $results.Add('A late publication failure rolls back the new payload while preserving old files and unowned blockers.')

    $previousState = $env:PIXELERY_STATE_ROOT
    try {
        $env:PIXELERY_STATE_ROOT = $state
        & (Join-Path $PSScriptRoot 'Test-CliSmoke.ps1') -CliPath (Join-Path $install 'Pixelery.Cli.exe') -OutputDirectory (Join-Path $run 'CliSmoke') -ExpectedVersion $ExpectedVersion
        if ($LaunchGui) {
            $exe = Join-Path $install 'Pixelery.exe'
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
    foreach ($withCli in @($false, $true)) {
        Uninstall
        Stage-PreviousLayout $withCli
        Install
        Assert-Registration
        Assert-Payload $appSource
        Assert-Payload $cliSource
        if ((Test-Path -LiteralPath (Join-Path $install 'app')) -or (Test-Path -LiteralPath (Join-Path $install 'cli'))) { throw 'Upgrade retained old component payload.' }
    }
    $results.Add('Upgrades from the former GUI-only and GUI/CLI layouts install both executables at the root.')

    # The first real setup owns the lock while waiting for its parent. Competing
    # setup and uninstall processes must fail before changing any installed bytes.
    $parent = Start-UpdateParent
    $updater = $null
    $setupMutex = $null
    try {
        $arguments = '/S /UPDATEPID={0} /D={1}' -f $parent.Id, $install
        $updater = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $watch = [Diagnostics.Stopwatch]::StartNew()
        do {
            if ([Threading.Mutex]::TryOpenExisting('Global\Pixelery.Setup', [ref]$setupMutex)) { break }
            Start-Sleep -Milliseconds 100
        } while (-not $updater.HasExited -and $watch.Elapsed.TotalSeconds -lt 10)
        if (-not $setupMutex) { throw 'Setup did not acquire its shared installation mutex.' }
        if ($setupMutex.WaitOne(0)) {
            $setupMutex.ReleaseMutex()
            throw 'Setup created its mutex without owning it.'
        }
        $before = Get-InstalledHashes
        $commands = @(@($installer, ('/S /D=' + $install)), @((Join-Path $install 'Uninstall.exe'), ('/S _?=' + $install)))
        foreach ($command in $commands) {
            $contender = Start-Process -FilePath $command[0] -ArgumentList $command[1] -PassThru -WindowStyle Hidden
            try {
                if (-not $contender.WaitForExit(15000) -or $contender.ExitCode -ne 1618) { throw 'Concurrent setup/removal was not rejected with exit code 1618.' }
            }
            finally { $contender.Dispose() }
            Assert-InstalledHashes $before
        }
        $parent.StandardInput.WriteLine('exit')
        if (-not $parent.WaitForExit(10000)) { throw 'Update test parent did not exit.' }
        if (-not $updater.WaitForExit(60000) -or $updater.ExitCode -ne 0) { throw 'Update did not complete after the parent exited.' }
        Assert-Registration
        Assert-Payload $appSource
        Assert-Payload $cliSource
        $results.Add('A real setup excludes competing setup and uninstall processes, preserves every byte, then installs after its parent exits.')
    }
    finally {
        if (-not $parent.HasExited) { $parent.StandardInput.WriteLine('exit'); [void]$parent.WaitForExit(10000) }
        if ($updater) {
            if (-not $updater.WaitForExit(60000)) { throw 'Concurrent installer test did not finish.' }
            $updater.Dispose()
        }
        if ($setupMutex) { $setupMutex.Dispose() }
        $parent.Dispose()
    }

    # Exercise the actual automatic-update switches, including failure feedback
    # and restart of the restored Native AOT app, not just a direct /S install.
    foreach ($blockUpdate in @($true, $false)) {
        $parent = Start-UpdateParent
        $updater = $null
        $locked = $null
        $dialog = [IntPtr]::Zero
        try {
            if ($blockUpdate) {
                Add-Content -LiteralPath $oldAsset -Value 'previous installed version'
                $locked = [IO.File]::Open((Join-Path $install 'Pixelery.Cli.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            }
            $before = Get-InstalledHashes
            $arguments = '/S /LANGUAGE=1033 /UPDATEPID={0} /RELAUNCH=1 /D={1}' -f $parent.Id, $install
            $updater = Start-Process -FilePath $installer -ArgumentList $arguments -PassThru -WindowStyle Hidden
            $parent.StandardInput.WriteLine('exit')
            if (-not $parent.WaitForExit(10000)) { throw 'Automatic-update test parent did not exit.' }
            if ($blockUpdate) {
                $watch = [Diagnostics.Stopwatch]::StartNew()
                do {
                    $dialog = [InstallerDialog]::FindError($updater.Id)
                    if ($dialog -ne [IntPtr]::Zero) { break }
                    Start-Sleep -Milliseconds 100
                } while (-not $updater.HasExited -and $watch.Elapsed.TotalSeconds -lt 60)
                if ($dialog -eq [IntPtr]::Zero) { throw 'Silent automatic-update failure did not display its error.' }
                Assert-InstalledHashes $before
                [InstallerDialog]::Dismiss($dialog, $WindowX, $WindowY)
                $dialog = [IntPtr]::Zero
            }
            $expectedExit = if ($blockUpdate) { 1 } else { 0 }
            if (-not $updater.WaitForExit(60000) -or $updater.ExitCode -ne $expectedExit) { throw 'Automatic update returned an unexpected exit status.' }
            Assert-RelaunchedApplication $updater.Id
            if ($blockUpdate) { Assert-InstalledHashes $before }
            else { Assert-Payload $appSource; Assert-Payload $cliSource }
        }
        finally {
            if ($locked) { $locked.Dispose() }
            if ($updater -and -not $updater.HasExited) {
                $dialog = [InstallerDialog]::FindError($updater.Id)
                if ($dialog -ne [IntPtr]::Zero) { [InstallerDialog]::Dismiss($dialog, $WindowX, $WindowY) }
                if (-not $updater.WaitForExit(10000)) { throw 'Automatic-update test installer did not exit.' }
            }
            if (-not $parent.HasExited) { $parent.StandardInput.WriteLine('exit'); [void]$parent.WaitForExit(10000) }
            $parent.Dispose()
            if ($updater) { $updater.Dispose() }
        }
    }
    $results.Add('Automatic update displays failures, restores original bytes, and restarts the previous app; successful update restarts the new app. Both windows close normally.')

    $manifestPath = Join-Path $install 'files.lst'
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    try {
        [IO.File]::WriteAllText($manifestPath, "..\State\settings-preserved.txt`r`n", [Text.Encoding]::Unicode)
        $arguments = '/S _?=' + $install
        $process = Start-Process -FilePath (Join-Path $install 'Uninstall.exe') -ArgumentList $arguments -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit(15000) -or $process.ExitCode -ne 1) { throw 'Uninstaller accepted an unsafe manifest path.' }
        if (-not (Test-Path -LiteralPath $sentinel) -or -not (Test-Path -LiteralPath (Join-Path $install 'Pixelery.exe'))) { throw 'Unsafe manifest changed files.' }
    }
    finally { [IO.File]::WriteAllBytes($manifestPath, $manifestBytes) }
    $results.Add('Uninstaller rejects path traversal in a modified payload manifest.')

    $languages = @{
        1033 = 'en'; 1031 = 'de'; 1036 = 'fr'; 1034 = 'es'; 1040 = 'it'; 1041 = 'ja'
        1042 = 'ko'; 2052 = 'zh-CN'; 1025 = 'ar'; 1055 = 'tr'; 1058 = 'uk'; 1037 = 'he'
    }
    foreach ($language in $languages.GetEnumerator()) {
        $env:PIXELERY_STATE_ROOT = Join-Path $run ('Languages/' + $language.Value)
        Install $language.Key
        $preference = Join-Path $env:PIXELERY_STATE_ROOT 'language.state'
        if ((Get-Content -LiteralPath $preference -Raw).Trim() -ne $language.Value) { throw 'Installer did not initialize the selected language.' }
    }
    $preference = Join-Path $env:PIXELERY_STATE_ROOT 'language.state'
    $before = (Get-FileHash -LiteralPath $preference).Hash
    Install 1036
    if ((Get-FileHash -LiteralPath $preference).Hash -ne $before) { throw 'Installer overwrote an existing language preference.' }
    $results.Add('All 12 installer languages initialize new preferences; existing preferences remain unchanged.')
    Uninstall
    if ((Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath (Join-Path $install 'Pixelery.exe')) -or (Test-Path -LiteralPath (Join-Path $install 'Pixelery.Cli.exe'))) { throw 'Uninstall retained a shortcut or application executable.' }
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
