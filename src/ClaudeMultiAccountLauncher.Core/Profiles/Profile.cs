namespace ClaudeMultiAccountLauncher.Core.Profiles;

/// <summary>Stored sign-in state. "Running" is never stored; it is detected (FR-7).</summary>
public enum ProfileStatus
{
    NotSignedIn,
    Ready,
}

/// <summary>A user-added profile. The built-in Default profile is not stored (FR-1).</summary>
public sealed record Profile(string Name, string Slug, ProfileStatus Status, DateTimeOffset CreatedAt);
