// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Imaging;
using PhotoFlow.Core.Pipeline;

namespace PhotoFlow.Analysis.Culling;

/// <summary>
/// Stages Preview → Analyzers → Decision for one verified photo: upright preview at the reference size, grid thumbnail,
/// culling signals, faces (normalized coordinates) and the proposed outcome of the profile.
/// </summary>
public sealed class CullingPhotoProcessor : IPhotoProcessor
{
    private readonly IPreviewProvider _previews;
    private readonly CullingAnalyzer _analyzer;
    private readonly IDecisionEngine _engine;
    private readonly Func<string, SceneProfile> _profiles;
    private readonly IThumbnailStore? _thumbnails;
    private readonly IImageEmbedder? _embedder;
    private readonly IFaceEmbedder? _faceEmbedder;
    private readonly Func<IFingerprintProtector>? _protector;

    /// <summary>Faces smaller than this (long side, reference scale) get no fingerprint (spec §7 <c>minFaceSizePx</c>).</summary>
    public const double MinFingerprintFaceSize = 48;

    /// <param name="embedder">CLIP image embedding for bursts and similar photos (phase 2); null without the model.</param>
    /// <param name="faceEmbedder">Fingerprints for the Recognize mode; null without the model.</param>
    /// <param name="protector">Current fingerprint encryption; without an available key no fingerprint is computed (P13).</param>
    public CullingPhotoProcessor(IPreviewProvider previews, CullingAnalyzer analyzer, IDecisionEngine engine, Func<string, SceneProfile> profiles, IThumbnailStore? thumbnails,
        IImageEmbedder? embedder = null, IFaceEmbedder? faceEmbedder = null, Func<IFingerprintProtector>? protector = null)
    {
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(analyzer);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(profiles);
        _previews = previews;
        _analyzer = analyzer;
        _engine = engine;
        _profiles = profiles;
        _thumbnails = thumbnails;
        _embedder = embedder;
        _faceEmbedder = faceEmbedder;
        _protector = protector;
    }

    public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, ProcessingOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(options);
        var profile = _profiles(options.ProfileId);
        if (options.Faces is { } faceMode)
        {
            profile = profile with { Faces = faceMode };
        }

        var preview = await _previews.GetPreviewAsync(photo.Path, CullingSignals.ReferenceLongSide, ct).ConfigureAwait(false);
        if (_thumbnails is not null && photo.ContentHash is { } hash && !_thumbnails.Exists(hash))
        {
            await _thumbnails.StoreAsync(hash, preview.Image, ct).ConfigureAwait(false);
        }

        var analysis = await _analyzer.AnalyzeDetailedAsync(new PhotoContext(null, photo.Path, preview, photo.Exif), profile, ct).ConfigureAwait(false);
        var embedding = _embedder is null
            ? null
            : new ImageEmbedding(_embedder.ModelVersion, await _embedder.EmbedAsync(preview.Image, ct).ConfigureAwait(false));
        var decision = _engine.Decide(analysis.Signals, profile);
        var (width, height) = (preview.Image.Width, preview.Image.Height);
        var protector = profile.Faces == FaceMode.Recognize && _faceEmbedder is not null ? _protector?.Invoke() : null;
        var faces = new List<Face>(analysis.Faces.Count);
        foreach (var f in analysis.Faces)
        {
            byte[]? fingerprint = null;
            if (protector is { IsAvailable: true } && f.ReferenceSize >= MinFingerprintFaceSize)
            {
                fingerprint = protector.Protect(await _faceEmbedder!.EmbedAsync(preview.Image, f.Face, ct).ConfigureAwait(false));
            }

            faces.Add(new Face(
                0,
                0,
                f.Box,
                f.Face.Landmarks?.Select(p => new Point2(p.X / width, p.Y / height)).ToArray(),
                f.Face.Score,
                f.Eyes.Left,
                f.Eyes.Right,
                fingerprint,
                null,
                null,
                null,
                analysis.ModelVersion,
                f.Face.Keypoints.Select(p => new Point2(p.X / width, p.Y / height)).ToArray(),
                fingerprint is null ? null : _faceEmbedder!.ModelVersion));
        }

        return new PhotoProcessingResult(
            new AnalyzerResult(_analyzer.Id, analysis.ModelVersion, CullingScores.ToScores(analysis.Signals)),
            faces,
            decision,
            $"{_engine.Version}+{analysis.ModelVersion}+{profile.Id}@{profile.Version}",
            embedding);
    }
}
