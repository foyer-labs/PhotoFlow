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

public sealed record MetadataChanges(
    int? Rating,
    string? Label,
    IReadOnlyList<string> FlatKeywords,
    IReadOnlyList<string> HierarchicalKeywords,
    string? Description);
