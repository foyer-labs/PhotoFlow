// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Metadata.ExifTool;
using SkiaSharp;

namespace PhotoFlow.Tests.Support;

/// <summary>The exiftool provisioned next to the test assembly by build/ExifTool.targets.</summary>
public static class ExifToolFixture
{
    public static ExifToolSession Start()
    {
        var options = ExifToolOptions.Bundled(AppContext.BaseDirectory);
        if (!File.Exists(options.ExecutablePath))
        {
            if (Environment.GetEnvironmentVariable("CI") is not null)
            {
                throw new FileNotFoundException("exiftool was not provisioned by the build.", options.ExecutablePath);
            }

            Assert.Skip("exiftool not provisioned (build with network access once).");
        }

        return new ExifToolSession(options);
    }

    /// <summary>Reads tags back with an independent exiftool call (no stay_open), as a user would.</summary>
    public static async Task<string> ReadTagAsync(ExifToolSession session, string path, string tag) =>
        (await session.ExecuteAsync(["-s3", "-sep", "|", "-" + tag, path], TestContext.Current.CancellationToken)).Output.Trim();
}

/// <summary>Synthetic JPEG images (no personal photos, no license issues).</summary>
public static class SyntheticImages
{
    public static string WriteJpeg(string path, int width = 96, int height = 64, int seed = 1)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var random = new Random(seed);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)(x * 255 / width), (byte)(y * 255 / height), (byte)random.Next(256)));
            }
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
