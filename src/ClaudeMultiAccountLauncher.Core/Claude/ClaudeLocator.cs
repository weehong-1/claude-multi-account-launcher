using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Claude;

public sealed class ClaudeNotFoundException(IReadOnlyList<string> triedPaths)
    : Exception("Claude Desktop was not found. Tried:" + Environment.NewLine + string.Join(Environment.NewLine, triedPaths))
{
    public IReadOnlyList<string> TriedPaths { get; } = triedPaths;
}

/// <summary>Seams so the lookup order can be tested without a real install.</summary>
public interface IInstallProbe
{
    bool FileExists(string path);

    /// <summary>Install location of the Claude MSIX package for the current user, or null.</summary>
    string? FindMsixInstallLocation();
}

/// <summary>
/// Resolves Claude Desktop (FR-6). Order:
/// 1. MSIX execution alias (needs the package location too, for the process filter)
/// 2. Standalone install
/// 3. MSIX package location without the alias (exe inside the package)
/// </summary>
public sealed class ClaudeLocator(LauncherPaths paths, IInstallProbe probe)
{
    public string AliasPath => Path.Combine(paths.LocalAppData, "Microsoft", "WindowsApps", MsixPackage.AliasFileName);

    public string StandaloneFolder => Path.Combine(paths.LocalAppData, "AnthropicClaude");

    public string StandalonePath => Path.Combine(StandaloneFolder, "claude.exe");

    public ClaudeInstall Locate()
    {
        var tried = new List<string>();

        tried.Add(AliasPath);
        var aliasExists = probe.FileExists(AliasPath);
        var packageLocation = probe.FindMsixInstallLocation();

        if (aliasExists && packageLocation is not null)
        {
            return new ClaudeInstall(InstallKind.Msix, AliasPath, packageLocation);
        }

        tried.Add(StandalonePath);
        if (probe.FileExists(StandalonePath))
        {
            return new ClaudeInstall(InstallKind.Standalone, StandalonePath, StandaloneFolder);
        }

        tried.Add($"MSIX package {MsixPackage.FamilyName}");
        if (packageLocation is not null)
        {
            var exe = Path.Combine(packageLocation, MsixPackage.RelativeExePath);
            tried.Add(exe);
            if (probe.FileExists(exe))
            {
                return new ClaudeInstall(InstallKind.Msix, exe, packageLocation);
            }
        }

        throw new ClaudeNotFoundException(tried);
    }
}
