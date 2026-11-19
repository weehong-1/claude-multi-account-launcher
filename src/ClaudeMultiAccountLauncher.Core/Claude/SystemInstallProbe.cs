using Windows.Management.Deployment;

namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>Real file system and package manager. No admin rights needed.</summary>
public sealed class SystemInstallProbe : IInstallProbe
{
    public bool FileExists(string path) => File.Exists(path);

    /// <summary>Equivalent of <c>Get-AppxPackage</c> for Claude's package family, current user only.</summary>
    public string? FindMsixInstallLocation()
    {
        try
        {
            var package = new PackageManager()
                .FindPackagesForUser(string.Empty, MsixPackage.FamilyName)
                .OrderByDescending(p => p.Id.Version.Major)
                .ThenByDescending(p => p.Id.Version.Minor)
                .ThenByDescending(p => p.Id.Version.Build)
                .ThenByDescending(p => p.Id.Version.Revision)
                .FirstOrDefault();

            return package?.InstalledLocation.Path;
        }
        catch (Exception)
        {
            // Package APIs can fail on stripped-down Windows; treat as "no MSIX install".
            return null;
        }
    }
}
