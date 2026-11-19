using ClaudeMultiAccountLauncher.Core.Claude;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData("a b c", new[] { "a", "b", "c" })]
    [InlineData("  a   b  ", new[] { "a", "b" })]
    [InlineData("\"a b\" c", new[] { "a b", "c" })]
    [InlineData("\"--x=C:\\a b\\c\"", new[] { "--x=C:\\a b\\c" })]
    [InlineData("--x=\"C:\\a b\"", new[] { "--x=C:\\a b" })]
    [InlineData("a\\\\\"b c\"", new[] { "a\\b c" })]   // 2 backslashes + quote -> 1 backslash, quote toggles
    [InlineData("a\\\"b", new[] { "a\"b" })]          // 1 backslash + quote -> literal quote
    [InlineData("C:\\dir\\ x", new[] { "C:\\dir\\", "x" })]
    [InlineData("\"\"", new[] { "" })]
    [InlineData("", new string[0])]
    public void Splits_like_CommandLineToArgvW(string input, string[] expected) =>
        Assert.Equal(expected, CommandLine.Split(input));
}
