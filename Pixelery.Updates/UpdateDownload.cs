namespace Pixelery.Updates;

/// <summary>Owns a staged installer until it is discarded or handed to the installer process.</summary>
public sealed class UpdateDownload : IDisposable
{
    private const string InstallerName = "Pixelery-Setup.exe";
    private const string LeaseName = "download.lock";
    private FileStream? _lease;
    public string Path { get; }

    private UpdateDownload(string directory, FileStream lease)
    {
        Path = System.IO.Path.Combine(directory, InstallerName);
        _lease = lease;
    }

    internal static UpdateDownload Create(string stateDirectory)
    {
        var directory = System.IO.Path.Combine(CacheRoot(stateDirectory), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new UpdateDownload(directory, new FileStream(System.IO.Path.Combine(directory, LeaseName),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
    }

    /// <summary>Release ownership only after the installer process has started.</summary>
    public void HandOffToInstaller()
    {
        _lease?.Dispose();
        _lease = null;
    }

    public void Dispose()
    {
        if (_lease == null)
            return;
        _lease.Dispose();
        _lease = null;
        CleanupDirectory(System.IO.Path.GetDirectoryName(Path)!);
    }

    public static void CleanupAbandoned(string stateDirectory)
    {
        try
        {
            var root = CacheRoot(stateDirectory);
            if (!Directory.Exists(root) || IsLink(root))
                return;
            foreach (var directory in Directory.EnumerateDirectories(root))
                if (Guid.TryParseExact(System.IO.Path.GetFileName(directory), "N", out _))
                    CleanupDirectory(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string CacheRoot(string stateDirectory)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory) ||
            stateDirectory.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0 ||
            !System.IO.Path.IsPathFullyQualified(stateDirectory))
            throw new ArgumentException("Expected an absolute app data directory.", nameof(stateDirectory));
        var state = System.IO.Path.GetFullPath(stateDirectory);
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(state, "Updates"));
        var prefix = state.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            + System.IO.Path.DirectorySeparatorChar;
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The update cache must stay inside the app data directory.", nameof(stateDirectory));
        if (Directory.Exists(root) && IsLink(root))
            throw new IOException("The update cache must not be a directory link.");
        return root;
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void CleanupDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory) || IsLink(directory))
                return;
            // Only our flat cache layout is owned. Never traverse links or remove foreign files.
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                if (System.IO.Path.GetFileName(entry) is not (InstallerName or LeaseName) ||
                    IsLink(entry) || Directory.Exists(entry))
                    return;
            var leasePath = System.IO.Path.Combine(directory, LeaseName);
            // A missing lease means creation has not finished. Only the creator may
            // publish it; cleanup must never claim or remove an initializing directory.
            using (var lease = new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                File.Delete(System.IO.Path.Combine(directory, InstallerName));
            File.Delete(leasePath);
            Directory.Delete(directory);
        }
        // Another instance owns the lease, or Windows still has the installer executable open.
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
