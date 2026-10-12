// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Metadata;
using PhotoFlow.Core.Pipeline;
using PhotoFlow.Data;
using PhotoFlow.Sources.Ingest;
using PhotoFlow.Sources.Jobs;

// Usage: PhotoFlow.ScenarioHost <catalog> <backups> <jobId> <processed-log> <delay-ms>
// Recovers interrupted jobs, then runs or resumes the job. Prints "copied N", "completed N" and finally "state S".
// The processor appends every processed path to the log (flushed per line) and waits <delay-ms> per photo.
var (catalogPath, backups, jobId, log, delay) = (args[0], args[1], long.Parse(args[2], CultureInfo.InvariantCulture), args[3], int.Parse(args[4], CultureInfo.InvariantCulture));

await using var catalog = await SqliteCatalog.OpenAsync(new CatalogOpenOptions(catalogPath, backups), CancellationToken.None);
var interrupted = await ImportJobRunner.RecoverInterruptedAsync(catalog, CancellationToken.None);
Console.WriteLine($"interrupted {interrupted.Count}");

var fs = new PhysicalFileSystem();
var runner = new ImportJobRunner(catalog, new IngestService(new CopyEngine(fs)), new NoExifReader(), fs, new LoggingProcessor(log, delay));
var progress = new SynchronousProgress(p => Console.WriteLine($"copied {p.Copied} completed {p.Completed}"));
var summary = await runner.RunAsync(jobId, progress, CancellationToken.None);
Console.WriteLine($"state {summary.State}");
return 0;

/// <summary>Reports on the calling thread, in order (Progress&lt;T&gt; would post to the thread pool).</summary>
internal sealed class SynchronousProgress(Action<JobProgress> report) : IProgress<JobProgress>
{
    private readonly object _gate = new();

    public void Report(JobProgress value)
    {
        lock (_gate)
        {
            report(value);
        }
    }
}

internal sealed class NoExifReader : IExifReader
{
    public Task<IReadOnlyList<ExifInfo>> ReadAsync(IReadOnlyList<string> paths, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ExifInfo>>([]);
}

internal sealed class LoggingProcessor(string log, int delayMs) : IPhotoProcessor
{
    private readonly object _gate = new();

    public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, ProcessingOptions options, CancellationToken ct)
    {
        await Task.Delay(delayMs, ct);
        lock (_gate)
        {
            using var writer = new StreamWriter(log, append: true);
            writer.WriteLine(photo.Path);
        }

        return new PhotoProcessingResult(
            new AnalyzerResult("scenario", "scenario@1", new Dictionary<string, double>()),
            [],
            new DecisionProposal(CullingOutcome.Kept, []),
            "scenario");
    }
}
