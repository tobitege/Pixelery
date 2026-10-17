param(
    [Parameter(Mandatory)][string]$AppPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$WindowX = 20,
    [int]$WindowY = 20
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$exe = (Resolve-Path -LiteralPath $AppPath).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
$allowed = @((Join-Path $env:LOCALAPPDATA 'Pixelery/Tests'))
if ($env:RUNNER_TEMP) { $allowed += Join-Path $env:RUNNER_TEMP 'Pixelery/Tests' }
if (-not ($allowed | Where-Object { $root.StartsWith([IO.Path]::GetFullPath($_) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })) {
    throw 'UI tests must stay in the Pixelery test directory.'
}
$listener = Get-NetTCPConnection -LocalPort 9225 -State Listen -ErrorAction SilentlyContinue
if ($listener) { throw 'Port 9225 is already owned by another process.' }
$state = Join-Path $root ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $state -Force | Out-Null
Set-Content -LiteralPath (Join-Path $state 'window.state') -Value @($WindowX, $WindowY, 1400, 900)
$oldState = $env:PIXELERY_STATE_ROOT
$socket = [Net.WebSockets.ClientWebSocket]::new()
$script:requestId = 0
$process = $null

function Evaluate([string]$Expression) {
    $script:requestId++
    $request = @{ id = $script:requestId; method = 'Runtime.evaluate'; params = @{ expression = $Expression; returnByValue = $true } }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Depth 5 -Compress))
    $timeout = [Threading.CancellationTokenSource]::new(30000)
    try {
        $null = $socket.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult()
        do {
            $message = [IO.MemoryStream]::new()
            try {
                do {
                    $buffer = [byte[]]::new(65536)
                    $frame = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
                    if ($frame.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) { throw 'CDP disconnected.' }
                    $message.Write($buffer, 0, $frame.Count)
                } while (-not $frame.EndOfMessage)
                $response = [Text.Encoding]::UTF8.GetString($message.ToArray()) | ConvertFrom-Json -AsHashtable
            }
            finally { $message.Dispose() }
        } while ($response['id'] -ne $script:requestId)
        if ($response.ContainsKey('error')) { throw ($response['error'] | ConvertTo-Json -Compress) }
        if ($response.result.ContainsKey('exceptionDetails')) { throw ($response.result['exceptionDetails'] | ConvertTo-Json -Compress) }
        $value = $response.result.result.value
        if ($value -is [string] -and $value.StartsWith('Error:')) { throw ('{0} Expression: {1}' -f $value, $Expression) }
        return $value
    }
    finally { $timeout.Dispose() }
}

function Wait-Expression([string]$Expression) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        if ((Evaluate $Expression) -eq 'True') { return }
        if ($process.HasExited) { throw 'Test application exited.' }
        Start-Sleep -Milliseconds 100
    } while ($watch.Elapsed.TotalSeconds -lt 30)
    throw ('UI condition timed out: {0}' -f $Expression)
}

try {
    $env:PIXELERY_STATE_ROOT = $state
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $target = $null
    do {
        try { $target = @(Invoke-RestMethod -Uri 'http://127.0.0.1:9225/json/list' -TimeoutSec 1)[0] } catch { }
        if ($process.HasExited) { throw 'Debug app exited before CDP discovery.' }
        if (-not $target) { Start-Sleep -Milliseconds 100 }
    } while (-not $target -and $watch.Elapsed.TotalSeconds -lt 30)
    if (-not $target) { throw 'Debug CDP endpoint did not start.' }
    $timeout = [Threading.CancellationTokenSource]::new(10000)
    try { $null = $socket.ConnectAsync([uri]$target.webSocketDebuggerUrl, $timeout.Token).GetAwaiter().GetResult() }
    finally { $timeout.Dispose() }
    Wait-Expression 'Query("[AutomationId=UsePaletteSet]") != null && Query("[AutomationId=Style_mesh-gradient]") != null'
    $image = 'var card = Query("[AutomationId=Style_mesh-gradient]"); var image = (Image)card.GetType().GetField("_image", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(card); '
    $toggle = 'var toggle = (FrameworkElement)Query("[AutomationId=UsePaletteSet]"); var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(toggle); ((Microsoft.UI.Xaml.Automation.Provider.IToggleProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Toggle)).Toggle(); '
    $appSettings = 'var settings = Window.GetType().Assembly.GetType("Pixelery.Services.AppSettings"); '
    $null = Evaluate ($appSettings + 'settings.GetProperty("PreviewsFollowColors").SetValue(null, true);')
    foreach ($enabled in @('True', 'False')) {
        $null = Evaluate ($image + 'image.Tag = image.Source;')
        $null = Evaluate $toggle
        Wait-Expression ($image + '!Object.ReferenceEquals(image.Tag, image.Source)')
        $actual = Evaluate '((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)Query("[AutomationId=UsePaletteSet]")).IsChecked'
        if ($actual -ne $enabled) { throw ('Expected palette mode {0}, got {1}' -f $enabled, $actual) }
    }
    $null = Evaluate ($appSettings + 'settings.GetProperty("PreviewsFollowColors").SetValue(null, false);')
    # Drain any in-flight render before checking that the next click schedules none.
    Start-Sleep -Seconds 1
    $null = Evaluate ($image + 'image.Tag = image.Source;')
    $null = Evaluate $toggle
    Start-Sleep -Seconds 1
    if ((Evaluate ($image + 'Object.ReferenceEquals(image.Tag, image.Source)')) -ne 'True') { throw 'Palette mode ignored the previews-follow-colors setting.' }
    $result = 'Palette mode on/off both replace the real thumbnail; disabled color following preserves it.'
    Set-Content -LiteralPath (Join-Path $state 'results.txt') -Value $result
    Write-Output $result
}
finally {
    $socket.Dispose()
    if ($process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(10000)) { throw 'Test app did not close normally.' }
        }
        $process.Dispose()
    }
    $env:PIXELERY_STATE_ROOT = $oldState
}
