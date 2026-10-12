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

    public CullingPhotoProcessor(IPreviewProvider previews, CullingAnalyzer analyzer, IDecisionEngine engine, Func<string, SceneProfile> profiles, IThumbnailStore? thumbnails)
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
    }

    public async Task<PhotoProcessingResult> ProcessAsync(PhotoInput photo, string profileId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        var profile = _profiles(profileId);
        var preview = await _previews.GetPreviewAsync(photo.Path, CullingSignals.ReferenceLongSide, ct).ConfigureAwait(false);
        if (_thumbnails is not null && photo.ContentHash is { } hash && !_thumbnails.Exists(hash))
        {
            await _thumbnails.StoreAsync(hash, preview.Image, ct).ConfigureAwait(false);
        }

        var analysis = await _analyzer.AnalyzeDetailedAsync(new PhotoContext(null, photo.Path, preview, photo.Exif), profile, ct).ConfigureAwait(false);
        var decision = _engine.Decide(analysis.Signals, profile);
        var (width, height) = (preview.Image.Width, preview.Image.Height);
        var faces = analysis.Faces.Select(f => new Face(
            0,
            0,
            f.Box,
            f.Face.Landmarks?.Select(p => new Point2(p.X / width, p.Y / height)).ToArray(),
            f.Face.Score,
            f.Eyes.Left,
            f.Eyes.Right,
            null,
            null,
            null,
            null,
            analysis.ModelVersion)).ToList();

        return new PhotoProcessingResult(
            new AnalyzerResult(_analyzer.Id, analysis.ModelVersion, CullingScores.ToScores(analysis.Signals)),
            faces,
            decision,
            $"{_engine.Version}+{analysis.ModelVersion}+{profile.Id}@{profile.Version}");
    }
}
