using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace ClaudeMultiAccountLauncher;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 560;
    private const int DefaultHeight = 720;

    private readonly LauncherPaths _paths = LauncherPaths.ForCurrentUser();
    private readonly IProcessSource _processSource = new WmiProcessSource();
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // AppWindow sizes are in physical pixels; scale the logical size by the monitor DPI.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));

        // FR-7: refresh when the window gets focus
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _ = RefreshAsync();
            }
        };
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        RefreshButton.IsEnabled = false;
        try
        {
            // Package lookup and WMI are slow-ish; keep them off the UI thread.
            var snapshot = await Task.Run(TakeSnapshot);
            Show(snapshot);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _refreshing = false;
        }
    }

    private Snapshot TakeSnapshot()
    {
        var errors = new List<string>();

        IReadOnlyList<Profile> profiles = [];
        try
        {
            profiles = new ProfileStore(_paths.ProfilesFile).Load();
        }
        catch (ProfileStoreException ex)
        {
            errors.Add(ex.Message);
        }

        ClaudeInstall? install = null;
        try
        {
            install = new ClaudeLocator(_paths, new SystemInstallProbe()).Locate();
        }
        catch (ClaudeNotFoundException ex)
        {
            errors.Add(ex.Message);
        }

        IReadOnlyList<RunningInstance> running = [];
        var ignored = 0;
        if (install is not null)
        {
            var scanner = new ProcessScanner(_processSource);
            running = scanner.RunningInstances(install);
            ignored = _processSource.GetClaudeProcesses().Count - scanner.DesktopProcesses(install).Count;
        }

        return new Snapshot(install, profiles, running, ignored, errors);
    }

    private void Show(Snapshot s)
    {
        ErrorBar.IsOpen = s.Errors.Count > 0;
        ErrorBar.Message = string.Join(Environment.NewLine + Environment.NewLine, s.Errors);

        InstallKindText.Text = s.Install?.Kind.ToString() ?? "Not found";
        LaunchPathText.Text = s.Install?.LaunchPath ?? "—";
        InstallFolderText.Text = s.Install?.InstallFolder ?? "—";

        ProfilesFileText.Text = _paths.ProfilesFile;
        ProfilesCountText.Text = s.Profiles.Count == 1 ? "1 profile saved" : $"{s.Profiles.Count} profiles saved";

        RunningSummaryText.Text = s.Running.Count switch
        {
            0 => "None running.",
            1 => "1 instance:",
            var n => $"{n} instances:",
        };
        RunningList.ItemsSource = s.Running.Select(r => Describe(r, s.Profiles)).ToList();
        IgnoredText.Text = $"Ignored {s.IgnoredProcesses} other claude.exe process(es), e.g. the Claude Code CLI.";
    }

    private string Describe(RunningInstance r, IReadOnlyList<Profile> profiles)
    {
        if (r.UserDataDir is null)
        {
            return $"Default   pid {r.ProcessId}";
        }

        var match = profiles.FirstOrDefault(p => ProcessScanner.SameFolder(_paths.DataFolder(p.Slug), r.UserDataDir));
        var label = match?.Name ?? "Unknown profile";
        return $"{label}   pid {r.ProcessId}{Environment.NewLine}  {r.UserDataDir}";
    }

    private sealed record Snapshot(
        ClaudeInstall? Install,
        IReadOnlyList<Profile> Profiles,
        IReadOnlyList<RunningInstance> Running,
        int IgnoredProcesses,
        IReadOnlyList<string> Errors);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
