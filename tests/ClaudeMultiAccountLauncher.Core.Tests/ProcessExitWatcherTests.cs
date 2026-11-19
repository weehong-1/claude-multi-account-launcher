using System.Collections.Concurrent;
using System.Diagnostics;
using ClaudeMultiAccountLauncher.Core.Claude;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class ProcessExitWatcherTests
{
    private static Process StartDummy() =>
        Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;

    private static async Task<bool> WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        return condition();
    }

    [Fact]
    public async Task Reports_a_watched_process_as_soon_as_it_ends()
    {
        using var dummy = StartDummy();
        using var watcher = new ProcessExitWatcher();
        var exited = new ConcurrentBag<int>();
        watcher.Exited += exited.Add;

        watcher.Watch([dummy.Id]);
        Assert.Equal(1, watcher.Count);
        Assert.Empty(exited);

        var sw = Stopwatch.StartNew();
        dummy.Kill();

        Assert.True(await WaitFor(() => exited.Contains(dummy.Id)));
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(0, watcher.Count);
    }

    [Fact]
    public async Task Each_exit_is_reported_once_even_if_watched_twice()
    {
        using var dummy = StartDummy();
        using var watcher = new ProcessExitWatcher();
        var exited = new ConcurrentBag<int>();
        watcher.Exited += exited.Add;

        watcher.Watch([dummy.Id]);
        watcher.Watch([dummy.Id]);
        Assert.Equal(1, watcher.Count);

        dummy.Kill();
        Assert.True(await WaitFor(() => exited.Contains(dummy.Id)));
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Single(exited);
    }

    [Fact]
    public void A_process_that_is_already_gone_is_reported_straight_away()
    {
        var dummy = StartDummy();
        var id = dummy.Id;
        dummy.Kill();
        dummy.WaitForExit();
        dummy.Dispose();

        using var watcher = new ProcessExitWatcher();
        var exited = new List<int>();
        watcher.Exited += exited.Add;

        watcher.Watch([id]);

        Assert.Equal([id], exited);
        Assert.Equal(0, watcher.Count);
    }
}
