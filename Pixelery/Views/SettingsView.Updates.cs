using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Pixelery.Localization;
using Pixelery.Services;
using Pixelery.Updates;

namespace Pixelery.Views;

public sealed partial class SettingsView
{
    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly UpdateClient _updates = new(UpdateHttp, UpdateClient.PublicKey);
    private UpdateRelease? _release;
    private UpdateDownload? _installer;
    private CancellationTokenSource? _updateOperation;
    private string _updateStatusKey = "Update_Idle";

    public void CancelUpdateOperation()
    {
        _updateOperation?.Cancel();
        if (_installer != null)
            _updateStatusKey = "Update_Cancelled";
        _installer?.Dispose();
        _installer = null;
    }

    public void RefreshUpdateControls()
    {
        var busy = _updateOperation != null;
        UpdateStatus.Text = _updateStatusKey == "Update_Available"
            ? AppLocalization.Format(_updateStatusKey, _release?.Version.ToString() ?? "")
            : AppLocalization.GetString(_updateStatusKey);
        UpdateCheckButton.IsEnabled = !busy;
        UpdateInstallButton.Visibility = _release != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateInstallButton.IsEnabled = !busy && _shell?.CanInstallUpdate == true;
        UpdateInstallButton.Content = AppLocalization.GetString(_installer == null ? "Update_Download" : "Update_Install");
        UpdateCancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.IsIndeterminate = _updateStatusKey == "Update_Checking";
    }

    private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_updateOperation != null)
            return;
        using var operation = new CancellationTokenSource();
        _updateOperation = operation;
        _release = null;
        _installer?.Dispose();
        _installer = null;
        _updateStatusKey = "Update_Checking";
        RefreshUpdateControls();
        try
        {
            var version = Version.Parse(Core.AppInfo.Version(typeof(SettingsView).Assembly));
            _release = await _updates.CheckAsync(version, operation.Token);
            _updateStatusKey = _release == null ? "Update_Current" : "Update_Available";
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            _updateStatusKey = "Update_Cancelled";
        }
        catch (Exception)
        {
            _updateStatusKey = "Update_Error";
        }
        finally
        {
            _updateOperation = null;
            RefreshUpdateControls();
        }
    }

    private async void UpdateInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_updateOperation != null || _release is not { } release || _shell?.CanInstallUpdate != true)
            return;
        using var operation = new CancellationTokenSource();
        _updateOperation = operation;
        try
        {
            if (_installer == null)
            {
                _updateStatusKey = "Update_Downloading";
                UpdateProgress.Value = 0;
                RefreshUpdateControls();
                var progress = new Progress<double>(value =>
                {
                    if (ReferenceEquals(_updateOperation, operation))
                        UpdateProgress.Value = value;
                });
                _installer = await _updates.DownloadAsync(release, AppSettings.StateDirectory, progress, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                _updateStatusKey = "Update_Ready";
            }
            else
            {
                using var installer = _updates.StartInstaller(_installer.Path, release, Environment.ProcessId);
                _installer.HandOffToInstaller();
                _installer = null;
                _shell.CloseForUpdate();
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            _installer?.Dispose();
            _installer = null;
            _updateStatusKey = "Update_Cancelled";
        }
        catch (Exception)
        {
            _installer?.Dispose();
            _installer = null;
            _updateStatusKey = "Update_Error";
        }
        finally
        {
            _updateOperation = null;
            RefreshUpdateControls();
        }
    }

    private void UpdateCancel_Click(object sender, RoutedEventArgs e) => CancelUpdateOperation();
}
