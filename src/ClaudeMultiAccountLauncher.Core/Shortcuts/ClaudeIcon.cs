using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Shortcuts;

/// <summary>Keeps one stable copy of Claude's icon for shortcuts (FR-8).</summary>
public static class ClaudeIcon
{
    /// <summary>
    /// Returns the stable .ico, extracting it the first time. Returns null if it can't be made,
    /// in which case shortcuts fall back to the launcher's own icon.
    /// </summary>
    public static string? Ensure(LauncherPaths paths, ClaudeInstall? install)
    {
        if (File.Exists(paths.ClaudeIconFile))
        {
            return paths.ClaudeIconFile;
        }

        if (install is null)
        {
            return null;
        }

        try
        {
            IconExtractor.ExtractMainIcon(install.IconSourcePath, paths.ClaudeIconFile);
            return paths.ClaudeIconFile;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
