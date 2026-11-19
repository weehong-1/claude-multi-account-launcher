using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class ClaudeLocatorTests
{
    private const string PackageDir = @"C:\Program Files\WindowsApps\Claude_6.6395.0.0_x64__pzs8sxrjxfjjc";
    private static readonly LauncherPaths Paths = new(@"C:\U\AppData\Roaming", @"C:\U\AppData\Local", @"C:\U");

    private sealed class FakeProbe(string? packageLocation, params string[] files) : IInstallProbe
    {
        public bool FileExists(string path) => files.Contains(path, StringComparer.OrdinalIgnoreCase);

        public string? FindMsixInstallLocation() => packageLocation;
    }

    [Fact]
    public void Msix_alias_wins_and_carries_the_package_folder()
    {
        var install = new ClaudeLocator(Paths, new FakeProbe(PackageDir,
            @"C:\U\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe",
            @"C:\U\AppData\Local\AnthropicClaude\claude.exe")).Locate();

        Assert.Equal(InstallKind.Msix, install.Kind);
        Assert.Equal(@"C:\U\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe", install.LaunchPath);
        Assert.Equal(PackageDir, install.InstallFolder);
    }

    [Fact]
    public void Standalone_used_when_no_msix()
    {
        var install = new ClaudeLocator(Paths, new FakeProbe(null,
            @"C:\U\AppData\Local\AnthropicClaude\claude.exe")).Locate();

        Assert.Equal(InstallKind.Standalone, install.Kind);
        Assert.Equal(@"C:\U\AppData\Local\AnthropicClaude\claude.exe", install.LaunchPath);
        Assert.Equal(@"C:\U\AppData\Local\AnthropicClaude", install.InstallFolder);
    }

    [Fact]
    public void Package_exe_used_when_alias_missing()
    {
        var install = new ClaudeLocator(Paths, new FakeProbe(PackageDir, PackageDir + @"\app\Claude.exe")).Locate();

        Assert.Equal(InstallKind.Msix, install.Kind);
        Assert.Equal(PackageDir + @"\app\Claude.exe", install.LaunchPath);
    }

    [Fact]
    public void Not_found_lists_every_path_tried()
    {
        var ex = Assert.Throws<ClaudeNotFoundException>(() => new ClaudeLocator(Paths, new FakeProbe(null)).Locate());

        Assert.Contains(@"C:\U\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe", ex.TriedPaths);
        Assert.Contains(@"C:\U\AppData\Local\AnthropicClaude\claude.exe", ex.TriedPaths);
        Assert.Contains(ex.TriedPaths, p => p.Contains(MsixPackage.FamilyName));
    }
}
