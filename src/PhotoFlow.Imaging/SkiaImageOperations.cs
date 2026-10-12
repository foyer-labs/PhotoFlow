// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Imaging;
using SkiaSharp;

namespace PhotoFlow.Imaging;

/// <summary>
/// Decoding with SkiaSharp (no color-space conversion, no EXIF orientation: raw pixel order,
/// as Pillow's <c>Image.open</c>), resampling with <see cref="PillowResampler"/>.
/// </summary>
public sealed class SkiaImageOperations : IImageOperations
{
    public async Task<RgbImage> DecodeAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    public RgbImage Resize(RgbImage image, int width, int height, ResampleMode mode) =>
        PillowResampler.Resize(image, width, height, mode);

    private static RgbImage Decode(ReadOnlySpan<byte> encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("Unsupported or corrupt image data.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Image decoding failed: {result}.");
        }

        var rgba = bitmap.GetPixelSpan();
        var rgb = new byte[info.Width * info.Height * 3];
        for (int i = 0, j = 0; j < rgb.Length; i += 4, j += 3)
        {
            rgb[j] = rgba[i];
            rgb[j + 1] = rgba[i + 1];
            rgb[j + 2] = rgba[i + 2];
        }

        return new RgbImage(info.Width, info.Height, rgb);
    }
}
