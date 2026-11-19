using System.Diagnostics;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>Starts Claude Desktop for a profile (FR-3).</summary>
public sealed class ClaudeLauncher(LauncherPaths paths)
{
    public const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>
    /// Starts Claude. <paramref name="profile"/> null means the built-in Default profile.
    /// If that profile is already running, Claude's own single-instance lock brings it to the front.
    /// </summary>
    /// <param name="link">
    /// Optional claude:// link to hand to that instance, e.g. a Google sign-in callback (FR-4).
    /// Passed the way Windows passes it to Default (<c>-- "claude://..."</c>), so a running
    /// profile instance receives it through Claude's own single-instance hand-off.
    /// </param>
    public Process? Launch(ClaudeInstall install, Profile? profile, SignInLink? link = null)
    {
        if (profile is not null)
        {
            EnsureFolders(profile.Slug);
        }

        return Process.Start(BuildStartInfo(install, profile, link));
    }

    /// <summary>
    /// Creates the profile's folders in the real %APPDATA% / %USERPROFILE%. Done before every
    /// launch: if the data folder is missing when MSIX Claude starts, its writes are redirected
    /// into the package's LocalCache instead (Phase 1 spike).
    /// </summary>
    public void EnsureFolders(string slug)
    {
        Directory.CreateDirectory(paths.DataFolder(slug));
        Directory.CreateDirectory(paths.ConfigFolder(slug));
    }

    public ProcessStartInfo BuildStartInfo(ClaudeInstall install, Profile? profile, SignInLink? link = null)
    {
        var psi = new ProcessStartInfo(install.LaunchPath)
        {
            UseShellExecute = false, // needed to pass environment variables
            WorkingDirectory = paths.UserProfile,
        };

        if (profile is null)
        {
            // Default: no flag, and don't leak a CLAUDE_CONFIG_DIR the launcher itself may have inherited.
            psi.Environment.Remove(ConfigDirVariable);
        }
        else
        {
            psi.ArgumentList.Add("--user-data-dir=" + paths.DataFolder(profile.Slug));
            psi.Environment[ConfigDirVariable] = paths.ConfigFolder(profile.Slug);
        }

        if (link is not null)
        {
            // Same shape as the MSIX protocol handler: Parameters="-- &quot;%1&quot;"
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(link.Value);
        }

        return psi;
    }
}
