// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Core.Metadata;

/// <summary>Writes XMP by merging, never overwriting; RAW files only get sidecars (invariant I2).</summary>
public interface IMetadataWriter
{
    Task WriteAsync(Asset asset, MetadataChanges changes, WritePolicy policy, CancellationToken ct);
}

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
