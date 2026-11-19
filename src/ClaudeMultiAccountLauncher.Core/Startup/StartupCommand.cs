namespace ClaudeMultiAccountLauncher.Core.Startup;

/// <summary>Parsed launcher command line. Only <c>--launch &lt;slug&gt;</c> exists (FR-8).</summary>
public sealed record StartupCommand(string? LaunchSlug, string? Error)
{
    public const string LaunchSwitch = "--launch";

    /// <summary>Slug that targets the built-in Default profile; reserved by the slug generator.</summary>
    public const string DefaultSlug = "default";

    public static StartupCommand None { get; } = new(null, null);

    /// <param name="args">Arguments without the program path.</param>
    public static StartupCommand Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(LaunchSwitch + "=", StringComparison.OrdinalIgnoreCase))
            {
                return FromSlug(arg[(LaunchSwitch.Length + 1)..]);
            }

            if (string.Equals(arg, LaunchSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1 < args.Count
                    ? FromSlug(args[i + 1])
                    : new StartupCommand(null, "--launch needs a profile, e.g. --launch personal");
            }
        }

        return None;
    }

    private static StartupCommand FromSlug(string slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? new StartupCommand(null, "--launch needs a profile, e.g. --launch personal")
            : new StartupCommand(slug.Trim().ToLowerInvariant(), null);
}
