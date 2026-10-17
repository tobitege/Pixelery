param(
    [Parameter(Mandatory)][string]$OutputDirectory
)

# Run against a Debug app started with an isolated PIXELERY_STATE_ROOT below Pixelery/Tests.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$allowed = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Pixelery/Tests')) + [IO.Path]::DirectorySeparatorChar
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $root.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'UI test output must stay in the Pixelery test directory.'
}
$targets = Invoke-RestMethod -Uri 'http://127.0.0.1:9225/json/list' -TimeoutSec 5
$target = @($targets | Where-Object title -EQ 'Pixelery')[0]
$socket = [Net.WebSockets.ClientWebSocket]::new()
$script:requestId = 0
$originalFolder = $null
$originalCulture = $null
$settingsType = 'Window.GetType().Assembly.GetType("Pixelery.Services.AppSettings")'
$localizationType = 'Window.GetType().Assembly.GetType("Pixelery.Localization.AppLocalization")'

function Invoke-Cdp([string]$Method, [hashtable]$Parameters) {
    $script:requestId++
    $request = @{id = $script:requestId; method = $Method; params = $Parameters}
    $bytes = [Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Depth 8 -Compress))
    $timeout = [Threading.CancellationTokenSource]::new(20000)
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
        if ($response.ContainsKey('error')) { throw ($response.error | ConvertTo-Json -Compress) }
        return $response.result
    }
    finally { $timeout.Dispose() }
}

function Evaluate([string]$Expression) {
    $result = Invoke-Cdp 'Runtime.evaluate' @{expression = $Expression; returnByValue = $true}
    if ($result.ContainsKey('exceptionDetails')) { throw ($result.exceptionDetails | ConvertTo-Json -Compress) }
    $value = $result.result.value
    if ($value -is [string] -and $value.StartsWith('Error:')) { throw $value }
    return $value
}

function Literal([string]$Value) { return '@"' + $Value.Replace('"', '""') + '"' }

function Wait-True([string]$Expression) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        if ((Evaluate $Expression) -eq 'True') { return }
        Start-Sleep -Milliseconds 100
    } while ($watch.Elapsed.TotalSeconds -lt 10)
    throw ('UI condition timed out: {0}' -f $Expression)
}

function Click([string]$Id) {
    $button = '(FrameworkElement)Query("[AutomationId=' + $Id + ']")'
    Wait-True ('var b=' + $button + '; b != null && b.Visibility == Visibility.Visible && ((Control)b).IsEnabled')
    $null = Evaluate ('var b=' + $button + '; var p=Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(b); ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)p.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();')
}

function Wait-Section([string]$Key) {
    Wait-True ('var s=(ScrollViewer)Query("[AutomationId=ConfigScroller]"); var t=(FrameworkElement)Query("[AutomationId=' + $Key + ']"); var y=t.TransformToVisual(s).TransformPoint(new Windows.Foundation.Point(0,0)).Y; s.VerticalOffset > 0 && y >= -1 && y < s.ViewportHeight / 2')
}

function Screenshot([string]$Name) {
    $result = Invoke-Cdp 'Page.captureScreenshot' @{format = 'png'}
    [IO.File]::WriteAllBytes((Join-Path $root $Name), [Convert]::FromBase64String($result.data))
}

try {
    $timeout = [Threading.CancellationTokenSource]::new(10000)
    try { $null = $socket.ConnectAsync([uri]$target.webSocketDebuggerUrl, $timeout.Token).GetAwaiter().GetResult() }
    finally { $timeout.Dispose() }
    $state = Evaluate ($settingsType + '.GetProperty("StateDirectory").GetValue(null)')
    if (-not ([IO.Path]::GetFullPath($state)).StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Start the Debug app with an isolated PIXELERY_STATE_ROOT below Pixelery/Tests.'
    }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    Wait-True 'Query("[AutomationId=Navigate_Section_Output]") != null'
    $originalFolder = Evaluate 'Query("[AutomationId=OutputFolderInput]").GetType().GetProperty("Text").GetValue(Query("[AutomationId=OutputFolderInput]"))'
    $originalCulture = Evaluate ($localizationType + '.GetProperty("CurrentCultureName").GetValue(null)')
    $homePosition = Evaluate '((FrameworkElement)Query("[AutomationId=Navigate_Home]")).TransformToVisual(Window.Content).TransformPoint(new Windows.Foundation.Point(0,0)).Y'
    foreach ($section in @('Styles', 'Colors', 'Dimensions', 'Watermark', 'Output')) {
        Click ('Navigate_Section_' + $section)
        Wait-Section ('Section_' + $section)
    }
    Wait-True '((Panel)((FrameworkElement)Query("[AutomationId=Navigate_Home]")).Parent).Children.Count == 6'
    Wait-True ('((FrameworkElement)Query("[AutomationId=Navigate_Home]")).TransformToVisual(Window.Content).TransformPoint(new Windows.Foundation.Point(0,0)).Y == ' + $homePosition)
    Click 'Navigate_Home'
    Wait-True '((ScrollViewer)Query("[AutomationId=ConfigScroller]")).VerticalOffset == 0'
    Screenshot 'home.png'
    Click 'Navigate_OutputSettings'
    Wait-Section 'Section_Output'
    Wait-True 'var link=(HyperlinkButton)Query("[AutomationId=FooterGitHubLink]"); var icon=(Microsoft.UI.Xaml.Shapes.Path)((Viewbox)((Panel)link.Content).Children[0]).Child; icon.Data != null && icon.ActualWidth > 0'

    $folder = Join-Path $state 'Generated/A long output folder name that must wrap inside the Generate sidebar'
    $null = Evaluate ('var input=Query("[AutomationId=OutputFolderInput]"); input.GetType().GetProperty("Text").SetValue(input, ' + (Literal $folder) + ');')
    Wait-True ('var label=(TextBlock)Query("[AutomationId=OutputFolderSummary]"); label.Text == ' + (Literal $folder) + ' && label.TextWrapping == TextWrapping.Wrap && label.IsTextSelectionEnabled && label.ActualWidth <= ((FrameworkElement)label.Parent).ActualWidth')
    Screenshot 'output-long-path.png'

    $languages = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Pixelery/Localization') -Filter '*.json' -File)
    if ($languages.Count -ne 12) { throw 'Expected exactly 12 application languages.' }
    foreach ($language in $languages) {
        $strings = Get-Content -LiteralPath $language.FullName -Raw | ConvertFrom-Json -AsHashtable
        $null = Evaluate ($localizationType + '.GetMethod("SetCulture").Invoke(null, new object[] {' + (Literal $language.BaseName) + '});')
        Wait-True ('Microsoft.UI.Xaml.Automation.AutomationProperties.GetName((FrameworkElement)Query("[AutomationId=Navigate_Home]")) == ' + (Literal $strings.Navigation_Home))
        Wait-True ('Microsoft.UI.Xaml.Automation.AutomationProperties.GetName((FrameworkElement)Query("[AutomationId=Navigate_OutputSettings]")) == ' + (Literal $strings.Navigation_OutputSettings))
        Wait-True ('Microsoft.UI.Xaml.Automation.AutomationProperties.GetName((FrameworkElement)Query("[AutomationId=Navigate_Section_Colors]")) == ' + (Literal $strings.Section_Colors))
    }
    $message = 'Passed: five section jumps, fixed navigation, Home, output shortcut, footer icon, live wrapping folder display, and all 12 languages.'
    Set-Content -LiteralPath (Join-Path $root 'results.txt') -Value $message
    Write-Output $message
}
finally {
    try {
        if ($null -ne $originalFolder) {
            $null = Evaluate ('var input=Query("[AutomationId=OutputFolderInput]"); input.GetType().GetProperty("Text").SetValue(input, ' + (Literal $originalFolder) + ');')
        }
        if ($null -ne $originalCulture) {
            $null = Evaluate ($localizationType + '.GetMethod("SetCulture").Invoke(null, new object[] {' + (Literal $originalCulture) + '});')
        }
        if ($null -ne $originalFolder) { Click 'Navigate_Home' }
    }
    finally { $socket.Dispose() }
}
