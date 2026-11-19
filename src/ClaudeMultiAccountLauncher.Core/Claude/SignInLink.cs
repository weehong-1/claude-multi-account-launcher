namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>
/// A Claude sign-in callback link, e.g. <c>claude://login/google-auth?code=...</c>, copied from
/// the browser's "Open Claude" button. It carries a one-time code: never log or store it.
/// Only <c>claude://login/</c> links are accepted, so the launcher can't be used to push
/// arbitrary links into Claude.
/// </summary>
public sealed class SignInLink
{
    public const string Prefix = "claude://login/";
    private const int MaxLength = 4096;

    private SignInLink(string value) => Value = value;

    public string Value { get; }

    /// <summary>Accepts clipboard text holding exactly one sign-in link (surrounding whitespace ok).</summary>
    public static bool TryParse(string? text, out SignInLink? link)
    {
        link = null;
        var candidate = text?.Trim();
        if (string.IsNullOrEmpty(candidate)
            || candidate.Length > MaxLength
            || !candidate.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || candidate.Length == Prefix.Length
            || candidate.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"'))
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme != "claude")
        {
            return false;
        }

        link = new SignInLink(candidate);
        return true;
    }

    /// <summary>The code must not end up in logs or error messages.</summary>
    public override string ToString() => Prefix + "…";
}
