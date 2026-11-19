using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ClaudeMultiAccountLauncher.Core.Shortcuts;

/// <summary>What a .lnk file contains, as far as the launcher cares.</summary>
public sealed record ShellLinkInfo(string TargetPath, string Arguments, string WorkingDirectory, string Description, string IconPath);

/// <summary>Reads and writes .lnk files through the Windows shell (IShellLinkW).</summary>
internal static class ShellLink
{
    private const int MaxPath = 32768;

    public static void Write(string lnkPath, ShellLinkInfo info)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(info.TargetPath);
            link.SetArguments(info.Arguments);
            link.SetWorkingDirectory(info.WorkingDirectory);
            link.SetDescription(info.Description);
            if (info.IconPath.Length > 0)
            {
                link.SetIconLocation(info.IconPath, 0);
            }

            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    public static ShellLinkInfo Read(string lnkPath)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);

            var target = new StringBuilder(MaxPath);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0x4 /* SLGP_RAWPATH */);
            var args = new StringBuilder(MaxPath);
            link.GetArguments(args, args.Capacity);
            var dir = new StringBuilder(MaxPath);
            link.GetWorkingDirectory(dir, dir.Capacity);
            var desc = new StringBuilder(1024);
            link.GetDescription(desc, desc.Capacity);
            var icon = new StringBuilder(MaxPath);
            link.GetIconLocation(icon, icon.Capacity, out _);

            return new ShellLinkInfo(target.ToString(), args.ToString(), dir.ToString(), desc.ToString(), icon.ToString());
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);

        void GetIDList(out IntPtr idList);

        void SetIDList(IntPtr idList);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCmd);

        void SetShowCmd(int showCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int cch, out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

        void Resolve(IntPtr hwnd, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
