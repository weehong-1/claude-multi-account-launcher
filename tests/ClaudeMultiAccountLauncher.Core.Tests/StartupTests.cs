using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;
using ClaudeMultiAccountLauncher.Core.Startup;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public sealed class StartupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cmal-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LauncherPaths _paths;
    private readonly ProfileStore _store;
    private readonly List<(ClaudeInstall Install, Profile? Profile)> _started = [];

    public StartupTests()
    {
        _paths = new LauncherPaths(Path.Combine(_root, "Roaming"), Path.Combine(_root, "Local"), Path.Combine(_root, "home"));
        _store = new ProfileStore(_paths.ProfilesFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class Probe(bool found) : IInstallProbe
    {
        public bool FileExists(string path) => found;

        public string? FindMsixInstallLocation() => found ? @"C:\P\Claude_1_x64__pzs8sxrjxfjjc" : null;
    }

    private QuickLauncher NewQuickLauncher(bool claudeFound = true) =>
        new(_store, new ClaudeLocator(_paths, new Probe(claudeFound)), (i, p) => _started.Add((i, p)));

    [Theory]
    [InlineData(new[] { "--launch", "personal" }, "personal")]
    [InlineData(new[] { "--launch=Personal" }, "personal")]
    [InlineData(new[] { "--LAUNCH", "default" }, "default")]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--something" }, null)]
    public void Parses_launch_switch(string[] args, string? expected)
    {
        var cmd = StartupCommand.Parse(args);
        Assert.Equal(expected, cmd.LaunchSlug);
        Assert.Null(cmd.Error);
    }

    [Theory]
    [InlineData("--launch")]
    [InlineData("--launch=")]
    [InlineData("--launch| ")]
    public void Launch_without_slug_is_an_error(string argsJoinedByBar)
    {
        var cmd = StartupCommand.Parse(argsJoinedByBar.Split('|'));
        Assert.Null(cmd.LaunchSlug);
        Assert.NotNull(cmd.Error);
    }

    [Fact]
    public void Default_launches_with_no_profile()
    {
        var outcome = NewQuickLauncher().Run(StartupCommand.DefaultSlug);

        Assert.IsType<QuickLaunchOutcome.Launched>(outcome);
        Assert.Null(Assert.Single(_started).Profile);
    }

    [Fact]
    public void Ready_profile_launches_directly()
    {
        var ready = new Profile("Personal", "personal", ProfileStatus.Ready, DateTimeOffset.UnixEpoch);
        _store.Save([ready]);

        var outcome = NewQuickLauncher().Run("personal");

        Assert.IsType<QuickLaunchOutcome.Launched>(outcome);
        Assert.Equal(ready, Assert.Single(_started).Profile);
    }

    [Fact]
    public void Not_signed_in_profile_needs_the_sign_in_flow_and_starts_nothing()
    {
        var fresh = new Profile("Personal", "personal", ProfileStatus.NotSignedIn, DateTimeOffset.UnixEpoch);
        _store.Save([fresh]);

        var outcome = NewQuickLauncher().Run("personal");

        Assert.Equal(fresh, Assert.IsType<QuickLaunchOutcome.NeedsSignIn>(outcome).Profile);
        Assert.Empty(_started);
    }

    [Fact]
    public void Unknown_profile_fails_with_a_message()
    {
        var outcome = NewQuickLauncher().Run("gone");

        Assert.Contains("no longer exists", Assert.IsType<QuickLaunchOutcome.Failed>(outcome).Message);
        Assert.Empty(_started);
    }

    [Fact]
    public void Claude_not_found_fails_with_the_paths_tried()
    {
        var outcome = NewQuickLauncher(claudeFound: false).Run(StartupCommand.DefaultSlug);

        Assert.Contains("Tried:", Assert.IsType<QuickLaunchOutcome.Failed>(outcome).Message);
    }
}
