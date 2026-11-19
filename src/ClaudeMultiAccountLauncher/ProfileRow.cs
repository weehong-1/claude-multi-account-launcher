using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher;

/// <summary>One row in the profile list (FR-1). <see cref="Profile"/> null is the built-in Default.</summary>
public sealed partial class ProfileRow : INotifyPropertyChanged
{
    private Profile? _profile;
    private bool _isRunning;
    private bool _canLaunch;

    public ProfileRow(Profile? profile, string folderText)
    {
        _profile = profile;
        FolderText = folderText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity across refreshes. Empty for Default.</summary>
    public string Key => _profile?.Slug ?? string.Empty;

    public Profile? Profile
    {
        get => _profile;
        set
        {
            if (_profile == value)
            {
                return;
            }

            _profile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Name));
            OnStatusChanged();
        }
    }

    public bool IsDefault => _profile is null;

    public bool IsCustom => !IsDefault;

    public string Name => _profile?.Name ?? ProfileService.DefaultProfileName;

    public string FolderText { get; }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnStatusChanged();
        }
    }

    public bool IsNotRunning => !IsRunning;

    /// <summary>FR-1: only whether it is running; there is no stored sign-in state.</summary>
    public string StatusText => IsRunning ? "Running" : "Not running";

    /// <summary>False while Claude Desktop can't be found (FR-6).</summary>
    public bool CanLaunch
    {
        get => _canLaunch;
        set
        {
            if (_canLaunch != value)
            {
                _canLaunch = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Removing is blocked while running so folders are never deleted under Claude.</summary>
    public bool CanRemove => IsCustom && !IsRunning;

    private void OnStatusChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsNotRunning));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanRemove));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
