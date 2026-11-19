using System.Text;

namespace ClaudeMultiAccountLauncher.Core.Claude;

/// <summary>Splits a Windows command line the way CommandLineToArgvW / the MSVC runtime do.</summary>
internal static class CommandLine
{
    public static List<string> Split(string commandLine)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        var i = 0;

        while (i < commandLine.Length)
        {
            var c = commandLine[i];

            if (c == '\\')
            {
                var slashes = 0;
                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    slashes++;
                    i++;
                }

                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    // 2n backslashes + quote -> n backslashes, quote toggles
                    // 2n+1 backslashes + quote -> n backslashes + literal quote
                    current.Append('\\', slashes / 2);
                    if (slashes % 2 == 1)
                    {
                        current.Append('"');
                        i++;
                    }
                }
                else
                {
                    current.Append('\\', slashes);
                }

                hasToken = true;
                continue;
            }

            if (c == '"')
            {
                // "" inside quotes is a literal quote
                if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                {
                    current.Append('"');
                    i += 2;
                    continue;
                }

                inQuotes = !inQuotes;
                hasToken = true;
                i++;
                continue;
            }

            if (!inQuotes && (c == ' ' || c == '\t'))
            {
                if (hasToken)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                i++;
                continue;
            }

            current.Append(c);
            hasToken = true;
            i++;
        }

        if (hasToken)
        {
            args.Add(current.ToString());
        }

        return args;
    }
}
