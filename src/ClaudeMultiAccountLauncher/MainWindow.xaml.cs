using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace ClaudeMultiAccountLauncher;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 560;
    private const int DefaultHeight = 640;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // AppWindow sizes are in physical pixels; scale the logical size by the monitor DPI.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
