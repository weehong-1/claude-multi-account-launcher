using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Startup;

public abstract record QuickLaunchOutcome
{
    /// <summary>Claude was started; the launcher can exit without showing a window.</summary>
    public sealed record Launched : QuickLaunchOutcome;

    /// <summary>Show the window with this error.</summary>
    public sealed record Failed(string Message) : QuickLaunchOutcome;
}

/// <summary>
/// Handles <c>--launch &lt;slug&gt;</c> from a desktop shortcut (FR-8): one click opens the
/// profile, and the launcher window only appears when something went wrong.
/// </summary>
public sealed class QuickLauncher(ProfileStore store, ClaudeLocator locator, Action<ClaudeInstall, Profile?> start)
{
    public QuickLaunchOutcome Run(string slug)
    {
        Profile? profile = null;
        if (slug != StartupCommand.DefaultSlug)
        {
            try
            {
                profile = store.Load().FirstOrDefault(p => p.Slug == slug);
            }
            catch (ProfileStoreException ex)
            {
                return new QuickLaunchOutcome.Failed(ex.Message);
            }

            if (profile is null)
            {
                return new QuickLaunchOutcome.Failed(
                    $"The shortcut points to a profile that no longer exists (\"{slug}\"). Delete the shortcut or create it again.");
            }
        }

        ClaudeInstall install;
        try
        {
            install = locator.Locate();
        }
        catch (ClaudeNotFoundException ex)
        {
            return new QuickLaunchOutcome.Failed(ex.Message);
        }

        try
        {
            start(install, profile);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new QuickLaunchOutcome.Failed($"Couldn't start Claude: {ex.Message}");
        }

        return new QuickLaunchOutcome.Launched();
    }
}
