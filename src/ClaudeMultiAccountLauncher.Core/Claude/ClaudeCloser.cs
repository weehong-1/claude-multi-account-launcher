namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>
/// Ends every Claude Desktop process before a first sign-in (FR-4 step 2).
/// Processes are picked by executable path via <see cref="ClaudeInstall.OwnsExecutable"/>, and the
/// path is checked again on the handle being terminated, so the Claude Code CLI is never touched.
/// </summary>
public sealed class ClaudeCloser(ProcessScanner scanner, IProcessTerminator terminator)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public IReadOnlyList<ProcessSnapshot> DesktopProcesses(ClaudeInstall install) => scanner.DesktopProcesses(install);

    /// <summary>Returns true once no Claude Desktop process is left, false on timeout.</summary>
    public async Task<bool> CloseAllAsync(
        ClaudeInstall install,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        var poll = pollInterval ?? TimeSpan.FromMilliseconds(250);

        while (true)
        {
            var remaining = scanner.DesktopProcesses(install);
            if (remaining.Count == 0)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            // Main processes first so Electron doesn't try to respawn crashed children.
            foreach (var p in remaining.OrderBy(p => IsChild(p) ? 1 : 0))
            {
                terminator.Terminate(p.ProcessId, install.OwnsExecutable);
            }

            await Task.Delay(poll, cancellationToken);
        }
    }

    private static bool IsChild(ProcessSnapshot p) =>
        p.CommandLine?.Contains(" --type=", StringComparison.Ordinal) ?? false;
}
