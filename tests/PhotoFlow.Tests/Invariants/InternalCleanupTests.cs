// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Paths;
using PhotoFlow.Tests.Support;

namespace PhotoFlow.Tests.Invariants;

/// <summary>
/// I4 after decision B1: the only deletions allowed are PhotoFlow's own files inside its internal folders of
/// <see cref="IAppPaths"/>; anything else (user files, look-alike paths, paths escaping through "..") is refused.
/// </summary>
[Trait(TestCategory.Name, TestCategory.Unit)]
public sealed class InternalCleanupTests : IDisposable
{
    private readonly TempFolder _temp = new("pf-cleanup");

    [Fact]
    public void Files_inside_the_internal_folders_can_be_deleted()
    {
        var paths = new DefaultAppPaths(_temp.Path);
        var cleaner = new InternalFileCleaner(paths);
        foreach (var folder in new[] { paths.BackupsDirectory, paths.CacheDirectory, paths.ThumbnailsDirectory, paths.TempDirectory, paths.LogsDirectory, paths.ModelsDirectory, paths.GpuRuntimeDirectory })
        {
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "old.bin");
            File.WriteAllText(file, "x");
            Assert.True(cleaner.IsInternal(file), file);
            cleaner.DeleteInternalFile(file);
            Assert.False(File.Exists(file));
        }
    }

    [Fact]
    public void Anything_outside_the_internal_folders_is_refused()
    {
        var paths = new DefaultAppPaths(_temp.Path);
        var cleaner = new InternalFileCleaner(paths);
        Directory.CreateDirectory(paths.CacheDirectory);
        var photo = Path.Combine(_temp.Sub("library"), "DSC_0001.NEF");
        File.WriteAllText(photo, "raw");
        var lookAlike = Path.Combine(paths.CacheDirectory + "2", "x.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(lookAlike)!);
        File.WriteAllText(lookAlike, "x");
        var escaping = Path.Combine(paths.CacheDirectory, "..", "..", "library", "DSC_0001.NEF");

        foreach (var path in new[] { photo, lookAlike, escaping, paths.CatalogPath, paths.CacheDirectory, Path.Combine(paths.ConfigDirectory, "settings.json") })
        {
            Assert.False(cleaner.IsInternal(path), path);
            Assert.Throws<InvalidOperationException>(() => cleaner.DeleteInternalFile(path));
        }

        Assert.True(File.Exists(photo));
        Assert.True(File.Exists(lookAlike));
    }

    [Fact]
    public void A_link_inside_an_internal_folder_that_points_elsewhere_is_refused()
    {
        var paths = new DefaultAppPaths(_temp.Path);
        var cleaner = new InternalFileCleaner(paths);
        Directory.CreateDirectory(paths.CacheDirectory);
        var outside = _temp.Sub("photos");
        File.WriteAllText(Path.Combine(outside, "a.jpg"), "jpeg");
        var link = Path.Combine(paths.CacheDirectory, "linked");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here (Windows without developer mode).");
        }

        var target = Path.Combine(link, "a.jpg");
        Assert.False(cleaner.IsInternal(target));
        Assert.Throws<InvalidOperationException>(() => cleaner.DeleteInternalFile(target));
        Assert.True(File.Exists(Path.Combine(outside, "a.jpg")));
    }

    public void Dispose() => _temp.Dispose();
}
