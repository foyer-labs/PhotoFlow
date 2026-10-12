// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Imaging;

namespace PhotoFlow.Core.Analysis;

/// <summary>Image embedding (CLIP): bursts, similar photos, zero-shot profile and tags. Vectors are L2-normalized.</summary>
public interface IImageEmbedder
{
    string ModelVersion { get; }

    int Dimensions { get; }

    Task<float[]> EmbedAsync(RgbImage image, CancellationToken ct);
}

/// <summary>Text embedding in the same space as <see cref="IImageEmbedder"/> (zero-shot, natural-language search).</summary>
public interface ITextEmbedder
{
    string ModelVersion { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}

public static class Vectors
{
    /// <summary>Cosine similarity of two L2-normalized vectors (their dot product).</summary>
    public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors of different length.", nameof(b));
        }

        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * (double)b[i];
        }

        return (float)sum;
    }

    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        var sum = 0.0;
        foreach (var v in vector)
        {
            sum += v * (double)v;
        }

        var norm = Math.Sqrt(sum);
        var result = vector.ToArray();
        if (norm > 0)
        {
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (float)(result[i] / norm);
            }
        }

        return result;
    }
}
