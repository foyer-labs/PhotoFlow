// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Imaging;

namespace PhotoFlow.Core.Analysis;

/// <summary>Per-photo culling analyzer (exposure, sharpness, subject, faces/eyes, embedding).</summary>
public interface IPhotoAnalyzer
{
    string Id { get; }

    Task<AnalyzerResult> AnalyzeAsync(PhotoContext photo, SceneProfile profile, CancellationToken ct);
}

/// <summary>Object detection, implemented by a model adapter (e.g. D-FINE).</summary>
public interface IObjectDetector
{
    string ModelVersion { get; }

    Task<IReadOnlyList<Detection>> DetectAsync(RgbImage image, CancellationToken ct);
}

public sealed record PhotoContext(long? AssetId, string Path, PreviewImage Preview, ExifSummary? Exif);

/// <summary>Every automatic result records the model version that produced it (CLAUDE.md §4).</summary>
public sealed record AnalyzerResult(string AnalyzerId, string ModelVersion, IReadOnlyDictionary<string, double> Scores);

public sealed record SceneProfile(
    string Id,
    string Name,
    int Version,
    bool IsBuiltIn,
    FaceMode Faces,
    ProfileWeights Weights);

/// <summary>Composite score weights (spec §6), percentages summing to 100.</summary>
public sealed record ProfileWeights(int Sharpness, int Eyes, int Exposure, int Subject, int Aesthetic);

public enum FaceMode
{
    Off,
    DetectOnly,
    Recognize,
}

/// <summary>Box in normalized coordinates (0..1) relative to the analyzed image.</summary>
public readonly record struct BoundingBox(float X1, float Y1, float X2, float Y2)
{
    public float Width => X2 - X1;

    public float Height => Y2 - Y1;

    public float Area => Math.Max(0, Width) * Math.Max(0, Height);

    public float IntersectionOverUnion(BoundingBox other)
    {
        var ix = Math.Max(0, Math.Min(X2, other.X2) - Math.Max(X1, other.X1));
        var iy = Math.Max(0, Math.Min(Y2, other.Y2) - Math.Max(Y1, other.Y1));
        var intersection = ix * iy;
        var union = Area + other.Area - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}

public sealed record Detection(int ClassId, string Label, float Score, BoundingBox Box);
