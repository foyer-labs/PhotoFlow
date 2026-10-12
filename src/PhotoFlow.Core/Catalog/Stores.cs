// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Catalog;

/// <summary>The catalog (spec §9). All SQL lives behind these interfaces, in PhotoFlow.Data.</summary>
public interface ICatalog : IAsyncDisposable
{
    ILibraryStore Libraries { get; }

    IAssetStore Assets { get; }

    IJobStore Jobs { get; }

    IDecisionStore Decisions { get; }

    IXmpSyncStore XmpSync { get; }
}

public interface ILibraryStore
{
    Task<long> AddAsync(Library library, CancellationToken ct);

    Task<Library?> GetAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<Library>> ListAsync(CancellationToken ct);
}

public interface IAssetStore
{
    Task<long> AddAsync(Asset asset, CancellationToken ct);

    Task<Asset?> GetAsync(long id, CancellationToken ct);

    /// <summary>"Already imported" identity: quick hash plus capture time (spec §4).</summary>
    Task<Asset?> FindByIdentityAsync(string quickHash, DateTimeOffset? capturedAt, CancellationToken ct);

    Task<Asset?> FindByFullHashAsync(string fullHash, CancellationToken ct);

    Task AddLocationAsync(AssetLocation location, CancellationToken ct);

    Task<IReadOnlyList<AssetLocation>> GetLocationsAsync(long assetId, CancellationToken ct);

    Task AddPairAsync(long rawAssetId, long jpegAssetId, CancellationToken ct);
}

public interface IJobStore
{
    Task<long> CreateAsync(Job job, CancellationToken ct);

    Task<Job?> GetAsync(long id, CancellationToken ct);

    Task SetStateAsync(long jobId, JobState state, CancellationToken ct);

    /// <summary>Jobs left running or paused, e.g. after an unexpected shutdown (board 3c).</summary>
    Task<IReadOnlyList<Job>> ListUnfinishedAsync(CancellationToken ct);

    Task AddItemsAsync(long jobId, IReadOnlyList<JobItem> items, CancellationToken ct);

    Task UpdateItemAsync(JobItem item, CancellationToken ct);

    Task<IReadOnlyList<JobItem>> GetItemsAsync(long jobId, CancellationToken ct);
}

/// <summary>Culling decisions. Automatic processes can never overwrite the user's decision (invariant I5).</summary>
public interface IDecisionStore
{
    Task<Decision?> GetAsync(long assetId, CancellationToken ct);

    /// <summary>Stores the automatic proposal only; user outcome, rating and label are never touched.</summary>
    Task SetAutomaticAsync(long assetId, CullingOutcome outcome, IReadOnlyList<string> reasons, string modelVersion, CancellationToken ct);

    /// <summary>Stores the user's decision; it wins over any present or future automatic proposal.</summary>
    Task SetUserAsync(long assetId, CullingOutcome? outcome, int? rating, string? label, CancellationToken ct);
}

public interface IXmpSyncStore
{
    Task<XmpSyncState?> GetAsync(long assetId, CancellationToken ct);

    Task MarkPendingAsync(long assetId, string? error, CancellationToken ct);

    Task MarkWrittenAsync(long assetId, string signature, CancellationToken ct);

    Task<IReadOnlyList<long>> ListPendingAsync(CancellationToken ct);
}

public sealed class CatalogLockedException(string message) : IOException(message);

public sealed class CatalogCorruptException(string message) : IOException(message);
