namespace ClaudeMultiAccountLauncher.Core.Profiles;

/// <summary>
/// Where things live on disk. Roots are injectable so tests never touch the real profile.
/// </summary>
public sealed class LauncherPaths
{
    public const string AppFolderName = "claude-multi-account-launcher";

    public LauncherPaths(string appData, string localAppData, string userProfile)
    {
        AppData = appData;
        LocalAppData = localAppData;
        UserProfile = userProfile;
    }

    public static LauncherPaths ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>%APPDATA% (Roaming)</summary>
    public string AppData { get; }

    /// <summary>%LOCALAPPDATA%</summary>
    public string LocalAppData { get; }

    /// <summary>%USERPROFILE%</summary>
    public string UserProfile { get; }

    public string AppFolder => Path.Combine(AppData, AppFolderName);

    public string ProfilesFile => Path.Combine(AppFolder, "profiles.json");

    /// <summary>
    /// <c>%APPDATA%\Claude-&lt;slug&gt;</c>. Must sit directly under %APPDATA% (spec section 8, Cowork).
    /// The launcher must create it in the real %APPDATA% before Claude first runs, or MSIX
    /// redirects it into the package's LocalCache (verified in the Phase 1 spike).
    /// </summary>
    public string DataFolder(string slug) => Path.Combine(AppData, "Claude-" + slug);

    /// <summary><c>%USERPROFILE%\.claude-&lt;slug&gt;</c>, passed as CLAUDE_CONFIG_DIR.</summary>
    public string ConfigFolder(string slug) => Path.Combine(UserProfile, ".claude-" + slug);
}
