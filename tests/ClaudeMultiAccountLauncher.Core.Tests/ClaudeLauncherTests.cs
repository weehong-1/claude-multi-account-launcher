using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public sealed class ClaudeLauncherTests : IDisposable
{
    private static readonly ClaudeInstall Install = new(InstallKind.Msix, @"C:\WA\claude-desktop.exe", @"C:\P\Claude_1_x64__pzs8sxrjxfjjc");
    private static readonly Profile Personal = new("Personal", "personal", DateTimeOffset.UnixEpoch);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cmal-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LauncherPaths _paths;

    public ClaudeLauncherTests()
    {
        _paths = new LauncherPaths(Path.Combine(_root, "Roaming"), Path.Combine(_root, "Local"), Path.Combine(_root, "home"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Profile_gets_user_data_dir_and_config_dir()
    {
        var psi = new ClaudeLauncher(_paths).BuildStartInfo(Install, Personal);

        Assert.Equal(Install.LaunchPath, psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.Equal(["--user-data-dir=" + Path.Combine(_root, "Roaming", "Claude-personal")], psi.ArgumentList);
        Assert.Equal(Path.Combine(_root, "home", ".claude-personal"), psi.Environment[ClaudeLauncher.ConfigDirVariable]);
    }

    [Fact]
    public void Default_gets_neither_flag_nor_config_dir()
    {
        var psi = new ClaudeLauncher(_paths).BuildStartInfo(Install, profile: null);

        Assert.Empty(psi.ArgumentList);
        Assert.False(psi.Environment.ContainsKey(ClaudeLauncher.ConfigDirVariable));
    }

    [Fact]
    public void EnsureFolders_creates_both_folders()
    {
        new ClaudeLauncher(_paths).EnsureFolders("personal");

        Assert.True(Directory.Exists(Path.Combine(_root, "Roaming", "Claude-personal")));
        Assert.True(Directory.Exists(Path.Combine(_root, "home", ".claude-personal")));
    }

    [Fact]
    public void Paths_follow_the_spec()
    {
        var paths = new LauncherPaths(@"C:\U\AppData\Roaming", @"C:\U\AppData\Local", @"C:\U");

        Assert.Equal(@"C:\U\AppData\Roaming\Claude-personal", paths.DataFolder("personal"));
        Assert.Equal(@"C:\U\.claude-personal", paths.ConfigFolder("personal"));
        Assert.Equal(@"C:\U\AppData\Roaming\claude-multi-account-launcher\profiles.json", paths.ProfilesFile);
    }
}
