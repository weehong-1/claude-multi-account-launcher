using System.Collections.ObjectModel;
using System.ComponentModel;
using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;
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

    private ClaudeInstall? _install;
    private string? _installError;
    private string? _storeError;
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();

        _profiles = new ProfileService(new ProfileStore(_paths.ProfilesFile), _paths);
        _launcher = new ClaudeLauncher(_paths);
        ProfileList.ItemsSource = _rows;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // AppWindow sizes are in physical pixels; scale the logical size by the monitor DPI.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));

        LoadProfiles();

        // FR-7: refresh when the window gets focus
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _ = RefreshAsync();
            }
        };
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

        var profile = row.Profile;
        if (profile?.Status == ProfileStatus.NotSignedIn && !row.IsRunning)
        {
            // Temporary until Phase 3 adds the guided first sign-in (FR-4).
            var choice = await NewDialog(
                $"Launch \"{profile.Name}\"?",
                "The guided first sign-in arrives in Phase 3. You can launch this profile now to check it opens, but don't sign in yet: while another Claude is running, the sign-in link goes to that one.",
                primary: "Launch anyway",
                close: "Cancel").ShowAsync();
            if (choice != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            using var _ = _launcher.Launch(_install, profile);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't start Claude: {ex.Message}");
            return;
        }

        // Claude takes a moment to start; refresh so Running shows without refocusing.
        await Task.Delay(TimeSpan.FromSeconds(3));
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

        try
        {
            _profiles.Rename(profile.Slug, name);
        }
        catch (Exception ex) when (ex is ProfileValidationException or IOException or UnauthorizedAccessException)
        {
            ShowErrors($"Couldn't rename the profile: {ex.Message}");
            return;
        }

        await RefreshAsync();
    }

    private async void RemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ProfileRow { Profile: { } profile } row || row.IsRunning)
        {
            return;
        }

        var confirm = await NewDialog(
            $"Remove \"{profile.Name}\"?",
            "It will no longer appear in the launcher.",
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
        if (result.FailedFolders.Count > 0)
        {
            ShowErrors("Removed the profile, but couldn't delete these folders (maybe in use). Delete them by hand:"
                + Environment.NewLine + string.Join(Environment.NewLine, result.FailedFolders));
        }
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
