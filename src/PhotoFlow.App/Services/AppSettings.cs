// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Metadata;
using PhotoFlow.Core.Paths;

namespace PhotoFlow.App.Services;

/// <summary>User settings kept between sessions (config folder, spec §11), including what the last import used (board 3d).</summary>
public sealed record AppSettings
{
    public const int MaxRecentEvents = 5;

    /// <summary>Discards written as "to review" until the labelled set validates the culling (D20).</summary>
    public bool PrudentMode { get; init; } = true;

    public ImportDefaults Import { get; init; } = new();

    /// <summary>Model ids whose license the user accepted (P11), e.g. buffalo_l.</summary>
    public IReadOnlyList<string> AcceptedModelLicenses { get; init; } = [];

    public bool NvidiaLicensesAccepted { get; init; }

    /// <summary>True once the first-run setup (board 3b) is done or skipped.</summary>
    public bool SetupCompleted { get; init; }

    /// <summary>Interface language ("it", "en"), or null to follow the system; applied when the window restarts (P18).</summary>
    public string? Language { get; init; }

    public ThemePreference Theme { get; init; } = ThemePreference.Dark;

    /// <summary>exiftool chosen by the user instead of the bundled one (board 5i); applied at the next start.</summary>
    public string? ExifToolPath { get; init; }
}

public enum ThemePreference
{
    Dark,
    Light,
    System,
}

/// <summary>Remembered import choices ("Impostazioni ricordate dall'ultimo import").</summary>
public sealed record ImportDefaults
{
    public string? PrimaryRoot { get; init; }

    public IReadOnlyList<string> SecondaryRoots { get; init; } = [];

    public IReadOnlyList<string> DeferredRoots { get; init; } = [];

    public string ProfileId { get; init; } = "events";

    public FaceMode Faces { get; init; } = FaceMode.Recognize;

    public WritePolicy XmpPolicy { get; init; } = WritePolicy.EmbeddedForNonRaw;

    /// <summary>Destination path template (spec §4, board 5i).</summary>
    public string PathTemplate { get; init; } = Sources.Ingest.PathTemplate.Default;

    public IReadOnlyList<string> RecentEvents { get; init; } = [];

    public ImportDefaults WithRecentEvent(string name)
    {
        var recent = new[] { name }.Concat(RecentEvents.Where(e => !string.Equals(e, name, StringComparison.CurrentCultureIgnoreCase)))
            .Take(AppSettings.MaxRecentEvents)
            .ToList();
        return this with { RecentEvents = recent };
    }
}

/// <summary>Reads and writes <c>settings.json</c> in the config folder; a missing or unreadable file gives the defaults.</summary>
public sealed class SettingsStore(IFileSystem fileSystem, IAppPaths paths)
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private string FilePath => Path.Combine(paths.ConfigDirectory, FileName);

    public async Task<AppSettings> LoadAsync(CancellationToken ct)
    {
        if (!fileSystem.FileExists(FilePath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = fileSystem.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Json, ct).ConfigureAwait(false) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Language and theme read before the first window opens (P18), from a tiny local file: the rest of the settings
    /// loads with the services, off the UI thread.
    /// </summary>
    public static (string? Language, ThemePreference Theme) LoadStartupPreferences(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(paths.ConfigDirectory, FileName)), Json) ?? new AppSettings();
            var language = settings.Language is "it" or "en" ? settings.Language : null;
            return (language, settings.Theme);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, ThemePreference.Dark);
        }
    }

    /// <summary>Atomic save: written next to the file, then swapped in.</summary>
    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        fileSystem.CreateDirectory(paths.ConfigDirectory);
        var temp = FilePath + ".tmp";
        await using (var stream = fileSystem.OpenWrite(temp))
        {
            await JsonSerializer.SerializeAsync(stream, settings, Json, ct).ConfigureAwait(false);
        }

        if (fileSystem.FileExists(FilePath))
        {
            fileSystem.Replace(temp, FilePath, null);
        }
        else
        {
            fileSystem.Move(temp, FilePath);
        }
    }
}
