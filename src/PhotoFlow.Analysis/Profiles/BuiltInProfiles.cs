// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoFlow.Core.Analysis;

namespace PhotoFlow.Analysis.Profiles;

/// <summary>Scene profiles shipped with the application (repository folder <c>profiles/</c>, embedded), spec §6.</summary>
public static class BuiltInProfiles
{
    public const int SchemaVersion = 1;
    private const string ResourcePrefix = "PhotoFlow.Profiles.";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>All built-in profiles, in the order shown to the user.</summary>
    public static IReadOnlyList<SceneProfile> All { get; } = Load();

    public static SceneProfile Get(string id) =>
        All.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"Unknown profile '{id}'.");

    /// <summary>Parses a profile document (also used for custom profiles).</summary>
    public static SceneProfile Parse(string json, bool isBuiltIn)
    {
        var document = JsonSerializer.Deserialize<ProfileDocument>(json, Options) ?? throw new InvalidDataException("Empty profile.");
        if (document.Schema != SchemaVersion)
        {
            throw new InvalidDataException($"Profile '{document.Id}': schema {document.Schema} not supported.");
        }

        var w = document.Weights;
        if (w.Sharpness + w.Eyes + w.Exposure + w.Subject + w.Aesthetic != 100)
        {
            throw new InvalidDataException($"Profile '{document.Id}': weights must sum to 100.");
        }

        if (document.SharpnessZones.Count == 0)
        {
            throw new InvalidDataException($"Profile '{document.Id}': at least one sharpness zone.");
        }

        return new SceneProfile(document.Id, document.Name, document.Version, isBuiltIn, document.CullingEnabled, document.SharpnessZones,
            document.Exposure, document.Faces, document.SubjectClasses, document.Ocr, document.AbsoluteSharpness, w, document.Thresholds);
    }

    private static IReadOnlyList<SceneProfile> Load()
    {
        var order = new[] { "events", "portrait", "motorsport", "animals", "landscape", "night", "archive" };
        var assembly = typeof(BuiltInProfiles).Assembly;
        var profiles = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream);
                return Parse(reader.ReadToEnd(), isBuiltIn: true);
            })
            .ToList();
        return [.. profiles.OrderBy(p => Array.IndexOf(order, p.Id) is var i && i < 0 ? int.MaxValue : i).ThenBy(p => p.Id, StringComparer.Ordinal)];
    }

    private sealed record ProfileDocument(
        int Schema,
        string Id,
        int Version,
        string Name,
        bool CullingEnabled,
        IReadOnlyList<SharpnessZoneKind> SharpnessZones,
        ExposureMode Exposure,
        FaceMode Faces,
        IReadOnlyList<string> SubjectClasses,
        bool Ocr,
        bool AbsoluteSharpness,
        ProfileWeights Weights,
        CullingThresholds Thresholds);
}
