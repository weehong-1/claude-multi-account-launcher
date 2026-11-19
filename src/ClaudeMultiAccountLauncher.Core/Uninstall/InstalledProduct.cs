using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeMultiAccountLauncher.Core.Uninstall;

/// <summary>Finds the launcher's own MSI install, if there is one.</summary>
public interface IInstalledProduct
{
    /// <summary>Product code of the installed MSI, or null when not installed (portable use).</summary>
    string? FindProductCode();
}

public static class InstallerInfo
{
    /// <summary>Must match <c>UpgradeCode</c> in installer/Package.wxs. Never changes.</summary>
    public const string UpgradeCode = "{01B9B86C-279A-46EE-A998-5AC108B551E9}";

    /// <summary>Where the MSI installs, relative to %LOCALAPPDATA% (installer/Package.wxs).</summary>
    public const string InstallFolderRelative = @"Programs\claude-multi-account-launcher";
}

/// <summary>Asks Windows Installer which product carries our upgrade code.</summary>
public sealed class MsiInstalledProduct : IInstalledProduct
{
    public string? FindProductCode()
    {
        var buffer = new StringBuilder(39);
        try
        {
            return MsiEnumRelatedProductsW(InstallerInfo.UpgradeCode, 0, 0, buffer) == 0 ? buffer.ToString() : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiEnumRelatedProductsW(string upgradeCode, uint reserved, uint index, StringBuilder productCode);
}
