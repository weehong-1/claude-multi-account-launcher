using ClaudeMultiAccountLauncher.Core.Claude;
using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public class SignInLinkTests
{
    // Shape seen in the 2026-11-19 test; the code here is made up
    private const string GoogleLink = "claude://login/google-auth?code=4%2F0AbCdEf&anon_id=claudeai.v1.abc&hop_nonce=xyz";

    [Theory]
    [InlineData(GoogleLink)]
    [InlineData("  " + GoogleLink + "\r\n")]
    [InlineData("CLAUDE://login/magic-link?token=abc")]
    public void Accepts_claude_login_links(string text)
    {
        Assert.True(SignInLink.TryParse(text, out var link));
        Assert.Equal(text.Trim(), link!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("https://claude.ai/login/popup-google-auth")]
    [InlineData("claude://login/")]
    [InlineData("claude://settings/connectors")]
    [InlineData("claude://login/x y")]
    [InlineData("claude://login/x\"&calc")]
    [InlineData("claude://login/a\nclaude://login/b")]
    public void Rejects_anything_else(string? text) =>
        Assert.False(SignInLink.TryParse(text, out _));

    [Fact]
    public void Rejects_absurdly_long_text() =>
        Assert.False(SignInLink.TryParse("claude://login/" + new string('a', 5000), out _));

    [Fact]
    public void ToString_hides_the_code()
    {
        SignInLink.TryParse(GoogleLink, out var link);
        Assert.DoesNotContain("code", link!.ToString());
    }

    [Fact]
    public void Launch_passes_the_link_to_the_profile_like_Windows_does_for_Default()
    {
        var paths = new LauncherPaths(@"C:\U\AppData\Roaming", @"C:\U\AppData\Local", @"C:\U");
        var install = new ClaudeInstall(InstallKind.Msix, @"C:\WA\claude-desktop.exe", @"C:\P\Claude_1_x64__pzs8sxrjxfjjc");
        var profile = new Profile("Personal", "personal", DateTimeOffset.UnixEpoch);
        SignInLink.TryParse(GoogleLink, out var link);

        var psi = new ClaudeLauncher(paths).BuildStartInfo(install, profile, link);

        Assert.Equal([@"--user-data-dir=C:\U\AppData\Roaming\Claude-personal", "--", GoogleLink], psi.ArgumentList);
        Assert.Equal(@"C:\U\.claude-personal", psi.Environment[ClaudeLauncher.ConfigDirVariable]);
    }
}
