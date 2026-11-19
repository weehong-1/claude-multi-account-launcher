namespace ClaudeMultiAccountLauncher.Core.Claude;

public enum InstallKind
{
    /// <summary>Microsoft Store / MSIX package (current).</summary>
    Msix,

    /// <summary>Older Squirrel install under %LOCALAPPDATA%\AnthropicClaude.</summary>
    Standalone,
}

/// <summary>
/// A resolved Claude Desktop install (FR-6).
/// </summary>
/// <param name="LaunchPath">What to start. For MSIX this is the execution alias, which keeps
/// package identity and passes both arguments and environment through (Phase 1 spike).</param>
/// <param name="InstallFolder">Folder that holds the Desktop executable. Used to tell Claude
/// Desktop processes apart from the Claude Code CLI, which is also claude.exe (FR-4, FR-7).</param>
public sealed record ClaudeInstall(InstallKind Kind, string LaunchPath, string InstallFolder)
{
    /// <summary>
    /// The real Claude exe to copy the icon from. For MSIX this is inside the versioned package
    /// folder, which is deleted on update, so never point a shortcut at it directly (FR-8).
    /// </summary>
    public string IconSourcePath => Kind == InstallKind.Msix
        ? Path.Combine(InstallFolder, MsixPackage.RelativeExePath)
        : LaunchPath;

    /// <summary>
    /// True when <paramref name="executablePath"/> is Claude Desktop from this install.
    /// For MSIX any version of the package matches, so a process still running from the
    /// previous version right after an update is still recognised.
    /// </summary>
    public bool OwnsExecutable(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return false;
        }

        var exe = Path.GetFullPath(executablePath);
        if (IsUnder(exe, InstallFolder))
        {
            return true;
        }

        if (Kind != InstallKind.Msix)
        {
            return false;
        }

        // InstallFolder = ...\WindowsApps\Claude_6.6395.0.0_x64__pzs8sxrjxfjjc
        var packagesRoot = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(InstallFolder));
        if (packagesRoot is null || !IsUnder(exe, packagesRoot))
        {
            return false;
        }

        var packageFolder = exe[(packagesRoot.Length + 1)..].Split(Path.DirectorySeparatorChar)[0];
        return MsixPackage.IsPackageFolderName(packageFolder);
    }

    private static bool IsUnder(string path, string folder)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

public static class MsixPackage
{
    /// <summary>Package name from Claude's AppxManifest.</summary>
    public const string Name = "Claude";

    /// <summary>Anthropic's publisher id, part of the package family name.</summary>
    public const string PublisherId = "pzs8sxrjxfjjc";

    public const string FamilyName = Name + "_" + PublisherId;

    /// <summary>Execution alias declared in Claude's AppxManifest.</summary>
    public const string AliasFileName = "claude-desktop.exe";

    /// <summary>Executable inside the package, relative to its install location.</summary>
    public const string RelativeExePath = @"app\Claude.exe";

    /// <summary>Matches <c>Claude_&lt;version&gt;_&lt;arch&gt;__pzs8sxrjxfjjc</c>.</summary>
    public static bool IsPackageFolderName(string folderName) =>
        folderName.StartsWith(Name + "_", StringComparison.OrdinalIgnoreCase)
        && folderName.EndsWith("__" + PublisherId, StringComparison.OrdinalIgnoreCase);
}
