using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeMultiAccountLauncher.Core.Claude;

public enum TerminateOutcome
{
    /// <summary>The process was ended, or had already exited.</summary>
    Gone,

    /// <summary>The process is something else (path check failed) and was left alone.</summary>
    Skipped,

    /// <summary>Opening or ending the process failed.</summary>
    Failed,
}

public interface IProcessTerminator
{
    /// <summary>
    /// Ends <paramref name="processId"/> only if <paramref name="isAllowed"/> accepts its executable path.
    /// The path is checked on the same handle that is terminated, so a recycled PID can't be hit.
    /// </summary>
    TerminateOutcome Terminate(int processId, Func<string?, bool> isAllowed);
}

/// <summary>Win32 implementation: open, verify image path, terminate, all on one handle.</summary>
public sealed partial class Win32ProcessTerminator : IProcessTerminator
{
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInvalidParameter = 87; // OpenProcess on a PID that no longer exists

    public TerminateOutcome Terminate(int processId, Func<string?, bool> isAllowed)
    {
        var handle = OpenProcess(ProcessTerminate | ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            return Marshal.GetLastPInvokeError() == ErrorInvalidParameter ? TerminateOutcome.Gone : TerminateOutcome.Failed;
        }

        try
        {
            if (!isAllowed(ImagePath(handle)))
            {
                return TerminateOutcome.Skipped;
            }

            return TerminateProcess(handle, 1) ? TerminateOutcome.Gone : TerminateOutcome.Failed;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static unsafe string? ImagePath(IntPtr handle)
    {
        var buffer = new char[32768];
        var size = (uint)buffer.Length;
        fixed (char* p = buffer)
        {
            return QueryFullProcessImageNameW(handle, 0, p, ref size) ? new string(p, 0, (int)size) : null;
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(IntPtr handle, uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(IntPtr handle, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
