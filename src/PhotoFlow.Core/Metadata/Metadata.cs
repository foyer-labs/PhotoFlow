// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Metadata;

/// <summary>Writes XMP by merging, never overwriting; RAW files only get sidecars (invariant I2).</summary>
public interface IMetadataWriter
{
    Task<MetadataWriteResult> WriteAsync(MetadataTarget target, MetadataChanges changes, WritePolicy policy, CancellationToken ct);
}

/// <summary>The file whose metadata is written. RAW files are never modified: only their sidecar.</summary>
public sealed record MetadataTarget(string FilePath, bool IsRaw);

public enum MetadataWriteOutcome
{
    Embedded,
    Sidecar,
    CatalogOnly,
    Failed,
}

public sealed record MetadataWriteResult(MetadataWriteOutcome Outcome, string? WrittenPath, string? Error);

/// <summary>XMP write policy of a library (spec §4, D21).</summary>
public enum WritePolicy
{
    SidecarOnly,
    EmbeddedForNonRaw,
    CatalogOnly,
}

/// <param name="Regions">Named face regions (MWG), or null to leave the regions untouched.</param>
/// <param name="RemovedFlatKeywords">Keywords PhotoFlow may have written before that no longer apply (tags removed by the user or blacklisted); other keywords are never touched.</param>
public sealed record MetadataChanges(
    int? Rating,
    string? Label,
    IReadOnlyList<string> FlatKeywords,
    IReadOnlyList<string> HierarchicalKeywords,
    string? Description,
    FaceRegions? Regions = null,
    IReadOnlyList<string>? RemovedFlatKeywords = null,
    IReadOnlyList<string>? RemovedHierarchicalKeywords = null);

/// <summary>
/// Face regions to write (spec §4, §7: MWG regions with the name). Regions already in the file with a name that is
/// not managed by PhotoFlow (<paramref name="ManagedNames"/>: the names of the catalog's people) are kept.
/// </summary>
public sealed record FaceRegions(int ImageWidth, int ImageHeight, IReadOnlyList<FaceRegion> Regions, IReadOnlyCollection<string> ManagedNames);

/// <summary>A named face, MWG convention: centre and size normalized to the image (0..1).</summary>
public sealed record FaceRegion(string Name, double CenterX, double CenterY, double Width, double Height);
