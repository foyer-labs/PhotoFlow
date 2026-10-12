// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Paths;

/// <summary>
/// The only code allowed to delete files (invariant I4 after decision B1, spec §10): PhotoFlow's own files inside its
/// internal folders of <see cref="IAppPaths"/> — catalog backups, caches, temporary files, logs, downloads of models
/// and of the GPU pack. Any other path, a path escaping through "..", a look-alike folder or a link that leads
/// elsewhere is refused. The rules deciding <i>what</i> to delete live with their owners and call this class.
/// </summary>
public sealed class InternalFileCleaner
{
    private readonly IReadOnlyList<string> _roots;

    public InternalFileCleaner(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _roots = new[]
            {
                paths.BackupsDirectory, paths.CacheDirectory, paths.ThumbnailsDirectory, paths.TempDirectory,
                paths.LogsDirectory, paths.ModelsDirectory, paths.GpuRuntimeDirectory,
            }
            .Select(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)))
            .Where(r => !string.Equals(r, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(r) ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> Roots => _roots;

    /// <summary>True for a file strictly inside an internal folder, with no link along the way.</summary>
    public bool IsInternal(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var root = _roots.FirstOrDefault(r => full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            return false;
        }

        // Every element from the root down to the file must be a real entry, not a link (symbolic link, junction).
        for (var current = full; current.Length > root.Length; current = Path.GetDirectoryName(current)!)
        {
            if (IsLink(current))
            {
                return false;
            }
        }

        return !IsLink(root);
    }

    /// <exception cref="InvalidOperationException">The path is not an internal file of PhotoFlow.</exception>
    public void DeleteInternalFile(string path)
    {
        if (!IsInternal(path))
        {
            throw new InvalidOperationException($"Refusing to delete '{path}': not an internal file of PhotoFlow (invariant I4).");
        }

        File.Delete(Path.GetFullPath(path));
    }

    private static bool IsLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        return info.Exists && (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint));
    }
}
