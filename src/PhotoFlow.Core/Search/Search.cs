// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Search;

/// <summary>
/// Full-text and vector search, isolated for a future PostgreSQL backend.
/// Provisional members: to be finalized in the phase 3 plan.
/// </summary>
public interface ISearchIndex
{
    Task IndexTextAsync(long assetId, string text, CancellationToken ct);

    Task<IReadOnlyList<SearchHit>> SearchTextAsync(string query, int limit, CancellationToken ct);

    Task UpsertVectorAsync(long assetId, string modelVersion, ReadOnlyMemory<float> vector, CancellationToken ct);

    Task<IReadOnlyList<SearchHit>> SearchSimilarAsync(ReadOnlyMemory<float> vector, int limit, CancellationToken ct);
}

public sealed record SearchHit(long AssetId, double Score);
