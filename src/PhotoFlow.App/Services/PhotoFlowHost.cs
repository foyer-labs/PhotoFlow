// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Analysis.Culling;
using PhotoFlow.Analysis.Detectors;
using PhotoFlow.Analysis.Grouping;
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
using PhotoFlow.Faces.Protection;
using PhotoFlow.Faces.Recognition;
using PhotoFlow.Imaging;
using PhotoFlow.Inference;
using PhotoFlow.Inference.Gpu;
using PhotoFlow.Inference.Models;
using PhotoFlow.Metadata.ExifTool;
using PhotoFlow.Sources.Ingest;
using PhotoFlow.Sources.Jobs;
using PhotoFlow.Sources.Scanning;
using PhotoFlow.Tagging.Clip;

namespace PhotoFlow.App.Services;

/// <summary>Which models the analysis uses, for the status bar and the first run (3b).</summary>
public sealed record AnalysisSetup(string? ObjectDetector, string? FaceDetector, bool EyesAvailable, ExecutionBackend Backend, string? Embedder = null, string? FaceRecognizer = null);

/// <summary>
/// Composition of the non-UI services (catalog, exiftool, handlers, models, jobs, card detection), started once at
/// launch. Model sessions are created on first use and kept for the session; without models the analysis still
/// measures sharpness and exposure.
/// </summary>
public sealed class PhotoFlowHost : IAsyncDisposable
{
    public const string GpuPackFolder = "cuda13-cudnn9";

    /// <summary>Model adapters this version can run: only these models can be downloaded or chosen (board 5g).</summary>
    public static readonly IReadOnlySet<string> ImplementedAdapters = new HashSet<string>(StringComparer.Ordinal)
    {
        "dfine-hf-v1", "insightface-buffalo-v1", "yunet-v1", "clip-hf-v1", "sface-v1",
    };

    public const string ClipModelId = "clip-vit-b32";
    public const string SFaceModelId = "opencv-sface-2021dec";

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
            new ImportJobRunner(Catalog, new IngestService(new CopyEngine(FileSystem)), ExifReader, FileSystem, new LazyProcessor(this), Deferred, BatchStages()),
            new ArchiveJobRunner(Catalog, ExifReader, FileSystem, new LowPriorityProcessor(this), BatchStages())));
        Cards = new PollingCardDetector();
        Maintenance = new MaintenanceService(paths, new InternalFileCleaner(paths), protectedBackup: () => Restore?.Pending);
        Restore = new CatalogRestoreService(paths);
        FingerprintKeys = new FingerprintKeyStore(Path.Combine(paths.ConfigDirectory, "face-key.json"));
        FingerprintKeys.Open();
    }

    /// <summary>Key of the face fingerprints (D12): locked on another account or PC until the password or a recompute.</summary>
    public FingerprintKeyStore FingerprintKeys { get; }

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

    public CatalogRestoreService Restore { get; }

    /// <summary>Outcome of a restore requested in the settings and applied at this start (null when none was pending).</summary>
    public CatalogRestoreOutcome? RestoreOutcome { get; private set; }

    /// <summary>The import path template from the settings; the default when the stored one is not valid.</summary>
    public PathTemplate PathTemplate
    {
        get
        {
            try
            {
                return new PathTemplate(Settings.Import.PathTemplate);
            }
            catch (ArgumentException)
            {
                return new PathTemplate(PathTemplate.Default);
            }
        }
    }

    /// <summary>Jobs found running at startup: the previous session ended unexpectedly (board 3c).</summary>
    public IReadOnlyList<Job> InterruptedJobs { get; private set; } = [];

    /// <exception cref="CatalogLockedException">Another PhotoFlow window uses the catalog.</exception>
    public static async Task<PhotoFlowHost> StartAsync(IAppPaths paths, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var fileSystem = new PhysicalFileSystem();
        var settingsStore = new SettingsStore(fileSystem, paths);
        var settings = await settingsStore.LoadAsync(ct).ConfigureAwait(false);
        var restore = new CatalogRestoreService(paths);
        CatalogRestoreOutcome? restoreOutcome = null;
        if (restore.Pending is { } pending)
        {
            try
            {
                restoreOutcome = new CatalogRestoreOutcome(pending, await restore.ApplyPendingAsync(ct).ConfigureAwait(false), null);
            }
            catch (CatalogCorruptException e)
            {
                // The backup is damaged: nothing was replaced, the current catalog opens as before.
                restore.Cancel();
                restoreOutcome = new CatalogRestoreOutcome(pending, null, e.Message);
            }
        }

        var catalog = await SqliteCatalog.OpenAsync(new CatalogOpenOptions(paths.CatalogPath, paths.BackupsDirectory), ct).ConfigureAwait(false);
        var exifTool = new ExifToolSession(settings.ExifToolPath is { } custom && File.Exists(custom)
            ? new ExifToolOptions(custom)
            : ExifToolOptions.Bundled(AppContext.BaseDirectory));
        var host = new PhotoFlowHost(paths, fileSystem, settingsStore, settings, catalog, exifTool) { RestoreOutcome = restoreOutcome };
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

    /// <summary>
    /// Offline package (spec §11) from a folder copied from another PC: the models whose license is accepted (or needs
    /// no acceptance) and, with the NVIDIA licenses accepted, the GPU pack when all its archives are there. Every file
    /// is verified with SHA-256; the folder is only read. Acceptances are recorded only for what was installed.
    /// </summary>
    public async Task<(IReadOnlyList<string> Models, bool GpuPack)> ImportOfflinePackageAsync(
        string folder, IReadOnlySet<string> acceptedModelLicenses, bool nvidiaLicensesAccepted, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(acceptedModelLicenses);
        var models = await Task.Run(() => Models.ImportOfflineAsync(folder, m => acceptedModelLicenses.Contains(m.Id), ct), ct).ConfigureAwait(false);
        var accepted = models.Where(id => Models.Get(id).License.RequiresAcceptance).ToList();
        if (accepted.Count > 0)
        {
            await UpdateSettingsAsync(s => s with { AcceptedModelLicenses = s.AcceptedModelLicenses.Concat(accepted).Distinct().ToList() }, ct).ConfigureAwait(false);
        }

        var gpu = false;
        if (nvidiaLicensesAccepted && GpuRuntime.HasOfflinePackage(folder))
        {
            await UpdateSettingsAsync(s => s with { NvidiaLicensesAccepted = true }, ct).ConfigureAwait(false);
            await Task.Run(() => GpuRuntime.ImportOfflineAsync(folder, true, ct), ct).ConfigureAwait(false);
            gpu = true;
        }

        return (models, gpu);
    }

    /// <summary>Stages run once every photo of a job is analyzed: groupings, then decisions per group (spec §3, phases 4-5).</summary>
    private IReadOnlyList<IBatchStage> BatchStages()
    {
        var embedder = DescribeAnalysis().Embedder is { } clip ? $"{clip}@{Models.Get(clip).Version}" : null;
        return
        [
            new GroupingStage(Catalog, BuiltInProfiles.Get, embedder),
            new ProfileStage(Catalog, embedder is null ? null : new LazySceneClassifier(this), embedder),
            new ReanalysisStage(Catalog, FileSystem, new LazyProcessor(this), null),
            new GroupDecisionStage(Catalog, BuiltInProfiles.Get, new DecisionEngine()),
            new FacesStage(Catalog, () => FingerprintKeys.Protector),
        ];
    }

    /// <summary>Models the analysis would use now (installed and, where needed, accepted).</summary>
    public AnalysisSetup DescribeAnalysis()
    {
        var gpu = Inference.Detected.AvailableBackends.Contains(ExecutionBackend.Cuda);
        var detector = gpu && Models.IsInstalled("dfine-m-obj2coco") ? "dfine-m-obj2coco"
            : Models.IsInstalled("dfine-s-obj2coco") ? "dfine-s-obj2coco" : null;
        var buffalo = Models.IsInstalled("insightface-buffalo-l") && Settings.AcceptedModelLicenses.Contains("insightface-buffalo-l");
        var faces = buffalo ? "insightface-buffalo-l" : Models.IsInstalled("opencv-yunet-2023mar") ? "opencv-yunet-2023mar" : null;
        var recognizer = buffalo ? "insightface-buffalo-l" : faces is not null && Models.IsInstalled(SFaceModelId) ? SFaceModelId : null;
        return new AnalysisSetup(detector, faces, buffalo, gpu ? ExecutionBackend.Cuda : ExecutionBackend.Cpu,
            Models.IsInstalled(ClipModelId) ? ClipModelId : null, recognizer);
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

            IImageEmbedder? embedder = setup.Embedder is { } clip
                ? new ClipImageEmbedder(await Open(clip, "vision_model.onnx").ConfigureAwait(false), operations)
                : null;
            IFaceEmbedder? fingerprints = setup.FaceRecognizer switch
            {
                "insightface-buffalo-l" => new ArcFaceEmbedder(await Open("insightface-buffalo-l", ArcFaceEmbedder.File).ConfigureAwait(false)),
                SFaceModelId => new SFaceEmbedder(await Open(SFaceModelId, SFaceEmbedder.File).ConfigureAwait(false)),
                _ => null,
            };
            _processor = new CullingPhotoProcessor(Previews, new CullingAnalyzer(operations, objects, faces), new DecisionEngine(), BuiltInProfiles.Get, Thumbnails, embedder,
                fingerprints, () => FingerprintKeys.Protector);
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
    public async Task<long> StartArchiveAnalysisAsync(long libraryId, string profileId, ArchiveOrder order, CancellationToken ct)
    {
        var settings = new ArchiveJobSettings(libraryId, profileId, order, FaceMode.DetectOnly,
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

    /// <summary>CLIP text model loaded on the first classification (the prompts are embedded once).</summary>
    private sealed class LazySceneClassifier(PhotoFlowHost host) : ISceneClassifier
    {
        public string ModelVersion => $"{ClipModelId}+scenes-v1";

        public async Task<IReadOnlyList<(string ProfileId, double Probability)>> ClassifyAsync(ReadOnlyMemory<float> imageEmbedding, CancellationToken ct) =>
            await (await host.GetSceneClassifierAsync(ct).ConfigureAwait(false)).ClassifyAsync(imageEmbedding, ct).ConfigureAwait(false);
    }

    private ClipSceneClassifier? _sceneClassifier;

    private async Task<ClipSceneClassifier> GetSceneClassifierAsync(CancellationToken ct)
    {
        await _sessionsGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_sceneClassifier is null)
            {
                var session = await Inference.CreateSessionAsync(Models.Resolve(ClipModelId, "text_model.onnx"), ExecutionTarget.Auto, ct).ConfigureAwait(false);
                _sessions.Add(session);
                _sceneClassifier = new ClipSceneClassifier(new ClipTextEmbedder(session, ClipTokenizer.Load(Models.Resolve(ClipModelId, "tokenizer.json").FilePath)));
            }

            return _sceneClassifier;
        }
        finally
        {
            _sessionsGate.Release();
        }
    }

    /// <summary>Loads the models when the first photo reaches the analysis, not at startup.</summary>
    private sealed class LazyProcessor(PhotoFlowHost host) : IPhotoProcessor
    {
        public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, ProcessingOptions options, CancellationToken ct) =>
            await (await host.GetProcessorAsync(ct).ConfigureAwait(false)).ProcessAsync(photo, options, ct).ConfigureAwait(false);
    }
}
