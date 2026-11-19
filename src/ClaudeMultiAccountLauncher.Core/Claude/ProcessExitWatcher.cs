using System.ComponentModel;
using System.Diagnostics;

namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>
/// Raises <see cref="Exited"/> as soon as a watched process ends, so the Running status
/// clears the moment a Claude window is closed instead of on the next refresh (FR-7).
/// </summary>
public sealed class ProcessExitWatcher : IDisposable
{
    private readonly Dictionary<int, Process> _watched = [];
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>Raised on a thread-pool thread with the process id that ended.</summary>
    public event Action<int>? Exited;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _watched.Count;
            }
        }
    }

    /// <summary>Starts watching any of these processes not already watched.</summary>
    public void Watch(IEnumerable<int> processIds)
    {
        foreach (var pid in processIds)
        {
            Process process;
            lock (_lock)
            {
                if (_disposed || _watched.ContainsKey(pid))
                {
                    continue;
                }

                try
                {
                    process = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    // Already gone: report it so the caller refreshes.
                    process = null!;
                }

                if (process is not null)
                {
                    _watched[pid] = process;
                }
            }

            if (process is null)
            {
                Exited?.Invoke(pid);
                continue;
            }

            try
            {
                process.Exited += (_, _) => OnExited(pid);
                process.EnableRaisingEvents = true;
                if (process.HasExited)
                {
                    OnExited(pid); // ended between lookup and subscribe
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // Can't wait on it (access or already reaped); the periodic refresh covers it.
                Forget(pid);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            foreach (var p in _watched.Values)
            {
                p.Dispose();
            }

            _watched.Clear();
        }
    }

    private void OnExited(int pid)
    {
        if (Forget(pid))
        {
            Exited?.Invoke(pid);
        }
    }

    /// <summary>Stops watching; true only for the first caller, so each exit is reported once.</summary>
    private bool Forget(int pid)
    {
        lock (_lock)
        {
            if (!_watched.Remove(pid, out var process))
            {
                return false;
            }

            process.Dispose();
            return true;
        }
    }
}
