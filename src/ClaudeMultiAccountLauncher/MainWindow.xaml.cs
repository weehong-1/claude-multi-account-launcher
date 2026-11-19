using System.Collections.ObjectModel;
using System.ComponentModel;
using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;
using ClaudeMultiAccountLauncher.Core.Shortcuts;
using ClaudeMultiAccountLauncher.Core.Startup;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ClaudeMultiAccountLauncher;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 560;
    private const int DefaultHeight = 640;

    private readonly LauncherPaths _paths = LauncherPaths.ForCurrentUser();
    private readonly ProfileService _profiles;
    private readonly ClaudeLauncher _launcher;
    private readonly ProcessScanner _scanner = new(new WmiProcessSource());
    private readonly ObservableCollection<ProfileRow> _rows = [];

    private readonly ShortcutService _shortcuts = new(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.ProcessPath ?? string.Empty);

    private ClaudeInstall? _install;
    private string? _installError;
    private string? _storeError;
    private bool _refreshing;
    private StartupAction? _pendingStartup;

    public MainWindow(StartupAction? startup = null)
    {
        InitializeComponent();

        _profiles = new ProfileService(new ProfileStore(_paths.ProfilesFile), _paths);
        _launcher = new ClaudeLauncher(_paths);
        _pendingStartup = startup;
        ProfileList.ItemsSource = _rows;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Taskbar and Alt+Tab icon. The exe also embeds it (ApplicationIcon) for Explorer.
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icon))
        {
            AppWindow.SetIcon(icon);
        }

        // AppWindow sizes are in physical pixels; scale the logical size by the monitor DPI.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));

        LoadProfiles();

        // FR-7: refresh when the window gets focus
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _ = OnActivatedAsync();
            }
        };
    }

    private async Task OnActivatedAsync()
    {
        await RefreshAsync();

        // Opened from a shortcut that needs the user (FR-8): run it once, after the first refresh.
        if (_pendingStartup is not { } startup || !Root.IsLoaded)
        {
            return;
        }

        _pendingStartup = null;
        switch (startup)
        {
            case StartupAction.ShowError error:
                ShowErrors(error.Message);
                break;
            case StartupAction.SignIn signIn when _profiles.Find(signIn.Slug) is { } profile && _install is not null:
                await FirstSignInAsync(_install, profile);
                break;
        }
    }

    // ---------- Loading and refreshing ----------

    private void LoadProfiles()
    {
        try
        {
            _profiles.Load();
            _storeError = null;
        }
        catch (ProfileStoreException ex)
        {
            // Never overwrite a file we couldn't read: block changes until it's fixed.
            _storeError = ex.Message + Environment.NewLine + "Fix or move the file, then reopen the launcher.";
        }

        AddButton.IsEnabled = _storeError is null;
        SyncRows([]);
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            // Package lookup and WMI are slow-ish; keep them off the UI thread.
            var (install, error, running) = await Task.Run(() =>
            {
                try
                {
                    var found = new ClaudeLocator(_paths, new SystemInstallProbe()).Locate();
                    return (found, (string?)null, _scanner.RunningInstances(found));
                }
                catch (ClaudeNotFoundException ex)
                {
                    return ((ClaudeInstall?)null, ex.Message, (IReadOnlyList<RunningInstance>)[]);
                }
            });

            _install = install;
            _installError = error;
            SyncRows(running);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Updates rows in place so focus and open menus survive a refresh.</summary>
    private void SyncRows(IReadOnlyList<RunningInstance> running)
    {
        var wanted = new List<Profile?> { null };
        wanted.AddRange(_profiles.Profiles);

        // Drop rows whose profile is gone
        for (var i = _rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Any(p => (p?.Slug ?? string.Empty) == _rows[i].Key))
            {
                _rows.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var profile = wanted[i];
            var key = profile?.Slug ?? string.Empty;
            var row = _rows.FirstOrDefault(r => r.Key == key);

            if (row is null)
            {
                row = new ProfileRow(profile, profile is null ? "Claude's own data folder" : _paths.DataFolder(profile.Slug));
                _rows.Insert(i, row);
            }
            else
            {
                var at = _rows.IndexOf(row);
                if (at != i)
                {
                    _rows.Move(at, i);
                }

                row.Profile = profile;
            }

            row.IsRunning = RunningProfiles.IsRunning(profile, running, _paths);
            row.CanLaunch = _install is not null;
        }

        ShowErrors();
        FooterText.Text = _install is null
            ? string.Empty
            : $"Claude Desktop ({_install.Kind}): {_install.LaunchPath}";
    }

    private void ShowErrors(string? actionError = null)
    {
        var errors = new[] { actionError, _storeError, _installError }.Where(e => e is not null).ToList();
        ErrorBar.Message = string.Join(Environment.NewLine + Environment.NewLine, errors);
        ErrorBar.IsOpen = errors.Count > 0;
    }

    // ---------- Launch (FR-3) ----------

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow row || _install is null)
        {
            return;
        }

        if (row.Profile is { Status: ProfileStatus.NotSignedIn } profile)
        {
            await FirstSignInAsync(_install, profile);
        }
        else if (StartClaude(_install, row.Profile))
        {
            await RefreshSoonAsync();
        }
    }

    private bool StartClaude(ClaudeInstall install, Profile? profile)
    {
        try
        {
            using var _ = _launcher.Launch(install, profile);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't start Claude: {ex.Message}");
            return false;
        }
    }

    /// <summary>Claude takes a moment to start; refresh so Running shows without refocusing.</summary>
    private async Task RefreshSoonAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        await RefreshAsync();
    }

    // ---------- First sign-in (FR-4) ----------

    /// <summary>
    /// Sign-in returns through a claude:// link that Windows hands to whichever Claude is already
    /// running, so every other Claude Desktop must be closed before the new profile starts.
    /// </summary>
    private async Task FirstSignInAsync(ClaudeInstall install, Profile profile)
    {
        var closer = new ClaudeCloser(_scanner, new Win32ProcessTerminator());

        // 1. Anything running? Ask before closing it.
        var running = await Task.Run(() => closer.DesktopProcesses(install));
        if (running.Count > 0)
        {
            var confirm = await NewDialog(
                "Close Claude to sign in?",
                $"To sign in to \"{profile.Name}\", every Claude Desktop window has to close first. "
                + "Otherwise the sign-in link opens in the Claude that's already running."
                + Environment.NewLine + Environment.NewLine
                + "Anything unsent in open Claude windows will be lost, and Claude Code sessions inside Claude Desktop will stop. "
                + "Claude Code in a terminal keeps running.",
                primary: "Close Claude and continue",
                close: "Cancel").ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            // 2. End all Claude Desktop processes (by exe path, never the CLI) and wait until gone.
            SetBusy(true, "Closing Claude…");
            bool closed;
            try
            {
                closed = await Task.Run(() => closer.CloseAllAsync(install));
            }
            finally
            {
                SetBusy(false);
            }

            if (!closed)
            {
                ShowErrors("Couldn't close every Claude window. Quit Claude from its tray icon, then try again.");
                await RefreshAsync();
                return;
            }
        }

        // 3. Launch the new profile only.
        if (!StartClaude(install, profile))
        {
            return;
        }

        // 4. Ask whether sign-in worked.
        var signedIn = await NewDialog(
            "Signed in successfully?",
            $"Sign in to the account for \"{profile.Name}\" in the Claude window that just opened, then come back here.",
            primary: "Yes",
            close: "Not yet").ShowAsync();

        // 5. Yes marks it Ready; Not yet keeps it as is, so the next Launch runs this flow again.
        if (signedIn == ContentDialogResult.Primary)
        {
            TryChange(() => _profiles.SetStatus(profile.Slug, ProfileStatus.Ready), "update the profile");
        }

        await RefreshAsync();
    }

    private void SetBusy(bool busy, string? message = null)
    {
        BusyBar.IsOpen = busy;
        BusyBar.Message = message ?? string.Empty;
        AddButton.IsEnabled = !busy && _storeError is null;
        ProfileList.IsHitTestVisible = !busy;
    }

    private bool TryChange(Action change, string what)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex) when (ex is ProfileValidationException or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't {what}: {ex.Message}");
            return false;
        }
    }

    // ---------- Reset sign-in (FR-4) ----------

    private async void ResetSignInMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow { Profile: { } profile })
        {
            return;
        }

        TryChange(() => _profiles.SetStatus(profile.Slug, ProfileStatus.NotSignedIn), "reset the profile");
        await RefreshAsync();
    }

    // ---------- Add (FR-2) ----------

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = await AskForName("Add profile", "Add", initial: string.Empty, renamingSlug: null);
        if (name is null)
        {
            return;
        }

        try
        {
            _profiles.Add(name);
        }
        catch (Exception ex) when (ex is ProfileValidationException or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't add the profile: {ex.Message}");
            return;
        }

        await RefreshAsync();
    }

    // ---------- Rename and remove (FR-5) ----------

    private async void RenameMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow { Profile: { } profile })
        {
            return;
        }

        var name = await AskForName($"Rename \"{profile.Name}\"", "Rename", profile.Name, profile.Slug);
        if (name is null || name.Trim() == profile.Name)
        {
            return;
        }

        Profile renamed;
        try
        {
            renamed = _profiles.Rename(profile.Slug, name);
        }
        catch (Exception ex) when (ex is ProfileValidationException or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't rename the profile: {ex.Message}");
            return;
        }

        await RefreshAsync();

        // Keep the desktop shortcut's name in step with the profile.
        var shortcutError = TryShortcut(() => _shortcuts.Rename(renamed.Slug, renamed.Name, ClaudeIcon.Ensure(_paths, _install)));
        if (shortcutError is not null)
        {
            ShowErrors($"Renamed the profile, but couldn't update its desktop shortcut: {shortcutError}");
        }
    }

    private async void RemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow { Profile: { } profile } row || row.IsRunning)
        {
            return;
        }

        var hasShortcut = _shortcuts.Find(profile.Slug) is not null;
        var confirm = await NewDialog(
            $"Remove \"{profile.Name}\"?",
            hasShortcut
                ? "It will no longer appear in the launcher, and its desktop shortcut will be deleted."
                : "It will no longer appear in the launcher.",
            primary: "Remove",
            close: "Cancel").ShowAsync();
        if (confirm != ContentDialogResult.Primary)
        {
            return;
        }

        // Second confirmation; keeping the folders is the default (FR-5).
        var deleteFolders = false;
        var folders = _profiles.ExistingFolders(profile.Slug);
        if (folders.Count > 0)
        {
            var folderDialog = NewDialog(
                "Also delete its folders?",
                MonoList("This deletes the account's sign-in, chats, settings and Claude Code config on this PC:", folders),
                primary: "Keep folders",
                close: null,
                secondary: "Delete folders");
            deleteFolders = await folderDialog.ShowAsync() == ContentDialogResult.Secondary;
        }

        RemoveResult result;
        try
        {
            result = _profiles.Remove(profile.Slug, deleteFolders);
        }
        catch (Exception ex) when (ex is ProfileValidationException or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't remove the profile: {ex.Message}");
            return;
        }

        await RefreshAsync();

        var problems = new List<string>();
        if (result.FailedFolders.Count > 0)
        {
            problems.Add("Removed the profile, but couldn't delete these folders (maybe in use). Delete them by hand:"
                + Environment.NewLine + string.Join(Environment.NewLine, result.FailedFolders));
        }

        // A shortcut to a removed profile would only show an error, so it goes too.
        if (TryShortcut(() => _shortcuts.Delete(profile.Slug)) is { } shortcutError)
        {
            problems.Add($"Couldn't delete the profile's desktop shortcut: {shortcutError}");
        }

        if (problems.Count > 0)
        {
            ShowErrors(string.Join(Environment.NewLine + Environment.NewLine, problems));
        }
    }

    // ---------- Desktop shortcut (FR-8) ----------

    private void ShortcutMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow row)
        {
            return;
        }

        var slug = row.Profile?.Slug ?? StartupCommand.DefaultSlug;
        var icon = ClaudeIcon.Ensure(_paths, _install);
        string? path = null;
        var error = TryShortcut(() => path = _shortcuts.Create(row.Name, slug, icon));

        if (error is not null)
        {
            ShowErrors($"Couldn't create the desktop shortcut: {error}");
            return;
        }

        ShowNotice(icon is null
            ? $"Created \"{Path.GetFileName(path)}\" on your desktop. Claude's icon couldn't be copied, so it uses the launcher's icon."
            : $"Created \"{Path.GetFileName(path)}\" on your desktop.");
    }

    /// <summary>Runs a shortcut operation; returns an error message or null.</summary>
    private static string? TryShortcut(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (ShortcutConflictException ex)
        {
            return ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return ex.Message;
        }
    }

    private void ShowNotice(string message)
    {
        NoticeBar.Message = message;
        NoticeBar.IsOpen = true;
    }

    // ---------- Dialog helpers ----------

    /// <summary>Name prompt with live validation and a preview of the folders it will use.</summary>
    private async Task<string?> AskForName(string title, string primary, string initial, string? renamingSlug)
    {
        var box = new TextBox { Header = "Display name", PlaceholderText = "e.g. Personal", Text = initial };
        var error = new TextBlock
        {
            // Theme brush lives in the app's merged theme dictionaries, not Root.Resources
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        var preview = new TextBlock
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Root.Resources["MonoFont"],
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var dialog = NewDialog(title, new StackPanel { Spacing = 8, Children = { box, error, preview } }, primary, "Cancel");

        void Validate()
        {
            var message = _profiles.ValidateName(box.Text, renamingSlug);
            error.Text = box.Text.Length == 0 ? string.Empty : message ?? string.Empty;
            error.Visibility = string.IsNullOrEmpty(error.Text) ? Visibility.Collapsed : Visibility.Visible;
            dialog.IsPrimaryButtonEnabled = message is null;

            var slug = renamingSlug ?? _profiles.PreviewSlug(box.Text);
            preview.Text = renamingSlug is not null
                ? "Folders stay the same:" + Environment.NewLine + _paths.DataFolder(slug)
                : message is null
                    ? "Creates:" + Environment.NewLine + _paths.DataFolder(slug) + Environment.NewLine + _paths.ConfigFolder(slug)
                    : string.Empty;
        }

        box.TextChanged += (_, _) => Validate();
        dialog.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        Validate();

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    private ContentDialog NewDialog(string title, object content, string primary, string? close, string? secondary = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = title,
            Content = content is string text ? new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } : content,
            PrimaryButtonText = primary,
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = Root.ActualTheme,
        };

        if (close is not null)
        {
            dialog.CloseButtonText = close;
        }

        if (secondary is not null)
        {
            dialog.SecondaryButtonText = secondary;
        }

        return dialog;
    }

    private StackPanel MonoList(string intro, IEnumerable<string> lines)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock
        {
            Text = string.Join(Environment.NewLine, lines),
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Root.Resources["MonoFont"],
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        return panel;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
