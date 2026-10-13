// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Pipeline;
using PhotoFlow.Core.Tagging;

namespace PhotoFlow.Sources.Jobs;

/// <summary>
/// Regeneration (job <c>Reanalyze</c>, spec §5, D14). The photos of the perimeter whose results come from an older model
/// (or all, with "forza tutto") are analyzed again with the current models; the new results only go to the staging area
/// (<c>ReanalysisStaging</c>): the catalog does not change until the user confirms, after the preview of the differences.
/// A trial on a sample works the same way on a few photos. Files are only read.
/// </summary>
public sealed class ReanalysisJobRunner(ICatalog catalog, IFileSystem fileSystem, IPhotoProcessor processor, Func<CancellationToken, Task<ModelVersions>> versions)
    : IJobRunner
{
    public static readonly JsonSerializerOptions Json = new();

    public static ReanalysisSettings DeserializeSettings(string json) =>
        JsonSerializer.Deserialize<ReanalysisSettings>(json, Json) ?? throw new InvalidDataException("Empty regeneration settings.");

    /// <summary>Creates the job with the photos of the perimeter (RAW twins are analyzed through their JPEG).</summary>
    public static async Task<long> CreateAsync(ICatalog catalog, ReanalysisSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        var assets = new List<long>();
        if (settings.AssetIds is { } ids)
        {
            assets.AddRange(ids);
        }
        else if (settings.SourceJobId is { } sourceJob)
        {
            assets.AddRange((await catalog.Jobs.GetItemsAsync(sourceJob, ct).ConfigureAwait(false)).Where(i => i.AssetId is not null).Select(i => i.AssetId!.Value));
        }
        else if (settings.LibraryId is { } libraryId)
        {
            assets.AddRange((await catalog.Assets.ListInLibraryAsync(libraryId, settings.Folder, ct).ConfigureAwait(false)).Select(a => a.AssetId));
        }

        var scope = new List<long>();
        foreach (var assetId in assets.Distinct())
        {
            var asset = await catalog.Assets.GetAsync(assetId, ct).ConfigureAwait(false);
            if (asset is null || asset.State != AssetState.Active
                || (asset.Handler == "raw" && await catalog.Assets.GetTwinAsync(assetId, ct).ConfigureAwait(false) is not null))
            {
                continue;
            }

            if (settings.ExcludeReviewed
                && ((await catalog.Decisions.GetAsync(assetId, ct).ConfigureAwait(false)) is { UserOutcome: not null } or { Rating: not null }
                    || (await catalog.XmpSync.GetAsync(assetId, ct).ConfigureAwait(false))?.LastWrittenAt is not null))
            {
                continue;
            }

            scope.Add(assetId);
        }

        if (settings.Sample is { } sample && sample < scope.Count)
        {
            var random = new Random(scope.Count);
            scope = [.. scope.OrderBy(_ => random.Next()).Take(sample).Order()];
        }

        var jobId = await catalog.Jobs.CreateAsync(new Job(0, JobType.Reanalyze, null, settings.Sample is null ? null : "sample", null, settings.ProfileId, false, null,
            JobState.Pending, JobPriority.Low, JsonSerializer.Serialize(settings, Json), DateTimeOffset.UtcNow), ct).ConfigureAwait(false);
        var items = new List<JobItem>();
        foreach (var assetId in scope)
        {
            var path = await AssetFiles.LocateAsync(catalog, NullFileSystem.Instance, assetId, ct).ConfigureAwait(false) ?? string.Empty;
            items.Add(new JobItem(0, jobId, assetId, path.Length > 0 ? path : $"#{assetId}", null, null, PipelineStage.None, JobItemState.Pending, null));
        }

        foreach (var chunk in items.Chunk(5000))
        {
            await catalog.Jobs.AddItemsAsync(jobId, chunk, ct).ConfigureAwait(false);
        }

        return jobId;
    }

    public async Task<JobRunSummary> RunAsync(long jobId, IProgress<JobProgress>? progress, CancellationToken ct)
    {
        var job = await catalog.Jobs.GetAsync(jobId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException($"Job {jobId} not found.");
        if (job.Type != JobType.Reanalyze)
        {
            throw new InvalidOperationException($"Job {jobId} is not a regeneration.");
        }

        var settings = DeserializeSettings(job.SettingsSnapshotJson);
        var current = await versions(ct).ConfigureAwait(false);
        await catalog.Jobs.SetStateAsync(jobId, JobState.Running, CancellationToken.None).ConfigureAwait(false);
        var items = (await catalog.Jobs.GetItemsAsync(jobId, ct).ConfigureAwait(false)).Where(i => i.State != JobItemState.Done).ToList();
        int completed = 0, failed = 0;
        var total = items.Count;
        try
        {
            await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, settings.Workers), CancellationToken = ct }, async (item, token) =>
            {
                var ok = await ProcessAsync(jobId, item, settings, current, token).ConfigureAwait(false);
                lock (items)
                {
                    if (ok)
                    {
                        completed++;
                    }
                    else
                    {
                        failed++;
                    }

                    progress?.Report(new JobProgress(total, total, completed, failed, item.SourcePath));
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await catalog.Jobs.SetStateAsync(jobId, JobState.Paused, CancellationToken.None).ConfigureAwait(false);
            return new JobRunSummary(JobState.Paused, completed, failed, JobPauseReason.UserRequest);
        }

        await catalog.Jobs.SetStateAsync(jobId, JobState.Completed, CancellationToken.None).ConfigureAwait(false);
        return new JobRunSummary(JobState.Completed, completed, failed, null);
    }

    /// <summary>The parts of a photo made by older models (all the chosen parts with "forza tutto").</summary>
    public static async Task<ReanalysisParts> StalePartsAsync(ICatalog catalog, long assetId, ReanalysisSettings settings, ModelVersions current, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(current);
        var available = settings.Parts
            & ~(current.Embedding is null ? ReanalysisParts.Embedding : 0)
            & ~(current.Tags is null ? ReanalysisParts.Tags : 0)
            & ~(current.Captions is null ? ReanalysisParts.Captions : 0)
            & ~(current.Ocr is null ? ReanalysisParts.Ocr : 0)
            & ~(current.Culling is null ? ReanalysisParts.Culling | ReanalysisParts.Faces : 0);
        if (settings.Mode == ReanalysisMode.ForceAll)
        {
            return available;
        }

        var stale = ReanalysisParts.None;
        var result = (await catalog.Analysis.GetResultsAsync([assetId], current.AnalyzerId, ct).ConfigureAwait(false)).GetValueOrDefault(assetId);
        if (available.HasFlag(ReanalysisParts.Culling) && result?.ModelVersion != current.Culling)
        {
            stale |= ReanalysisParts.Culling;
        }

        if (available.HasFlag(ReanalysisParts.Faces))
        {
            var faces = await catalog.Analysis.GetFacesAsync(assetId, ct).ConfigureAwait(false);
            if (result?.ModelVersion != current.Culling
                || faces.Any(f => f.ModelVersion != current.Culling || (f.EmbeddingModel is not null && current.Fingerprints is not null && f.EmbeddingModel != current.Fingerprints)))
            {
                stale |= ReanalysisParts.Faces;
            }
        }

        if (available.HasFlag(ReanalysisParts.Embedding)
            && (await catalog.Analysis.GetEmbeddingsAsync([assetId], current.Embedding!, ct).ConfigureAwait(false)).Count == 0)
        {
            stale |= ReanalysisParts.Embedding;
        }

        if (available.HasFlag(ReanalysisParts.Tags)
            && (await catalog.Tags.GetForAssetAsync(assetId, ct).ConfigureAwait(false)).Any(t => t.Assignment.Source == TagSource.RamPlus && t.Assignment.State == AssetTagState.Active && t.Assignment.ModelVersion != current.Tags))
        {
            stale |= ReanalysisParts.Tags;
        }

        if (available.HasFlag(ReanalysisParts.Captions)
            && await catalog.Captions.GetAsync(assetId, ct).ConfigureAwait(false) is { Origin: CaptionOrigin.Automatic } caption && caption.ModelVersion != current.Captions)
        {
            stale |= ReanalysisParts.Captions;
        }

        if (available.HasFlag(ReanalysisParts.Ocr)
            && (await catalog.Motorsport.GetReadingsAsync(assetId, ct).ConfigureAwait(false)).Any(r => r.ModelVersion != current.Ocr))
        {
            stale |= ReanalysisParts.Ocr;
        }

        return stale;
    }

    private async Task<bool> ProcessAsync(long jobId, JobItem item, ReanalysisSettings settings, ModelVersions current, CancellationToken ct)
    {
        var assetId = item.AssetId!.Value;
        try
        {
            var parts = await StalePartsAsync(catalog, assetId, settings, current, ct).ConfigureAwait(false);
            if (parts != ReanalysisParts.None)
            {
                var path = await AssetFiles.LocateAsync(catalog, fileSystem, assetId, ct).ConfigureAwait(false)
                    ?? throw new FileNotFoundException("The photo is not reachable.", item.SourcePath);
                var asset = await catalog.Assets.GetAsync(assetId, ct).ConfigureAwait(false);
                var previous = (await catalog.Analysis.GetResultsAsync([assetId], current.AnalyzerId, ct).ConfigureAwait(false)).GetValueOrDefault(assetId);
                var profile = previous?.ProfileId ?? settings.ProfileId;
                var result = await processor.ProcessAsync(new PhotoInput(path, asset?.FullHash ?? asset?.QuickHash, asset?.Exif),
                    new ProcessingOptions(profile, null, parts.HasFlag(ReanalysisParts.Tags), parts.HasFlag(ReanalysisParts.Captions)), ct).ConfigureAwait(false);
                async Task Stage(ReanalysisParts part, object payload) =>
                    await catalog.Reanalysis.StageAsync(jobId, assetId, part, JsonSerializer.Serialize(payload, Json), CancellationToken.None).ConfigureAwait(false);
                if (parts.HasFlag(ReanalysisParts.Culling))
                {
                    await Stage(ReanalysisParts.Culling, new StagedCulling(result.Analysis, profile, result.Decision, result.DecisionVersion)).ConfigureAwait(false);
                }

                if (parts.HasFlag(ReanalysisParts.Faces))
                {
                    await Stage(ReanalysisParts.Faces, result.Faces).ConfigureAwait(false);
                }

                if (parts.HasFlag(ReanalysisParts.Embedding) && result.Embedding is { } embedding)
                {
                    await Stage(ReanalysisParts.Embedding, embedding).ConfigureAwait(false);
                }

                if (parts.HasFlag(ReanalysisParts.Tags) && result.Tags is { } tags)
                {
                    await Stage(ReanalysisParts.Tags, tags).ConfigureAwait(false);
                }

                if (parts.HasFlag(ReanalysisParts.Captions) && result.Caption is { } caption)
                {
                    await Stage(ReanalysisParts.Captions, caption).ConfigureAwait(false);
                }

                if (parts.HasFlag(ReanalysisParts.Ocr) && result.Readings is { } readings)
                {
                    await Stage(ReanalysisParts.Ocr, readings).ConfigureAwait(false);
                }
            }

            await catalog.Jobs.UpdateItemAsync(item with { LastCompletedStage = PipelineStage.Decision, State = JobItemState.Done, Error = null }, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            await catalog.Jobs.UpdateItemAsync(item with { State = JobItemState.Failed, Error = e.Message }, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>A file system that never finds anything: the path recorded in the job is informative only.</summary>
    private sealed class NullFileSystem : IFileSystem
    {
        public static NullFileSystem Instance { get; } = new();

        public bool FileExists(string path) => true;

        public bool DirectoryExists(string path) => true;

        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption option) => [];

        public FileMetadata GetFileMetadata(string path) => throw new NotSupportedException();

        public Stream OpenRead(string path) => throw new NotSupportedException();

        public Stream OpenWrite(string path) => throw new NotSupportedException();

        public void CreateDirectory(string path) => throw new NotSupportedException();

        public void Move(string sourcePath, string destinationPath) => throw new NotSupportedException();

        public void Replace(string replacementPath, string destinationPath, string? backupPath) => throw new NotSupportedException();

        public long GetAvailableFreeSpace(string path) => 0;
    }
}
