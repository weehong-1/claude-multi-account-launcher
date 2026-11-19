using System.Diagnostics;
using ClaudeMultiAccountLauncher.Core.Claude;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class ClaudeCloserTests
{
    private const string PackageDir = @"C:\Program Files\WindowsApps\Claude_6.6395.0.0_x64__pzs8sxrjxfjjc";
    private const string DesktopExe = PackageDir + @"\app\Claude.exe";
    private const string CliExe = @"C:\Users\V\AppData\Local\Claude-Data\claude_pzs8sxrjxfjjc\claude-code\2.1.326\abc\claude.exe";
    private static readonly ClaudeInstall Msix = new(InstallKind.Msix, @"C:\WA\claude-desktop.exe", PackageDir);

    /// <summary>A fake system: terminated processes disappear from the next scan.</summary>
    private sealed class FakeSystem(params ProcessSnapshot[] initial) : IProcessSource, IProcessTerminator
    {
        private readonly List<ProcessSnapshot> _alive = [.. initial];

        public List<int> Terminated { get; } = [];

        public HashSet<int> Unkillable { get; } = [];

        public IReadOnlyList<ProcessSnapshot> GetClaudeProcesses() => [.. _alive];

        public TerminateOutcome Terminate(int processId, Func<string?, bool> isAllowed)
        {
            var p = _alive.FirstOrDefault(x => x.ProcessId == processId);
            if (p is null)
            {
                return TerminateOutcome.Gone;
            }

            if (!isAllowed(p.ExecutablePath))
            {
                return TerminateOutcome.Skipped;
            }

            if (Unkillable.Contains(processId))
            {
                return TerminateOutcome.Failed;
            }

            Terminated.Add(processId);
            _alive.Remove(p);
            return TerminateOutcome.Gone;
        }
    }

    private static readonly ProcessSnapshot DefaultMain = new(100, DesktopExe, $"\"{DesktopExe}\" ");
    private static readonly ProcessSnapshot DefaultGpu = new(101, DesktopExe, $"\"{DesktopExe}\" --type=gpu-process");
    private static readonly ProcessSnapshot ProfileMain = new(200, DesktopExe, $"\"{DesktopExe}\" --user-data-dir=C:\\x");
    private static readonly ProcessSnapshot Cli = new(300, CliExe, $"\"{CliExe}\"");

    [Fact]
    public async Task Closes_every_desktop_process_and_never_the_CLI()
    {
        var system = new FakeSystem(DefaultGpu, DefaultMain, ProfileMain, Cli);
        var closer = new ClaudeCloser(new ProcessScanner(system), system);

        var done = await closer.CloseAllAsync(Msix, pollInterval: TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(done);
        Assert.DoesNotContain(300, system.Terminated);
        Assert.Equal([300], system.GetClaudeProcesses().Select(p => p.ProcessId));
    }

    [Fact]
    public async Task Main_processes_are_ended_before_children()
    {
        var system = new FakeSystem(DefaultGpu, DefaultMain);
        var closer = new ClaudeCloser(new ProcessScanner(system), system);

        await closer.CloseAllAsync(Msix, pollInterval: TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([100, 101], system.Terminated);
    }

    [Fact]
    public async Task Nothing_running_returns_true_without_terminating()
    {
        var system = new FakeSystem(Cli);
        var closer = new ClaudeCloser(new ProcessScanner(system), system);

        Assert.True(await closer.CloseAllAsync(Msix, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(system.Terminated);
    }

    [Fact]
    public async Task Gives_up_after_timeout_when_a_process_will_not_die()
    {
        var system = new FakeSystem(DefaultMain);
        system.Unkillable.Add(100);
        var closer = new ClaudeCloser(new ProcessScanner(system), system);

        var done = await closer.CloseAllAsync(
            Msix, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

        Assert.False(done);
    }

    // ---- Real Win32 terminator, against a throwaway process we start ourselves ----

    private static Process StartDummy() =>
        Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;

    [Fact]
    public void Win32_terminator_skips_a_process_whose_path_is_not_allowed()
    {
        using var dummy = StartDummy();
        try
        {
            string? seenPath = null;
            var outcome = new Win32ProcessTerminator().Terminate(dummy.Id, path => { seenPath = path; return false; });

            Assert.Equal(TerminateOutcome.Skipped, outcome);
            Assert.EndsWith(@"\ping.exe", seenPath, StringComparison.OrdinalIgnoreCase);
            Assert.False(dummy.HasExited);
        }
        finally
        {
            dummy.Kill();
        }
    }

    [Fact]
    public void Win32_terminator_ends_an_allowed_process()
    {
        using var dummy = StartDummy();

        var outcome = new Win32ProcessTerminator().Terminate(dummy.Id, _ => true);

        Assert.Equal(TerminateOutcome.Gone, outcome);
        Assert.True(dummy.WaitForExit(5000));
    }

    [Fact]
    public void Win32_terminator_reports_gone_for_an_exited_process()
    {
        using var dummy = StartDummy();
        var id = dummy.Id;
        dummy.Kill();
        dummy.WaitForExit();
        dummy.Dispose();

        Assert.Equal(TerminateOutcome.Gone, new Win32ProcessTerminator().Terminate(id, _ => true));
    }
}
