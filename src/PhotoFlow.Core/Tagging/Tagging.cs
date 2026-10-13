// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;
using PhotoFlow.Core.Imaging;

namespace PhotoFlow.Core.Tagging;

/// <summary>A tagger that looks at the image (level B, RAM++): the tags above their thresholds with their confidence.</summary>
public interface IImageTagger
{
    /// <summary>Recorded with every tag it produces (CLAUDE.md §4).</summary>
    string ModelVersion { get; }

    Task<IReadOnlyList<TagResult>> TagAsync(RgbImage image, CancellationToken ct);
}

/// <summary>Tagging levels of spec §5.</summary>
public enum TaggingLevel
{
    /// <summary>A: CLIP zero-shot on the personal vocabulary.</summary>
    ZeroShot,

    /// <summary>B: RAM++.</summary>
    Tagger,

    /// <summary>C: Florence-2 captions.</summary>
    Caption,

    /// <summary>D: VLM captions (GPU only).</summary>
    VlmCaption,
}

/// <summary>Tagging levels chosen for a job (spec §5): A runs on the stored embeddings, B looks at every photo.</summary>
[Flags]
public enum TagLevels
{
    None = 0,

    /// <summary>A: personal CLIP tags.</summary>
    ZeroShot = 1,

    /// <summary>B: RAM++.</summary>
    Tagger = 2,

    Default = ZeroShot | Tagger,
}

/// <param name="TagKey">Stable English key (spec §8).</param>
public sealed record TagResult(string TagKey, TagSource Source, double Confidence, string ModelVersion);

/// <summary>OCR (spec §8): the text lines of an image.</summary>
public interface ITextReader
{
    string ModelVersion { get; }

    Task<IReadOnlyList<TextReading>> ReadAsync(RgbImage image, CancellationToken ct);
}

/// <param name="Box">Normalized to the image read.</param>
public sealed record TextReading(BoundingBox Box, string Text, double Confidence);
