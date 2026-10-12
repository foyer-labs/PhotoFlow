// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Analysis;
using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Core.Tagging;

public interface ITagger
{
    TaggingLevel Level { get; }

    Task<IReadOnlyList<TagResult>> TagAsync(PhotoContext photo, CancellationToken ct);
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

public sealed record TagResult(string TagKey, TagSource Source, double Confidence, string ModelVersion);
