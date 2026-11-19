namespace ClaudeMultiAccountLauncher.Core.Profiles;

/// <summary>
/// A user-added profile. The built-in Default profile is not stored (FR-1).
/// No sign-in state is kept: whether it is running is detected (FR-7), and signing in
/// happens in the profile's own Claude window (FR-4). Older profiles.json files carry a
/// "status" field; it is ignored on load and dropped on the next save.
/// </summary>
public sealed record Profile(string Name, string Slug, DateTimeOffset CreatedAt);
