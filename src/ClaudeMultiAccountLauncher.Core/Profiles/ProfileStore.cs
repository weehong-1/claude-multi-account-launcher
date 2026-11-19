using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMultiAccountLauncher.Core.Profiles;

public sealed class ProfileStoreException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reads and writes profiles.json (spec section 5). No secrets are ever stored.</summary>
public sealed class ProfileStore(string filePath)
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string FilePath { get; } = filePath;

    /// <summary>Missing file means no profiles yet. A corrupt file throws instead of being overwritten.</summary>
    public IReadOnlyList<Profile> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        try
        {
            var doc = JsonSerializer.Deserialize<ProfilesDocument>(File.ReadAllText(FilePath), Options);
            if (doc?.Profiles is null)
            {
                throw new ProfileStoreException($"{FilePath} has no \"profiles\" list.");
            }

            if (doc.Version > CurrentVersion)
            {
                throw new ProfileStoreException(
                    $"{FilePath} was written by a newer version of the launcher (format {doc.Version}).");
            }

            return doc.Profiles;
        }
        catch (JsonException ex)
        {
            throw new ProfileStoreException($"{FilePath} is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>Writes to a temp file then swaps it in, so a crash never leaves a half-written file.</summary>
    public void Save(IEnumerable<Profile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(new ProfilesDocument(CurrentVersion, [.. profiles]), Options);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }

    private sealed record ProfilesDocument(int Version, List<Profile> Profiles);
}
