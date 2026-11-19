using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;
using ClaudeMultiAccountLauncher.Core.Startup;
using Microsoft.UI.Xaml;

namespace ClaudeMultiAccountLauncher;

/// <summary>What the window should do once it opens, when started from a shortcut.</summary>
public abstract record StartupAction
{
    public sealed record ShowError(string Message) : StartupAction;
}

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var command = StartupCommand.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
        StartupAction? action = command.Error is { } error ? new StartupAction.ShowError(error) : null;

        // FR-8: a shortcut runs "--launch <slug>". Start Claude and quit without a window,
        // unless something went wrong.
        if (command.LaunchSlug is { } slug)
        {
            var paths = LauncherPaths.ForCurrentUser();
            var launcher = new ClaudeLauncher(paths);
            var outcome = new QuickLauncher(
                new ProfileStore(paths.ProfilesFile),
                new ClaudeLocator(paths, new SystemInstallProbe()),
                (install, profile) =>
                {
                    using var _ = launcher.Launch(install, profile);
                }).Run(slug);

            switch (outcome)
            {
                case QuickLaunchOutcome.Launched:
                    Exit();
                    return;
                case QuickLaunchOutcome.Failed failed:
                    action = new StartupAction.ShowError(failed.Message);
                    break;
            }
        }

        _window = new MainWindow(action);
        _window.Activate();
    }
}
