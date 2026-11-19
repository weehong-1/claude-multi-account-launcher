using ClaudeMultiAccountLauncher.Core.Claude;

namespace ClaudeMultiAccountLauncher.Core.Profiles;

/// <summary>Maps running Claude instances to profiles (FR-7).</summary>
public static class RunningProfiles
{
    /// <param name="profile">Null means the built-in Default profile.</param>
    public static bool IsRunning(Profile? profile, IEnumerable<RunningInstance> running, LauncherPaths paths) =>
        profile is null
            ? running.Any(r => r.UserDataDir is null)
            : running.Any(r => r.UserDataDir is not null && ProcessScanner.SameFolder(r.UserDataDir, paths.DataFolder(profile.Slug)));
}
