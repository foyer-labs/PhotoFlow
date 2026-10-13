// SPDX-License-Identifier: Apache-2.0

using System.IO.Hashing;
using System.Text;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Metadata;

namespace PhotoFlow.App.Services;

public sealed record XmpWriteSummary(int Written, int Skipped, IReadOnlyList<(string Path, string Error)> Failed, IReadOnlyList<long> FailedAssets);

/// <summary>
/// Writes the confirmed decisions as XMP (spec §4, §6; boards 2g/2h), with the library policy. Discard → rating −1
/// (prudent mode: red label), to review → red label, user stars → rating. Photos that never had XMP and have nothing
/// to say (kept, similar, best without stars) are not touched; a photo written before gets its fields cleared. RAW
/// files only get sidecars (I2). Failures stay pending in <c>XmpSync</c> and do not stop the others.
/// </summary>
public sealed class XmpPublisher(ICatalog catalog, IMetadataWriter writer)
{
    public const string RedLabel = "Red";

    /// <summary>XMP fields for a decision, or null when nothing has to be written.</summary>
    public static MetadataChanges? ChangesFor(Decision? decision, bool prudentMode, bool writtenBefore)
    {
        var outcome = decision?.EffectiveOutcome;
        int? rating = decision?.Rating is { } stars and > 0 ? stars : outcome == CullingOutcome.Discard && !prudentMode ? -1 : null;
        var label = outcome == CullingOutcome.ToReview || (outcome == CullingOutcome.Discard && prudentMode) ? RedLabel : decision?.Label;
        if (rating is null && label is null)
        {
            return writtenBefore ? new MetadataChanges(0, string.Empty, [], [], null) : null;
        }

        return new MetadataChanges(rating ?? 0, label ?? string.Empty, [], [], null);
    }

    public static string Signature(MetadataChanges changes) =>
        Convert.ToHexString(XxHash3.Hash(Encoding.UTF8.GetBytes($"{changes.Rating}|{changes.Label}")));

    public async Task<XmpWriteSummary> WriteAsync(IReadOnlyList<long> assetIds, bool prudentMode, IProgress<int>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        int written = 0, skipped = 0, done = 0;
        var failed = new List<(string, string)>();
        var failedAssets = new List<long>();
        var libraries = (await catalog.Libraries.ListAsync(ct).ConfigureAwait(false)).ToDictionary(l => l.Id);
        foreach (var assetId in assetIds)
        {
            ct.ThrowIfCancellationRequested();
            var asset = await catalog.Assets.GetAsync(assetId, ct).ConfigureAwait(false);
            var sync = await catalog.XmpSync.GetAsync(assetId, ct).ConfigureAwait(false);
            var changes = ChangesFor(await catalog.Decisions.GetAsync(assetId, ct).ConfigureAwait(false), prudentMode, sync?.LastWrittenAt is not null);
            if (asset is null || changes is null || (sync?.XmpSignature == Signature(changes) && !sync.WritePending))
            {
                skipped++;
                done++;
                progress?.Report(done);
                continue;
            }

            foreach (var location in await catalog.Assets.GetLocationsAsync(assetId, ct).ConfigureAwait(false))
            {
                if (!libraries.TryGetValue(location.LibraryId, out var library))
                {
                    continue;
                }

                var path = Path.Combine(library.RootPath, location.RelativePath);
                var policy = library.IsReadOnly ? WritePolicy.CatalogOnly : library.XmpPolicy;
                var result = await writer.WriteAsync(new MetadataTarget(path, asset.Handler == "raw"), changes, policy, ct).ConfigureAwait(false);
                if (result.Outcome == MetadataWriteOutcome.Failed)
                {
                    failed.Add((path, result.Error ?? string.Empty));
                    failedAssets.Add(assetId);
                    await catalog.XmpSync.MarkPendingAsync(assetId, result.Error, ct).ConfigureAwait(false);
                }
                else
                {
                    written++;
                    await catalog.XmpSync.MarkWrittenAsync(assetId, Signature(changes), ct).ConfigureAwait(false);
                }
            }

            done++;
            progress?.Report(done);
        }

        return new XmpWriteSummary(written, skipped, failed, failedAssets.Distinct().ToList());
    }
}
