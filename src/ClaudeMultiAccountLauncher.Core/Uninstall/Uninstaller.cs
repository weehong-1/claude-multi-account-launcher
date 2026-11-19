using System.Diagnostics;
using ClaudeMultiAccountLauncher.Core.Profiles;
using ClaudeMultiAccountLauncher.Core.Shortcuts;

namespace ClaudeMultiAccountLauncher.Core.Uninstall;

public enum UninstallItemKind
{
    /// <summary>A profile's Claude Desktop data: sign-in, chats, settings.</summary>
    ProfileData,

    /// <summary>A profile's Claude Code config (CLAUDE_CONFIG_DIR).</summary>
    ProfileConfig,

    DesktopShortcut,

    /// <summary>The launcher's own folder: profiles.json and the shortcut icon.</summary>
    LauncherData,

    /// <summary>Looks like a profile folder but isn't in the list (e.g. kept after Remove).</summary>
    OldProfileFolder,
}

public sealed record UninstallItem(string Path, UninstallItemKind Kind, string? ProfileName = null);

/// <summary>Everything a full uninstall removes, shown to the user before anything is deleted.</summary>
/// <param name="Items">Removed when the user confirms.</param>
/// <param name="OldProfileFolders">Removed only if the user also ticks them.</param>
/// <param name="ProductCode">The installed MSI to uninstall afterwards, or null when running portable.</param>
/// <param name="PortableExe">The running exe when it isn't the installed one; the user deletes it.</param>
/// <param name="ExtractionRoot">Unpacked runtime under %TEMP%; removed after the launcher exits.</param>
public sealed record UninstallPlan(
    IReadOnlyList<UninstallItem> Items,
    IReadOnlyList<UninstallItem> OldProfileFolders,
    string? ProductCode,
    string? PortableExe,
    string? ExtractionRoot);

/// <summary>
/// Removes everything the launcher created (spec FR-9). Never touches the Default profile's
/// data, which belongs to Claude itself, and never a folder it can't tie to the launcher
/// without the user ticking it.
/// </summary>
public sealed class Uninstaller(LauncherPaths paths, ShortcutService shortcuts, IInstalledProduct product)
{
    public UninstallPlan Plan(IReadOnlyList<Profile> profiles, string? runningExe, string? appBaseDirectory)
    {
        var items = new List<UninstallItem>();
        foreach (var p in profiles)
        {
            AddIfExists(items, paths.DataFolder(p.Slug), UninstallItemKind.ProfileData, p.Name);
            AddIfExists(items, paths.RedirectedDataFolder(p.Slug), UninstallItemKind.ProfileData, p.Name);
            AddIfExists(items, paths.ConfigFolder(p.Slug), UninstallItemKind.ProfileConfig, p.Name);
        }

        items.AddRange(shortcuts.FindAll().Select(s => new UninstallItem(s, UninstallItemKind.DesktopShortcut)));
        AddIfExists(items, paths.AppFolder, UninstallItemKind.LauncherData);

        var productCode = product.FindProductCode();
        var installedExe = Path.Combine(paths.LocalAppData, InstallerInfo.InstallFolderRelative, "claude-multi-account-launcher.exe");
        var portable = runningExe is not null && !(productCode is not null && SamePath(runningExe, installedExe))
            ? runningExe
            : null;

        return new UninstallPlan(items, FindOldProfileFolders(profiles), productCode, portable, ExtractionRoot(appBaseDirectory));
    }

    /// <summary>Deletes the plan's items (and old profile folders if asked). Returns what couldn't be deleted.</summary>
    public IReadOnlyList<string> DeleteData(UninstallPlan plan, bool includeOldProfileFolders)
    {
        var targets = includeOldProfileFolders ? plan.Items.Concat(plan.OldProfileFolders) : plan.Items;
        var failed = new List<string>();
        foreach (var item in targets)
        {
            try
            {
                if (item.Kind == UninstallItemKind.DesktopShortcut)
                {
                    File.Delete(item.Path);
                }
                else if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(item.Path);
            }
        }

        return failed;
    }

    /// <summary>
    /// Runs after the launcher has exited (it can't delete itself): waits a moment, uninstalls
    /// the MSI if there is one, and removes the unpacked runtime. Null when there's nothing to do.
    /// </summary>
    public ProcessStartInfo? FinishCommand(UninstallPlan plan)
    {
        var steps = new List<string> { "ping -n 3 127.0.0.1 >nul" }; // ~2 s; `timeout` needs a console
        if (plan.ProductCode is not null)
        {
            steps.Add($"msiexec.exe /x {plan.ProductCode} /passive");
        }

        if (plan.ExtractionRoot is not null)
        {
            steps.Add($"rmdir /s /q \"{plan.ExtractionRoot}\"");
        }

        if (steps.Count == 1)
        {
            return null;
        }

        // cmd strips the outer quotes and keeps the inner ones
        return new ProcessStartInfo("cmd.exe", $"/d /c \"{string.Join(" & ", steps)}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = paths.UserProfile,
        };
    }

    /// <summary>
    /// The single-file exe unpacks to %TEMP%\.net\&lt;app&gt;\&lt;hash&gt;; returns %TEMP%\.net\&lt;app&gt;.
    /// Null when not running from there (e.g. a dev build).
    /// </summary>
    public static string? ExtractionRoot(string? appBaseDirectory)
    {
        if (string.IsNullOrEmpty(appBaseDirectory))
        {
            return null;
        }

        var bundleRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(Path.GetTempPath(), ".net")));
        var hashDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appBaseDirectory));
        var appDir = Path.GetDirectoryName(hashDir);
        return appDir is not null && SamePath(Path.GetDirectoryName(appDir), bundleRoot) ? appDir : null;
    }

    /// <summary>%APPDATA%\Claude-* and %USERPROFILE%\.claude-* not belonging to any current profile.</summary>
    private List<UninstallItem> FindOldProfileFolders(IReadOnlyList<Profile> profiles)
    {
        // The launcher's own folder matches "Claude-*" too (Windows matching ignores case);
        // it is already in the main list as LauncherData.
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(paths.AppFolder) };
        foreach (var p in profiles)
        {
            known.Add(Path.GetFullPath(paths.DataFolder(p.Slug)));
            known.Add(Path.GetFullPath(paths.ConfigFolder(p.Slug)));
        }

        var found = new List<UninstallItem>();
        foreach (var (root, pattern, kind) in new[]
                 {
                     (paths.AppData, "Claude-*", UninstallItemKind.OldProfileFolder),
                     (paths.UserProfile, ".claude-*", UninstallItemKind.OldProfileFolder),
                 })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root, pattern))
            {
                if (!known.Contains(Path.GetFullPath(dir)))
                {
                    found.Add(new UninstallItem(dir, kind));
                }
            }
        }

        return found;
    }

    private static void AddIfExists(List<UninstallItem> items, string path, UninstallItemKind kind, string? profileName = null)
    {
        if (Directory.Exists(path))
        {
            items.Add(new UninstallItem(path, kind, profileName));
        }
    }

    private static bool SamePath(string? a, string? b) =>
        a is not null && b is not null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}
