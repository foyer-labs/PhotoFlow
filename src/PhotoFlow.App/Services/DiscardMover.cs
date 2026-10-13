// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.IO;

namespace PhotoFlow.App.Services;

/// <summary>
/// "Move the discards…" (P6, invariant I4): on the user's explicit request the discarded photos move into a
/// <c>_scarti</c> folder of their own library, keeping their relative path, together with their sidecar and their
/// <c>_original</c> copy. Only renames inside the library root (same volume): nothing is copied, nothing is deleted,
/// nothing is overwritten (a name already taken gets a suffix). Read-only libraries are left alone. The catalog
/// follows the move.
/// </summary>
public sealed class DiscardMover(ICatalog catalog, IFileSystem fileSystem)
{
    public const string FolderName = "_scarti";

    public async Task<(int Moved, int Skipped)> MoveAsync(IReadOnlyList<long> assetIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        var libraries = (await catalog.Libraries.ListAsync(ct).ConfigureAwait(false)).ToDictionary(l => l.Id);
        int moved = 0, skipped = 0;
        foreach (var assetId in assetIds)
        {
            foreach (var location in await catalog.Assets.GetLocationsAsync(assetId, ct).ConfigureAwait(false))
            {
                if (!libraries.TryGetValue(location.LibraryId, out var library) || library.IsReadOnly
                    || location.RelativePath.StartsWith(FolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var source = Path.Combine(library.RootPath, location.RelativePath);
                if (!fileSystem.FileExists(source))
                {
                    skipped++;
                    continue;
                }

                var relative = FreeName(library.RootPath, Path.Combine(FolderName, location.RelativePath));
                var target = Path.Combine(library.RootPath, relative);
                fileSystem.CreateDirectory(Path.GetDirectoryName(target)!);
                fileSystem.Move(source, target);
                foreach (var companion in new[] { (Path.ChangeExtension(source, ".xmp"), Path.ChangeExtension(target, ".xmp")), (source + "_original", target + "_original") })
                {
                    if (fileSystem.FileExists(companion.Item1) && !fileSystem.FileExists(companion.Item2))
                    {
                        fileSystem.Move(companion.Item1, companion.Item2);
                    }
                }

                await catalog.Assets.MoveLocationAsync(location.LibraryId, location.RelativePath, relative, CancellationToken.None).ConfigureAwait(false);
                moved++;
            }
        }

        return (moved, skipped);
    }

    private string FreeName(string root, string relative)
    {
        var candidate = relative;
        for (var n = 2; fileSystem.FileExists(Path.Combine(root, candidate)); n++)
        {
            candidate = Path.Combine(Path.GetDirectoryName(relative) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(relative)}-{n}{Path.GetExtension(relative)}");
        }

        return candidate;
    }
}
