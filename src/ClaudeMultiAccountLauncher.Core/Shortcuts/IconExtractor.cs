using System.Runtime.InteropServices;

namespace ClaudeMultiAccountLauncher.Core.Shortcuts;

/// <summary>Copies an exe's main icon (all sizes) into a standalone .ico file.</summary>
public static class IconExtractor
{
    private const uint LoadLibraryAsDatafile = 0x00000002;
    private const uint LoadLibraryAsImageResource = 0x00000020;
    private static readonly IntPtr RtIcon = 3;
    private static readonly IntPtr RtGroupIcon = 14;

    /// <summary>Writes the first icon group of <paramref name="exePath"/> to <paramref name="icoPath"/>.</summary>
    public static void ExtractMainIcon(string exePath, string icoPath)
    {
        var module = LoadLibraryExW(exePath, IntPtr.Zero, LoadLibraryAsDatafile | LoadLibraryAsImageResource);
        if (module == IntPtr.Zero)
        {
            throw new IOException($"Couldn't open {exePath} (error {Marshal.GetLastWin32Error()}).");
        }

        try
        {
            var groupName = FirstGroupIconName(module)
                ?? throw new InvalidDataException($"{exePath} has no icon.");

            var ico = IcoFile.FromResourceGroup(
                ReadResource(module, groupName, RtGroupIcon),
                id => ReadResource(module, new ResourceName(id, null), RtIcon));

            Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);
            var temp = icoPath + ".tmp";
            File.WriteAllBytes(temp, ico);
            File.Move(temp, icoPath, overwrite: true);
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    /// <summary>A resource name is either a numeric id or a string.</summary>
    private sealed record ResourceName(ushort Id, string? Text);

    private static ResourceName? FirstGroupIconName(IntPtr module)
    {
        ResourceName? found = null;
        EnumResourceNamesW(module, RtGroupIcon, (_, _, name, _) =>
        {
            found = name <= 0xFFFF ? new ResourceName((ushort)name, null) : new ResourceName(0, Marshal.PtrToStringUni(name));
            return false; // first one is the app icon
        }, IntPtr.Zero);
        return found;
    }

    private static byte[] ReadResource(IntPtr module, ResourceName name, IntPtr type)
    {
        var info = name.Text is null
            ? FindResourceW(module, (IntPtr)name.Id, type)
            : FindResourceW(module, name.Text, type);
        if (info == IntPtr.Zero)
        {
            throw new InvalidDataException($"Icon resource {name.Text ?? name.Id.ToString()} is missing.");
        }

        var size = SizeofResource(module, info);
        var data = LockResource(LoadResource(module, info));
        var bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, (int)size);
        return bytes;
    }

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string fileName, IntPtr reserved, uint flags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNamesW(IntPtr module, IntPtr type, EnumResNameProc callback, IntPtr param);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResourceW(IntPtr module, string name, IntPtr type);

    [DllImport("kernel32.dll")]
    private static extern uint SizeofResource(IntPtr module, IntPtr info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr data);
}
