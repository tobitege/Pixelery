namespace Pixelery.Core;

/// <summary>Product information shown in the app's about section and by the command line tool.</summary>
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/tobitege/Pixelery";

    public const string Copyright = "© 2026 tobitege";

    public const string License = "MIT License";

    /// <summary>Environment variable that overrides the settings folder (absolute path).</summary>
    public const string StateRootVariable = "PIXELERY_STATE_ROOT";

    /// <summary>Name of the app before it was renamed to Pixelery; its settings folder is copied once.</summary>
    private const string LegacyName = "AbstractImg";

    /// <summary>
    /// Settings folder: <see cref="StateRootVariable"/> when set, otherwise %LOCALAPPDATA%\Pixelery. When that folder does not
    /// exist yet but the folder of the former name does, its content is copied over (the old folder stays untouched).
    /// </summary>
    public static string StateDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(StateRootVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured))
            return Path.GetFullPath(configured);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(localAppData, "Pixelery");
        var legacy = Path.Combine(localAppData, LegacyName);
        if (!Directory.Exists(folder) && Directory.Exists(legacy))
        {
            try
            {
                CopyDirectory(legacy, folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Start with fresh settings if the old ones cannot be copied.
            }
        }
        return folder;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    /// <summary>Version of the calling assembly as "major.minor.patch".</summary>
    public static string Version(System.Reflection.Assembly assembly) =>
        assembly.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
}
