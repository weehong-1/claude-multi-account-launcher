using ClaudeMultiAccountLauncher.Core.Claude;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class ProcessScannerTests
{
    private const string PackageDir = @"C:\Program Files\WindowsApps\Claude_6.6395.0.0_x64__pzs8sxrjxfjjc";
    private const string DesktopExe = PackageDir + @"\app\Claude.exe";
    private const string Alias = @"C:\Users\V\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe";
    private const string CliExe = @"C:\Users\V\AppData\Local\Claude-Data\claude_pzs8sxrjxfjjc\claude-code\2.1.326\abc\claude.exe";

    private static readonly ClaudeInstall Msix = new(InstallKind.Msix, Alias, PackageDir);

    private sealed class FakeSource(params ProcessSnapshot[] processes) : IProcessSource
    {
        public IReadOnlyList<ProcessSnapshot> GetClaudeProcesses() => processes;
    }

    // Real command lines captured in the Phase 1 spike
    private static readonly ProcessSnapshot DefaultMain = new(100, DesktopExe, $"\"{DesktopExe}\" ");
    private static readonly ProcessSnapshot DefaultRenderer = new(101, DesktopExe, $"\"{DesktopExe}\" --type=renderer --user-data-dir=\"C:\\x\"");
    private static readonly ProcessSnapshot ProfileMain = new(200, DesktopExe, $"\"{Alias}\" --user-data-dir=C:\\Users\\V\\AppData\\Roaming\\Claude-personal");
    private static readonly ProcessSnapshot ProfileGpu = new(201, DesktopExe, $"\"{DesktopExe}\" --type=gpu-process --user-data-dir=C:\\Users\\V\\AppData\\Roaming\\Claude-personal");
    private static readonly ProcessSnapshot Cli = new(300, CliExe, $"\"{CliExe}\" --resume");

    [Fact]
    public void Desktop_processes_exclude_the_Claude_Code_CLI()
    {
        var scanner = new ProcessScanner(new FakeSource(DefaultMain, DefaultRenderer, ProfileMain, ProfileGpu, Cli));

        var ids = scanner.DesktopProcesses(Msix).Select(p => p.ProcessId);

        Assert.Equal([100, 101, 200, 201], ids);
    }

    [Fact]
    public void Running_instances_are_main_processes_keyed_by_user_data_dir()
    {
        var scanner = new ProcessScanner(new FakeSource(DefaultMain, DefaultRenderer, ProfileMain, ProfileGpu, Cli));

        var running = scanner.RunningInstances(Msix);

        Assert.Equal(
            [new RunningInstance(100, null), new RunningInstance(200, @"C:\Users\V\AppData\Roaming\Claude-personal")],
            running);
    }

    [Fact]
    public void Quoted_whole_argument_with_spaces_is_parsed()
    {
        // What .NET's ProcessStartInfo.ArgumentList produces when the path has a space
        var p = new ProcessSnapshot(1, DesktopExe, $"\"{Alias}\" \"--user-data-dir=C:\\Users\\Ann Lee\\AppData\\Roaming\\Claude-work\"");

        var running = new ProcessScanner(new FakeSource(p)).RunningInstances(Msix);

        Assert.Equal(@"C:\Users\Ann Lee\AppData\Roaming\Claude-work", Assert.Single(running).UserDataDir);
    }

    [Fact]
    public void Process_from_previous_package_version_still_counts_as_desktop()
    {
        var oldExe = @"C:\Program Files\WindowsApps\Claude_6.6000.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe";
        var scanner = new ProcessScanner(new FakeSource(new ProcessSnapshot(1, oldExe, $"\"{oldExe}\"")));

        Assert.Single(scanner.DesktopProcesses(Msix));
    }

    [Theory]
    [InlineData(@"C:\Program Files\WindowsApps\ClaudeHelper_1.0.0.0_x64__other123\app\claude.exe")]
    [InlineData(@"C:\Program Files\WindowsApps\Claude_6.6395.0.0_x64__someoneelse\app\claude.exe")]
    [InlineData(@"C:\tools\claude.exe")]
    [InlineData(null)]
    public void Unrelated_claude_exe_is_ignored(string? exe) =>
        Assert.False(Msix.OwnsExecutable(exe));

    [Fact]
    public void Standalone_install_matches_its_versioned_app_folder()
    {
        var standalone = new ClaudeInstall(InstallKind.Standalone, @"C:\U\AppData\Local\AnthropicClaude\claude.exe", @"C:\U\AppData\Local\AnthropicClaude");

        Assert.True(standalone.OwnsExecutable(@"C:\U\AppData\Local\AnthropicClaude\app-0.9.3\claude.exe"));
        Assert.False(standalone.OwnsExecutable(@"C:\U\AppData\Local\AnthropicClaudeOther\claude.exe"));
    }

    [Theory]
    [InlineData(@"C:\A\Claude-x", @"c:\a\claude-x\", true)]
    [InlineData(@"C:\A\Claude-x", @"C:\A\Claude-xy", false)]
    public void SameFolder_ignores_case_and_trailing_slash(string a, string b, bool expected) =>
        Assert.Equal(expected, ProcessScanner.SameFolder(a, b));
}
