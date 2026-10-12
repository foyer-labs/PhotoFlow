// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Ingest;

namespace PhotoFlow.Core.Pipeline;

/// <summary>
/// Settings of an import job, stored as the job's settings snapshot: a resumed job uses exactly these, even if the
/// application settings changed in the meantime.
/// </summary>
/// <param name="LibraryId">Library of the primary destination (assets get their location there).</param>
/// <param name="SourceRoot">Card or folder imported (never written, I1).</param>
public sealed record ImportJobSettings(
    long LibraryId,
    string SourceRoot,
    IReadOnlyList<IngestDestination> Destinations,
    string ProfileId,
    int MaxParallelFiles = 2,
    int AnalysisWorkers = 2);

/// <summary>Runs or resumes a job until it completes, pauses (cancellation, unavailable destination or source) or fails.</summary>
public interface IJobRunner
{
    Task<JobRunSummary> RunAsync(long jobId, IProgress<JobProgress>? progress, CancellationToken ct);
}

/// <param name="Total">Files of the job.</param>
/// <param name="Copied">Files whose primary copy is verified (including earlier runs).</param>
/// <param name="Completed">Files with every stage done.</param>
public sealed record JobProgress(int Total, int Copied, int Completed, int Failed, string? CurrentItem);

public sealed record JobRunSummary(JobState State, int Completed, int Failed, JobPauseReason? PauseReason);

public enum JobPauseReason
{
    /// <summary>Paused by the user (cancellation of the run).</summary>
    UserRequest,

    /// <summary>A destination disappeared (disk disconnected, P8): resume when it is back.</summary>
    DestinationUnavailable,

    /// <summary>The card or source folder is not reachable: files still to copy wait for it.</summary>
    SourceUnavailable,

    /// <summary>The application closed while the job was running (detected at the next start, board 3c).</summary>
    Interrupted,
}

/// <summary>Preview, analysis and proposed decision of one verified photo (stages Preview → Analyzers → Decision).</summary>
public interface IPhotoProcessor
{
    Task<PhotoProcessingResult> ProcessAsync(string path, ExifSummary? exif, string profileId, CancellationToken ct);
}

/// <param name="Faces">Detected faces, ids and asset not yet assigned.</param>
/// <param name="DecisionVersion">Version of the decision engine and of the models behind the signals.</param>
public sealed record PhotoProcessingResult(
    AnalyzerResult Analysis,
    IReadOnlyList<Face> Faces,
    DecisionProposal Decision,
    string DecisionVersion);
