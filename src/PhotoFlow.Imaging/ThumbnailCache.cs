// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Imaging;
using PhotoFlow.Core.IO;
using SkiaSharp;

namespace PhotoFlow.Imaging;

/// <summary>
/// Thumbnails for the grid (cells up to 240 px, 2× for high-DPI screens): JPEG, long side 512, keyed by the content hash
/// so they follow the photo when it moves. Always local and rebuildable (spec §9). Written atomically (temp + rename).
/// </summary>
public sealed class ThumbnailCache
{
    public const int Size = 512;
    private const int Quality = 85;

    private readonly IFileSystem _fileSystem;
    private readonly string _root;

    public ThumbnailCache(IFileSystem fileSystem, string thumbnailsDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailsDirectory);
        _fileSystem = fileSystem;
        _root = thumbnailsDirectory;
    }

    public string PathFor(string contentHash) => Path.Combine(_root, contentHash[..2], contentHash + ".jpg");

    public bool Exists(string contentHash) => _fileSystem.FileExists(PathFor(contentHash));

    /// <summary>Stores the thumbnail of a decoded preview (downscaled to <see cref="Size"/>); returns its path.</summary>
    public async Task<string> StoreAsync(string contentHash, RgbImage preview, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(preview);
        var path = PathFor(contentHash);
        if (_fileSystem.FileExists(path))
        {
            return path;
        }

        var thumbnail = ImageTransforms.FitWithin(preview, Size);
        var encoded = Encode(thumbnail);
        _fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await using (var stream = _fileSystem.OpenWrite(temp))
        {
            await stream.WriteAsync(encoded, ct).ConfigureAwait(false);
        }

        try
        {
            _fileSystem.Move(temp, path);
        }
        catch (IOException) when (_fileSystem.FileExists(path))
        {
            // Another worker stored the same thumbnail meanwhile: the temp file is overwritten next time.
        }

        return path;
    }

    private static byte[] Encode(RgbImage image)
    {
        var rgba = new byte[image.Width * image.Height * 4];
        var rgb = image.Pixels.Span;
        for (int i = 0, j = 0; i < rgb.Length; i += 3, j += 4)
        {
            rgba[j] = rgb[i];
            rgba[j + 1] = rgb[i + 1];
            rgba[j + 2] = rgb[i + 2];
            rgba[j + 3] = 255;
        }

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var skImage = SKImage.FromPixelCopy(info, rgba);
        using var data = skImage.Encode(SKEncodedImageFormat.Jpeg, Quality);
        return data.ToArray();
    }
}
