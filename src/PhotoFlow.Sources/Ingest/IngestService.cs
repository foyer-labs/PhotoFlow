// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Ingest;

namespace PhotoFlow.Sources.Ingest;

/// <summary>Copies a set of files and hands only verified files to the next stage (invariant I3).</summary>
public sealed class IngestService : IIngestService
{
    private readonly ICopyEngine _copyEngine;

    public IngestService(ICopyEngine copyEngine) => _copyEngine = copyEngine;

    public Task<IngestSummary> RunAsync(
        IngestRequest request,
        Func<VerifiedFile, CancellationToken, ValueTask> onVerified,
        IProgress<IngestProgress>? progress,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
