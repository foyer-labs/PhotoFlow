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

    IDeferredCopyStore DeferredCopies { get; }

    IAnalysisStore Analysis { get; }

    /// <summary>Consistent copy of the open catalog into <paramref name="backupsDirectory"/> (spec §9); returns its path.</summary>
    Task<string> BackupAsync(string backupsDirectory, string reason, CancellationToken ct);
}

public interface ILibraryStore
{
    Task<long> AddAsync(Library library, CancellationToken ct);

    Task<Library?> GetAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<Library>> ListAsync(CancellationToken ct);

    /// <summary>The library with this root (compared case-insensitively, as on Windows), if any.</summary>
    Task<Library?> FindByRootAsync(string rootPath, CancellationToken ct);

    /// <summary>Photos (assets) with a location in the library.</summary>
    Task<int> CountAssetsAsync(long libraryId, CancellationToken ct);
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

    /// <summary>Assets of a library still without the full hash (computed in background, spec §10).</summary>
    Task<IReadOnlyList<(long AssetId, string RelativePath)>> ListWithoutFullHashAsync(long libraryId, int limit, CancellationToken ct);

    Task SetFullHashAsync(long assetId, string fullHash, CancellationToken ct);
}

public interface IJobStore
{
    Task<long> CreateAsync(Job job, CancellationToken ct);

    Task<Job?> GetAsync(long id, CancellationToken ct);

    Task SetStateAsync(long jobId, JobState state, CancellationToken ct);

    /// <summary>Jobs left running or paused, e.g. after an unexpected shutdown (board 3c).</summary>
    Task<IReadOnlyList<Job>> ListUnfinishedAsync(CancellationToken ct);

    /// <summary>The most recent jobs, newest first.</summary>
    Task<IReadOnlyList<Job>> ListRecentAsync(int limit, CancellationToken ct);

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

    /// <summary>Effective outcomes (the user's, else the automatic one) of the assets of a job, by outcome.</summary>
    Task<IReadOnlyDictionary<CullingOutcome, int>> CountOutcomesForJobAsync(long jobId, CancellationToken ct);
}

public interface IXmpSyncStore
{
    Task<XmpSyncState?> GetAsync(long assetId, CancellationToken ct);

    Task MarkPendingAsync(long assetId, string? error, CancellationToken ct);

    Task MarkWrittenAsync(long assetId, string signature, CancellationToken ct);

    Task<IReadOnlyList<long>> ListPendingAsync(CancellationToken ct);
}

/// <summary>Persistent queue of copies to deferred destinations (NAS), retried until verified.</summary>
public interface IDeferredCopyStore
{
    Task EnqueueAsync(DeferredCopy copy, CancellationToken ct);

    Task<IReadOnlyList<DeferredCopy>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken ct);

    Task UpdateAsync(DeferredCopy copy, CancellationToken ct);

    Task<int> CountPendingAsync(CancellationToken ct);
}

/// <summary>Automatic analysis results and detected faces. Manual face assignments are never removed (invariant I5).</summary>
public interface IAnalysisStore
{
    /// <summary>Stores the result, replacing the previous result of the same analyzer for the asset.</summary>
    Task SaveResultAsync(AnalyzerResultRecord result, CancellationToken ct);

    Task<AnalyzerResultRecord?> GetResultAsync(long assetId, string analyzerId, CancellationToken ct);

    /// <summary>Replaces the automatically detected faces of the asset; faces with a manual assignment are kept.</summary>
    Task ReplaceAutomaticFacesAsync(long assetId, IReadOnlyList<Face> faces, CancellationToken ct);

    Task<IReadOnlyList<Face>> GetFacesAsync(long assetId, CancellationToken ct);
}

public sealed class CatalogLockedException(string message) : IOException(message);

public sealed class CatalogCorruptException(string message) : IOException(message);
