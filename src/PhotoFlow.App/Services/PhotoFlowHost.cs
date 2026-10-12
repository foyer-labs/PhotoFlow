// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Analysis.Culling;
using PhotoFlow.Analysis.Detectors;
using PhotoFlow.Analysis.Profiles;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Imaging;
using PhotoFlow.Core.Inference;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Metadata;
using PhotoFlow.Core.Paths;
using PhotoFlow.Core.Pipeline;
using PhotoFlow.Core.Sources;
using PhotoFlow.Data;
using PhotoFlow.Faces.Detection;
using PhotoFlow.Imaging;
using PhotoFlow.Inference;
using PhotoFlow.Inference.Gpu;
using PhotoFlow.Inference.Models;
using PhotoFlow.Metadata.ExifTool;
using PhotoFlow.Sources.Ingest;
using PhotoFlow.Sources.Jobs;
using PhotoFlow.Sources.Scanning;

namespace PhotoFlow.App.Services;

/// <summary>Which models the analysis uses, for the status bar and the first run (3b).</summary>
public sealed record AnalysisSetup(string? ObjectDetector, string? FaceDetector, bool EyesAvailable, ExecutionBackend Backend);

/// <summary>
/// Composition of the non-UI services (catalog, exiftool, handlers, models, jobs, card detection), started once at
/// launch. Model sessions are created on first use and kept for the session; without models the analysis still
/// measures sharpness and exposure.
/// </summary>
public sealed class PhotoFlowHost : IAsyncDisposable
{
    public const string GpuPackFolder = "cuda13-cudnn9";

    private readonly SemaphoreSlim _sessionsGate = new(1, 1);
    private readonly List<IInferenceSession> _sessions = [];
    private CullingPhotoProcessor? _processor;
    private Task _maintenance = Task.CompletedTask;

    private PhotoFlowHost(IAppPaths paths, IFileSystem fileSystem, SettingsStore settingsStore, AppSettings settings, SqliteCatalog catalog, ExifToolSession exifTool)
    {
        Paths = paths;
        FileSystem = fileSystem;
        SettingsStore = settingsStore;
        Settings = settings;
        Catalog = catalog;
        ExifTool = exifTool;
        ExifReader = new ExifToolExifReader(exifTool);
        MetadataWriter = new ExifToolMetadataWriter(exifTool, fileSystem);
        Thumbnails = new ThumbnailCache(fileSystem, paths.ThumbnailsDirectory);
        Previews = new PreviewProvider(fileSystem, ImageHandlerRegistry.CreateDefault(fileSystem, new ExifToolPreviewSource(exifTool), new MagickRawDecoder()));
        Inference = new OnnxRuntimeInferenceProvider(new InferenceOptions(Path.Combine(paths.GpuRuntimeDirectory, GpuPackFolder)));
        Http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        Models = new ModelRegistry(Http, ModelRegistry.EmbeddedManifest(), paths.ModelsDirectory);
        GpuRuntime = new GpuRuntimeInstaller(Http, GpuRuntimeManifest.Embedded(), Path.Combine(paths.GpuRuntimeDirectory, GpuPackFolder), Path.Combine(paths.GpuRuntimeDirectory, "downloads"));
        Deferred = new DeferredCopyService(new CopyEngine(fileSystem), catalog.DeferredCopies);
        Planner = new ImportPlanner(fileSystem, ExifReader, catalog.Assets);
        Jobs = new JobManager(() => new JobRunnerRouter(
            Catalog,
            new ImportJobRunner(Catalog, new IngestService(new CopyEngine(FileSystem)), ExifReader, FileSystem, new LazyProcessor(this), Deferred),
            new ArchiveJobRunner(Catalog, ExifReader, FileSystem, new LowPriorityProcessor(this))));
        Cards = new PollingCardDetector();
        Maintenance = new MaintenanceService(paths, new InternalFileCleaner(paths));
    }

    public IAppPaths Paths { get; }

    public IFileSystem FileSystem { get; }

    public SettingsStore SettingsStore { get; }

    public AppSettings Settings { get; private set; }

    public ICatalog Catalog { get; }

    public ExifToolSession ExifTool { get; }

    public IExifReader ExifReader { get; }

    public IMetadataWriter MetadataWriter { get; }

    public ThumbnailCache Thumbnails { get; }

    public IPreviewProvider Previews { get; }

    public OnnxRuntimeInferenceProvider Inference { get; }

    public HttpClient Http { get; }

    public ModelRegistry Models { get; }

    public GpuRuntimeInstaller GpuRuntime { get; }

    public DeferredCopyService Deferred { get; }

    public ImportPlanner Planner { get; }

    public JobManager Jobs { get; }

    public ICardDetector Cards { get; }

    /// <summary>Backups and cleanup of PhotoFlow's internal files (B1).</summary>
    public MaintenanceService Maintenance { get; }

    /// <summary>Jobs found running at startup: the previous session ended unexpectedly (board 3c).</summary>
    public IReadOnlyList<Job> InterruptedJobs { get; private set; } = [];

    /// <exception cref="CatalogLockedException">Another PhotoFlow window uses the catalog.</exception>
    public static async Task<PhotoFlowHost> StartAsync(IAppPaths paths, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var fileSystem = new PhysicalFileSystem();
        var settingsStore = new SettingsStore(fileSystem, paths);
        var settings = await settingsStore.LoadAsync(ct).ConfigureAwait(false);
        var catalog = await SqliteCatalog.OpenAsync(new CatalogOpenOptions(paths.CatalogPath, paths.BackupsDirectory), ct).ConfigureAwait(false);
        var exifTool = new ExifToolSession(ExifToolOptions.Bundled(AppContext.BaseDirectory));
        var host = new PhotoFlowHost(paths, fileSystem, settingsStore, settings, catalog, exifTool);
        host.InterruptedJobs = await ImportJobRunner.RecoverInterruptedAsync(catalog, ct).ConfigureAwait(false);
        host.Cards.Start();
        host._maintenance = Task.Run(() => host.Maintenance.RunAtStartupAsync(
            catalog, Path.Combine(paths.GpuRuntimeDirectory, "downloads"), host.GpuRuntime.IsInstalled, CancellationToken.None), CancellationToken.None);
        return host;
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);
        Settings = change(Settings);
        await SettingsStore.SaveAsync(Settings, ct).ConfigureAwait(false);
    }

    /// <summary>Models the analysis would use now (installed and, where needed, accepted).</summary>
    public AnalysisSetup DescribeAnalysis()
    {
        var gpu = Inference.Detected.AvailableBackends.Contains(ExecutionBackend.Cuda);
        var detector = gpu && Models.IsInstalled("dfine-m-obj2coco") ? "dfine-m-obj2coco"
            : Models.IsInstalled("dfine-s-obj2coco") ? "dfine-s-obj2coco" : null;
        var buffalo = Models.IsInstalled("insightface-buffalo-l") && Settings.AcceptedModelLicenses.Contains("insightface-buffalo-l");
        var faces = buffalo ? "insightface-buffalo-l" : Models.IsInstalled("opencv-yunet-2023mar") ? "opencv-yunet-2023mar" : null;
        return new AnalysisSetup(detector, faces, buffalo, gpu ? ExecutionBackend.Cuda : ExecutionBackend.Cpu);
    }

    public async ValueTask DisposeAsync()
    {
        Cards.Dispose();
        await Jobs.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _maintenance.ConfigureAwait(false);
            await Maintenance.BackupAtCloseAsync(Catalog, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            // A backup that cannot be written must not prevent closing; the daily backup retries.
        }

        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        await Catalog.DisposeAsync().ConfigureAwait(false);
        await ExifTool.DisposeAsync().ConfigureAwait(false);
        Http.Dispose();
        _sessionsGate.Dispose();
    }

    private async Task<IPhotoProcessor> GetProcessorAsync(CancellationToken ct)
    {
        await _sessionsGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_processor is not null)
            {
                return _processor;
            }

            var setup = DescribeAnalysis();
            var operations = new SkiaImageOperations();
            IObjectDetector? objects = null;
            if (setup.ObjectDetector is { } detectorId)
            {
                var session = await Open(detectorId, "model.onnx").ConfigureAwait(false);
                objects = new DFineDetector(session, operations);
            }

            IFaceDetector? faces = null;
            if (setup.FaceDetector == "insightface-buffalo-l")
            {
                faces = new InsightFaceDetector(await Open(setup.FaceDetector, "det_10g.onnx").ConfigureAwait(false),
                    await Open(setup.FaceDetector, "2d106det.onnx").ConfigureAwait(false), operations);
            }
            else if (setup.FaceDetector is { } yunet)
            {
                faces = new YuNetFaceDetector(await Open(yunet, "face_detection_yunet_2023mar.onnx").ConfigureAwait(false), operations);
            }

            _processor = new CullingPhotoProcessor(Previews, new CullingAnalyzer(operations, objects, faces), new DecisionEngine(), BuiltInProfiles.Get, Thumbnails);
            return _processor;
        }
        finally
        {
            _sessionsGate.Release();
        }

        async Task<IInferenceSession> Open(string id, string file)
        {
            var session = await Inference.CreateSessionAsync(Models.Resolve(id, file), ExecutionTarget.Auto, ct).ConfigureAwait(false);
            _sessions.Add(session);
            return session;
        }
    }

    /// <summary>
    /// Creates and starts the in-place analysis of a library at low priority: half the parallel analysis of an
    /// import, and it waits while an import runs (spec §10, archives).
    /// </summary>
    public async Task<long> StartArchiveAnalysisAsync(long libraryId, ArchiveOrder order, CancellationToken ct)
    {
        var settings = new ArchiveJobSettings(libraryId, Settings.Import.ProfileId, order, FaceMode.DetectOnly,
            Math.Max(1, PipelineDefaults.DefaultAnalysisWorkers / 2));
        var jobId = await ArchiveJobRunner.CreateAsync(Catalog, FileSystem, settings, ct).ConfigureAwait(false);
        _ = Jobs.StartAsync(jobId, JobPriority.Low);
        return jobId;
    }

    /// <summary>Low-priority analysis: each photo first yields to a running import.</summary>
    private sealed class LowPriorityProcessor(PhotoFlowHost host) : IPhotoProcessor
    {
        public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, ProcessingOptions options, CancellationToken ct)
        {
            await host.Jobs.YieldToNormalAsync(ct).ConfigureAwait(false);
            return await (await host.GetProcessorAsync(ct).ConfigureAwait(false)).ProcessAsync(photo, options, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Loads the models when the first photo reaches the analysis, not at startup.</summary>
    private sealed class LazyProcessor(PhotoFlowHost host) : IPhotoProcessor
    {
        public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, ProcessingOptions options, CancellationToken ct) =>
            await (await host.GetProcessorAsync(ct).ConfigureAwait(false)).ProcessAsync(photo, options, ct).ConfigureAwait(false);
    }
}
