// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Imaging;

namespace PhotoFlow.Analysis.Signals;

/// <summary>Pixel rectangle inside an image.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Full(int width, int height) => new(0, 0, width, height);

    /// <summary>A normalized box mapped to pixels, clamped to the image.</summary>
    public static PixelRect FromBox(BoundingBox box, int width, int height)
    {
        var x1 = Math.Clamp((int)Math.Floor(box.X1 * width), 0, width - 1);
        var y1 = Math.Clamp((int)Math.Floor(box.Y1 * height), 0, height - 1);
        var x2 = Math.Clamp((int)Math.Ceiling(box.X2 * width), x1 + 1, width);
        var y2 = Math.Clamp((int)Math.Ceiling(box.Y2 * height), y1 + 1, height);
        return new PixelRect(x1, y1, x2 - x1, y2 - y1);
    }

    public int Area => Width * Height;
}

/// <summary>A grayscale image (8 bit).</summary>
public sealed class GrayImage(int width, int height, byte[] pixels)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public byte[] Pixels { get; } = pixels;
}

/// <summary>
/// Low-level image signals, written to reproduce OpenCV exactly (decision F1-4, verified against OpenCV in the lab):
/// <c>cvtColor(RGB2GRAY)</c>, <c>GaussianBlur(3×3)</c> on 8 bit, <c>Laplacian(CV_64F, ksize=1)</c>, all with BORDER_REFLECT_101.
/// </summary>
public static class ImageSignals
{
    /// <summary>OpenCV RGB → gray for 8 bit: fixed point Q15 with rounding (identical to OpenCV 5.0 on all 16.7M RGB values).</summary>
    public static GrayImage ToGray(RgbImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var rgb = image.Pixels.Span;
        var gray = new byte[image.Width * image.Height];
        for (int i = 0, j = 0; j < gray.Length; i += 3, j++)
        {
            gray[j] = (byte)((rgb[i] * 9798 + rgb[i + 1] * 19235 + rgb[i + 2] * 3735 + 16384) >> 15);
        }

        return new GrayImage(image.Width, image.Height, gray);
    }

    /// <summary>OpenCV 3×3 Gaussian (sigma 0 → kernel [1 2 1]/4), bit-exact 8-bit rounding: (S + 8) / 16.</summary>
    public static GrayImage GaussianBlur3(GrayImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        int w = image.Width, h = image.Height;
        var src = image.Pixels;
        var dst = new byte[src.Length];
        for (var y = 0; y < h; y++)
        {
            var ym = Reflect101(y - 1, h) * w;
            var y0 = y * w;
            var yp = Reflect101(y + 1, h) * w;
            for (var x = 0; x < w; x++)
            {
                var xm = Reflect101(x - 1, w);
                var xp = Reflect101(x + 1, w);
                var s = src[ym + xm] + 2 * src[ym + x] + src[ym + xp]
                        + 2 * (src[y0 + xm] + 2 * src[y0 + x] + src[y0 + xp])
                        + src[yp + xm] + 2 * src[yp + x] + src[yp + xp];
                dst[y0 + x] = (byte)((s + 8) >> 4);
            }
        }

        return new GrayImage(w, h, dst);
    }

    /// <summary>Population variance of the 4-neighbour Laplacian inside <paramref name="zone"/> (Laplacian computed on the whole image).</summary>
    public static double LaplacianVariance(GrayImage image, PixelRect zone)
    {
        ArgumentNullException.ThrowIfNull(image);
        int w = image.Width, h = image.Height;
        var p = image.Pixels;
        double sum = 0, sumSquares = 0;
        for (var y = zone.Y; y < zone.Y + zone.Height; y++)
        {
            var ym = Reflect101(y - 1, h) * w;
            var y0 = y * w;
            var yp = Reflect101(y + 1, h) * w;
            for (var x = zone.X; x < zone.X + zone.Width; x++)
            {
                double v = p[ym + x] + p[yp + x] + p[y0 + Reflect101(x - 1, w)] + p[y0 + Reflect101(x + 1, w)] - 4 * p[y0 + x];
                sum += v;
                sumSquares += v * v;
            }
        }

        var n = (double)zone.Area;
        var mean = sum / n;
        return Math.Max(0, sumSquares / n - mean * mean);
    }

    /// <summary>Sharpness of a zone: blur (noise suppression) then Laplacian variance.</summary>
    public static double Sharpness(GrayImage gray, PixelRect zone) => LaplacianVariance(GaussianBlur3(gray), zone);

    /// <summary>Clipped highlights (any channel ≥ 250), crushed shadows (gray ≤ 5) and mean brightness in a zone.</summary>
    public static ExposureStats Exposure(RgbImage image, GrayImage gray, PixelRect zone)
    {
        ArgumentNullException.ThrowIfNull(image);
        var rgb = image.Pixels.Span;
        long highlights = 0, shadows = 0, total = 0;
        for (var y = zone.Y; y < zone.Y + zone.Height; y++)
        {
            for (var x = zone.X; x < zone.X + zone.Width; x++)
            {
                var i = y * image.Width + x;
                var r = rgb[i * 3];
                var g = rgb[(i * 3) + 1];
                var b = rgb[(i * 3) + 2];
                if (r >= 250 || g >= 250 || b >= 250)
                {
                    highlights++;
                }

                var luma = gray.Pixels[i];
                if (luma <= 5)
                {
                    shadows++;
                }

                total += luma;
            }
        }

        var n = (double)zone.Area;
        return new ExposureStats(highlights / n, shadows / n, total / n / 255.0);
    }

    private static int Reflect101(int i, int n)
    {
        if (n == 1)
        {
            return 0;
        }

        if (i < 0)
        {
            return -i;
        }

        return i >= n ? (2 * n) - i - 2 : i;
    }
}

public sealed record ExposureStats(double HighlightClip, double ShadowClip, double MeanBrightness);
