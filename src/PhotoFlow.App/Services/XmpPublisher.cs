// SPDX-License-Identifier: Apache-2.0

using System.IO.Hashing;
using System.Text;
using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Metadata;

namespace PhotoFlow.App.Services;

public sealed record XmpWriteSummary(int Written, int Skipped, IReadOnlyList<(string Path, string Error)> Failed, IReadOnlyList<long> FailedAssets);

/// <summary>
/// Writes the confirmed decisions as XMP (spec §4, §6; boards 2g/2h), with the library policy. Discard → rating −1
/// (prudent mode: red label), to review → red label, user stars → rating. Photos that never had XMP and have nothing
/// to say (kept, similar, best without stars) are not touched; a photo written before gets its fields cleared. RAW
/// files only get sidecars (I2). Failures stay pending in <c>XmpSync</c> and do not stop the others. Named people on
/// the photo (phase 2, spec §7) become MWG face regions and the keywords <c>Name</c> and <c>Persone|Name</c>; the
/// regions of other software are kept; unnamed groups and ignored people are not written.
/// </summary>
/// <param name="writeCaptions">Captions in <c>dc:description</c> (spec §8, off by default); the user's are written too.</param>
public sealed class XmpPublisher(ICatalog catalog, IMetadataWriter writer, bool writeCaptions = false)
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

    public const string PeopleKeywordRoot = "Persone";

    public static string Signature(MetadataChanges changes) =>
        Convert.ToHexString(XxHash3.Hash(Encoding.UTF8.GetBytes(
            $"{changes.Rating}|{changes.Label}|{string.Join(',', changes.FlatKeywords)}|-{string.Join(',', changes.RemovedFlatKeywords ?? [])}|"
            + string.Join(';', changes.Regions?.Regions.Select(r => FormattableString.Invariant($"{r.Name}:{r.CenterX:0.####},{r.CenterY:0.####}")) ?? [])
            + (changes.Description is { } description ? "|d:" + description : string.Empty))));

    /// <summary>The caption of the photo in <c>dc:description</c> (an empty caption of the user clears it).</summary>
    public static MetadataChanges? WithCaption(MetadataChanges? changes, Caption? caption)
    {
        if (caption is null)
        {
            return changes;
        }

        return (changes ?? new MetadataChanges(null, null, [], [], null)) with { Description = caption.Text };
    }

    /// <summary>Adds the named people of the photo to the changes (or makes changes for them alone).</summary>
    public static MetadataChanges? WithPeople(MetadataChanges? changes, IReadOnlyList<(string Name, BoundingBox Box)> people, (int Width, int Height)? size, IReadOnlyCollection<string> managedNames)
    {
        ArgumentNullException.ThrowIfNull(people);
        if (people.Count == 0)
        {
            return changes;
        }

        var names = people.Select(p => p.Name).Distinct(StringComparer.Ordinal).ToList();
        var regions = size is { } s
            ? new FaceRegions(s.Width, s.Height, people.Select(p => new FaceRegion(p.Name, (p.Box.X1 + p.Box.X2) / 2.0, (p.Box.Y1 + p.Box.Y2) / 2.0, p.Box.X2 - p.Box.X1, p.Box.Y2 - p.Box.Y1)).ToList(), managedNames)
            : null;
        var baseChanges = changes ?? new MetadataChanges(null, null, [], [], null);
        return baseChanges with
        {
            FlatKeywords = [.. baseChanges.FlatKeywords, .. names],
            HierarchicalKeywords = [.. baseChanges.HierarchicalKeywords, .. names.Select(n => $"{PeopleKeywordRoot}|{n}")],
            Regions = regions,
        };
    }

    /// <summary>
    /// Adds the tags of the photo (spec §4, §8): the English label always, the Italian one once reviewed, and
    /// <c>Category|Label</c> in the hierarchy (Italian once reviewed, English before); blacklisted tags are never written. The keywords of tags the user removed
    /// (or blacklisted) are taken out of the file by <see cref="WithRemovals"/>, only if PhotoFlow wrote them before.
    /// </summary>
    public static MetadataChanges? WithTags(MetadataChanges? changes, IReadOnlyList<AssetTagInfo> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var active = tags.Where(t => t.Assignment.State == AssetTagState.Active && !t.Tag.Blacklisted).Select(t => t.Tag).ToList();
        if (active.Count == 0)
        {
            return changes;
        }

        var baseChanges = changes ?? new MetadataChanges(null, null, [], [], null);
        return baseChanges with
        {
            FlatKeywords = [.. baseChanges.FlatKeywords, .. active.SelectMany(Flat)],
            HierarchicalKeywords = [.. baseChanges.HierarchicalKeywords, .. active.SelectMany(Hierarchical)],
        };

        static IEnumerable<string> Flat(Tag tag) =>
            new[] { tag.English, tag.Reviewed ? tag.Italian : null }.OfType<string>().Distinct(StringComparer.Ordinal);

        // Spec §4: the hierarchy is in Italian; a tag not reviewed yet is written in English only (there too).
        static IEnumerable<string> Hierarchical(Tag tag) => tag.Category is not { } category
            ? []
            : [$"{category}|{Capitalize(tag.Reviewed && tag.Italian is { } italian ? italian : tag.English)}"];

        static string Capitalize(string label) => label.Length == 0 ? label : char.ToUpper(label[0], System.Globalization.CultureInfo.InvariantCulture) + label[1..];
    }

    /// <summary>The keywords PhotoFlow wrote last time that no longer apply are removed; nobody else's keywords are touched.</summary>
    public static MetadataChanges? WithRemovals(MetadataChanges? changes, XmpSyncState? sync)
    {
        if (sync is null || (sync.WrittenFlat.Count == 0 && sync.WrittenHierarchical.Count == 0))
        {
            return changes;
        }

        var baseChanges = changes ?? new MetadataChanges(null, null, [], [], null);
        var flat = sync.WrittenFlat.Except(baseChanges.FlatKeywords, StringComparer.Ordinal).ToList();
        var hierarchical = sync.WrittenHierarchical.Except(baseChanges.HierarchicalKeywords, StringComparer.Ordinal).ToList();
        return flat.Count == 0 && hierarchical.Count == 0 && changes is null
            ? null
            : baseChanges with { RemovedFlatKeywords = flat, RemovedHierarchicalKeywords = hierarchical };
    }

    public async Task<XmpWriteSummary> WriteAsync(IReadOnlyList<long> assetIds, bool prudentMode, IProgress<int>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        int written = 0, skipped = 0, done = 0;
        var failed = new List<(string, string)>();
        var failedAssets = new List<long>();
        var libraries = (await catalog.Libraries.ListAsync(ct).ConfigureAwait(false)).ToDictionary(l => l.Id);
        var persons = (await catalog.People.ListPersonsAsync(ct).ConfigureAwait(false)).Select(p => p.Person).ToDictionary(p => p.Id);
        var managed = persons.Values.Where(p => p.Name is not null).Select(p => p.Name!).ToHashSet(StringComparer.Ordinal);
        foreach (var assetId in assetIds)
        {
            ct.ThrowIfCancellationRequested();
            var asset = await catalog.Assets.GetAsync(assetId, ct).ConfigureAwait(false);
            var sync = await catalog.XmpSync.GetAsync(assetId, ct).ConfigureAwait(false);
            var changes = ChangesFor(await catalog.Decisions.GetAsync(assetId, ct).ConfigureAwait(false), prudentMode, sync?.LastWrittenAt is not null);
            if (asset is not null)
            {
                // The faces live on the analyzed photo: a RAW gets the people of its JPEG twin.
                var faces = await catalog.Analysis.GetFacesAsync(assetId, ct).ConfigureAwait(false);
                if (faces.Count == 0 && await catalog.Assets.GetTwinAsync(assetId, ct).ConfigureAwait(false) is { } twin)
                {
                    faces = await catalog.Analysis.GetFacesAsync(twin, ct).ConfigureAwait(false);
                }

                var people = faces
                    .Where(f => f.PersonId is { } id && persons.TryGetValue(id, out var person) && person.Name is not null && !person.Ignored)
                    .Select(f => (persons[f.PersonId!.Value].Name!, f.Box))
                    .ToList();
                changes = WithPeople(changes, people, asset.Width is { } w && asset.Height is { } h ? (w, h) : null, managed);

                // Tags live on the analyzed photo too.
                var tags = await catalog.Tags.GetForAssetAsync(assetId, ct).ConfigureAwait(false);
                if (tags.Count == 0 && await catalog.Assets.GetTwinAsync(assetId, ct).ConfigureAwait(false) is { } tagTwin)
                {
                    tags = await catalog.Tags.GetForAssetAsync(tagTwin, ct).ConfigureAwait(false);
                }

                changes = WithRemovals(WithTags(changes, tags), sync);
                if (writeCaptions)
                {
                    var caption = await catalog.Captions.GetAsync(assetId, ct).ConfigureAwait(false);
                    if (caption is null && await catalog.Assets.GetTwinAsync(assetId, ct).ConfigureAwait(false) is { } captionTwin)
                    {
                        caption = await catalog.Captions.GetAsync(captionTwin, ct).ConfigureAwait(false);
                    }

                    changes = WithCaption(changes, caption);
                }
            }
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
                    await catalog.XmpSync.MarkWrittenAsync(assetId, Signature(changes), ct, changes.FlatKeywords, changes.HierarchicalKeywords).ConfigureAwait(false);
                }
            }

            done++;
            progress?.Report(done);
        }

        return new XmpWriteSummary(written, skipped, failed, failedAssets.Distinct().ToList());
    }
}
