using ClaudeMultiAccountLauncher.Core.Profiles;

namespace ClaudeMultiAccountLauncher.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cmal-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "sub", "profiles.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_means_no_profiles() =>
        Assert.Empty(new ProfileStore(FilePath).Load());

    [Fact]
    public void Round_trips_profiles_and_creates_the_folder()
    {
        var store = new ProfileStore(FilePath);
        var created = new DateTimeOffset(2026, 11, 19, 9, 30, 0, TimeSpan.FromHours(8));
        Profile[] profiles =
        [
            new("Personal", "personal", ProfileStatus.NotSignedIn, created),
            new("Resetrix", "resetrix", ProfileStatus.Ready, created),
        ];

        store.Save(profiles);

        Assert.Equal(profiles, store.Load());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Writes_the_spec_field_names()
    {
        var store = new ProfileStore(FilePath);
        store.Save([new("Personal", "personal", ProfileStatus.NotSignedIn, DateTimeOffset.UnixEpoch)]);

        var json = File.ReadAllText(FilePath);
        Assert.Contains("\"name\": \"Personal\"", json);
        Assert.Contains("\"slug\": \"personal\"", json);
        Assert.Contains("\"status\": \"notSignedIn\"", json);
        Assert.Contains("\"createdAt\":", json);
    }

    [Fact]
    public void Corrupt_file_throws_and_is_left_alone()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Throws<ProfileStoreException>(() => new ProfileStore(FilePath).Load());
        Assert.Equal("{ not json", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Newer_format_version_is_refused()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "version": 99, "profiles": [] }""");

        Assert.Throws<ProfileStoreException>(() => new ProfileStore(FilePath).Load());
    }
}
