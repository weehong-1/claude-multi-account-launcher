using System.Management;

namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>A process named claude.exe; may be Claude Desktop or the Claude Code CLI.</summary>
public sealed record ProcessSnapshot(int ProcessId, string? ExecutablePath, string? CommandLine);

/// <summary>A running Claude Desktop main process.</summary>
/// <param name="UserDataDir">Value of <c>--user-data-dir</c>, or null for the Default profile.</param>
public sealed record RunningInstance(int ProcessId, string? UserDataDir);

public interface IProcessSource
{
    IReadOnlyList<ProcessSnapshot> GetClaudeProcesses();
}

/// <summary>Reads executable paths and command lines through WMI.</summary>
public sealed class WmiProcessSource : IProcessSource
{
    public IReadOnlyList<ProcessSnapshot> GetClaudeProcesses()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE Name = 'claude.exe'");
        using var results = searcher.Get();

        var list = new List<ProcessSnapshot>();
        foreach (var obj in results)
        {
            using (obj)
            {
                list.Add(new ProcessSnapshot(
                    Convert.ToInt32(obj["ProcessId"]),
                    obj["ExecutablePath"] as string,
                    obj["CommandLine"] as string));
            }
        }

        return list;
    }
}

/// <summary>Finds running Claude Desktop instances (FR-7).</summary>
public sealed class ProcessScanner(IProcessSource source)
{
    private const string UserDataDirSwitch = "--user-data-dir=";

    /// <summary>Every Claude Desktop process of this install, children included. Never the CLI.</summary>
    public IReadOnlyList<ProcessSnapshot> DesktopProcesses(ClaudeInstall install) =>
        source.GetClaudeProcesses().Where(p => install.OwnsExecutable(p.ExecutablePath)).ToList();

    /// <summary>One entry per running Desktop instance: main processes only (no <c>--type=</c>).</summary>
    public IReadOnlyList<RunningInstance> RunningInstances(ClaudeInstall install)
    {
        var instances = new List<RunningInstance>();
        foreach (var p in DesktopProcesses(install))
        {
            if (p.CommandLine is null)
            {
                continue; // Can't tell which profile; skip rather than guess.
            }

            var args = CommandLine.Split(p.CommandLine);
            if (args.Skip(1).Any(a => a.StartsWith("--type=", StringComparison.Ordinal)))
            {
                continue; // Electron child process (renderer, gpu, utility...)
            }

            var dataDir = args.Skip(1)
                .LastOrDefault(a => a.StartsWith(UserDataDirSwitch, StringComparison.OrdinalIgnoreCase))?[UserDataDirSwitch.Length..];

            instances.Add(new RunningInstance(p.ProcessId, string.IsNullOrEmpty(dataDir) ? null : dataDir));
        }

        return instances;
    }

    /// <summary>Compares two folder paths the way Windows does (case-insensitive, trailing slash ignored).</summary>
    public static bool SameFolder(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}
