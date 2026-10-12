// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Core.Metadata;

/// <summary>Reads the EXIF data of spec §4 for a batch of files.</summary>
public interface IExifReader
{
    Task<IReadOnlyList<ExifInfo>> ReadAsync(IReadOnlyList<string> paths, CancellationToken ct);
}

/// <summary>EXIF data used by PhotoFlow. Capture time keeps its sub-seconds (burst order) and records its source.</summary>
public sealed record ExifInfo(
    string Path,
    string? FileType,
    string? Make,
    string? Model,
    string? Lens,
    int? Iso,
    double? ExposureTimeSeconds,
    double? Aperture,
    double? FocalLengthMm,
    double? ExposureCompensationEv,
    DateTimeOffset? CapturedAt,
    DateSource CaptureDateSource,
    int? Orientation,
    string? AfPoint,
    int? Width,
    int? Height,
    string? RawCompression,
    long? EmbeddedJpegLength,
    long? PreviewImageLength)
{
    /// <summary>Nikon High Efficiency NEF (TicoRAW): not decodable by LibRaw, preview only (R15).</summary>
    public bool IsHighEfficiencyRaw =>
        RawCompression is { } c && c.Contains("High Efficiency", StringComparison.OrdinalIgnoreCase);

    public ExifSummary ToSummary() =>
        new(Model, Lens, Iso, ExposureTimeSeconds, Aperture, FocalLengthMm, ExposureCompensationEv, AfPoint);
}
