// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Core.Pipeline;

/// <summary>Batch barrier stage (groupings, faces): waits for the whole set.</summary>
public interface IBatchStage
{
    Task RunAsync(JobContext job, IProgress<StageProgress> progress, CancellationToken ct);
}

public sealed record JobContext(long JobId, JobType Type, SceneProfile Profile);

public sealed record StageProgress(PipelineStage Stage, int Completed, int Total, string? CurrentItem);

/// <summary>Pipeline stages of spec §3, in order.</summary>
public enum PipelineStage
{
    None = 0,
    CopyAndVerify = 1,
    Preview = 2,
    Analyzers = 3,
    Grouping = 4,
    Decision = 5,
    TaggingAndOcr = 6,
    Faces = 7,
    Review = 8,
    XmpWrite = 9,
}
