using ClaudeMultiAccountLauncher.Core.Startup;

namespace ClaudeMultiAccountLauncher.Core.Shortcuts;

public sealed class ShortcutConflictException(string path)
    : Exception($"\"{System.IO.Path.GetFileName(path)}\" already exists on the desktop and wasn't made by this launcher. Rename or delete it first.")
{
    public string ShortcutPath { get; } = path;
}

/// <summary>
/// Desktop shortcuts named "Claude - &lt;name&gt;" that run <c>launcher --launch &lt;slug&gt;</c> (FR-8).
/// A shortcut is ours when it targets an exe with the launcher's file name; it belongs to a
/// profile when its arguments are that profile's <c>--launch</c> line.
/// </summary>
public sealed class ShortcutService(string desktopFolder, string launcherExePath)
{
    public const string NamePrefix = "Claude - ";

    public string DesktopFolder { get; } = desktopFolder;

    public string PathFor(string displayName) => Path.Combine(DesktopFolder, NamePrefix + displayName + ".lnk");

    public static string ArgumentsFor(string slug) => $"{StartupCommand.LaunchSwitch} {slug}";

    /// <summary>Our shortcut for <paramref name="slug"/>, whatever it is called now, or null.</summary>
    public string? Find(string slug)
    {
        if (!Directory.Exists(DesktopFolder))
        {
            return null;
        }

        var wanted = ArgumentsFor(slug);
        foreach (var lnk in Directory.EnumerateFiles(DesktopFolder, NamePrefix + "*.lnk"))
        {
            var info = TryRead(lnk);
            if (info is not null && IsOurs(info) && string.Equals(info.Arguments.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return lnk;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates (or refreshes) the shortcut. An older shortcut for the same profile under a
    /// different name is removed, so renaming a profile and recreating doesn't leave two.
    /// </summary>
    /// <param name="iconPath">Stable .ico path, or null to use the launcher's own icon.</param>
    public string Create(string displayName, string slug, string? iconPath)
    {
        var path = PathFor(displayName);
        if (File.Exists(path) && TryRead(path) is var existing && (existing is null || !IsOurs(existing)))
        {
            throw new ShortcutConflictException(path);
        }

        var previous = Find(slug);

        ShellLink.Write(path, new ShellLinkInfo(
            launcherExePath,
            ArgumentsFor(slug),
            Path.GetDirectoryName(launcherExePath) ?? string.Empty,
            $"Open Claude as \"{displayName}\"",
            iconPath ?? string.Empty));

        if (previous is not null && !string.Equals(previous, path, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(previous);
        }

        return path;
    }

    /// <summary>After a rename: if the profile has a shortcut, recreate it under the new name.</summary>
    public string? Rename(string slug, string newDisplayName, string? iconPath) =>
        Find(slug) is null ? null : Create(newDisplayName, slug, iconPath);

    /// <summary>Deletes the profile's shortcut if there is one. Returns its path.</summary>
    public string? Delete(string slug)
    {
        var path = Find(slug);
        if (path is not null)
        {
            File.Delete(path);
        }

        return path;
    }

    private bool IsOurs(ShellLinkInfo info) =>
        string.Equals(Path.GetFileName(info.TargetPath), Path.GetFileName(launcherExePath), StringComparison.OrdinalIgnoreCase);

    private static ShellLinkInfo? TryRead(string lnk)
    {
        try
        {
            return ShellLink.Read(lnk);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
