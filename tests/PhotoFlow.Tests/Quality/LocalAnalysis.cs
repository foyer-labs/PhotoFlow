// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Analysis.Culling;
using PhotoFlow.Analysis.Detectors;
using PhotoFlow.Analysis.Profiles;
using PhotoFlow.Core.Imaging;
using PhotoFlow.Core.Inference;
using PhotoFlow.Core.IO;
using PhotoFlow.Faces.Detection;
using PhotoFlow.Imaging;
using PhotoFlow.Inference;
using PhotoFlow.Metadata.ExifTool;
using PhotoFlow.Tests.Parity;
using PhotoFlow.Tests.Support;

namespace PhotoFlow.Tests.Quality;

/// <summary>
/// The real analysis of the application (previews, D-FINE M, buffalo_l, decision engine) built from the models in
/// PHOTOFLOW_TESTDATA/models (lab layout), for the local Quality and Performance tests. CUDA when available.
/// </summary>
public sealed class LocalAnalysis : IAsyncDisposable
{
    private readonly List<IInferenceSession> _sessions = [];

    private LocalAnalysis(ExifToolSession exifTool, CullingPhotoProcessor processor, ExecutionBackend backend)
    {
        ExifTool = exifTool;
        Processor = processor;
        Backend = backend;
    }

    public ExifToolSession ExifTool { get; }

    public CullingPhotoProcessor Processor { get; }

    public ExecutionBackend Backend { get; }

    public static async Task<LocalAnalysis> CreateAsync(IFileSystem fileSystem, IThumbnailStore? thumbnails, CancellationToken ct)
    {
        var models = TestData.RequireDirectory("models");
        var provider = new OnnxRuntimeInferenceProvider(new InferenceOptions(GpuRuntime.Directory));
        var target = provider.Detected.AvailableBackends.Contains(ExecutionBackend.Cuda) ? ExecutionTarget.Gpu : ExecutionTarget.Cpu;
        var exifTool = ExifToolFixture.Start();
        var operations = new SkiaImageOperations();
        var sessions = new List<IInferenceSession>();

        async Task<IInferenceSession> Open(string folder, string file, string adapter)
        {
            var path = Path.Combine(models, folder, file);
            if (!File.Exists(path))
            {
                Assert.Skip($"Model missing for the local test: {path}");
            }

            var session = await provider.CreateSessionAsync(new ModelDescriptor(folder, ModelFunction.Faces, folder, "lab", path, string.Empty, adapter), target, ct);
            sessions.Add(session);
            return session;
        }

        var detector = new DFineDetector(await Open("dfine-m-obj2coco", "model.onnx", DFineDetector.AdapterId), operations);
        var faces = new InsightFaceDetector(
            await Open("insightface-buffalo-l", "det_10g.onnx", InsightFaceDetector.AdapterId),
            await Open("insightface-buffalo-l", "2d106det.onnx", InsightFaceDetector.AdapterId),
            operations);
        var previews = new PreviewProvider(fileSystem, ImageHandlerRegistry.CreateDefault(fileSystem, new ExifToolPreviewSource(exifTool), new MagickRawDecoder()));
        var processor = new CullingPhotoProcessor(previews, new CullingAnalyzer(operations, detector, faces), new DecisionEngine(), BuiltInProfiles.Get, thumbnails);
        var analysis = new LocalAnalysis(exifTool, processor, target == ExecutionTarget.Gpu ? ExecutionBackend.Cuda : ExecutionBackend.Cpu);
        analysis._sessions.AddRange(sessions);
        return analysis;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        await ExifTool.DisposeAsync();
    }
}
