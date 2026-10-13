// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Pipeline;

/// <summary>What a regeneration computes again (spec §5, "Cosa": selectable).</summary>
[Flags]
public enum ReanalysisParts
{
    None = 0,

    /// <summary>Culling scores and the automatic proposal (the user's decisions stay).</summary>
    Culling = 1,

    Embedding = 2,

    /// <summary>Faces and fingerprints (new face model: assignments transferred, spec §5).</summary>
    Faces = 4,

    /// <summary>Tags of level B (manual and suppressed tags stay, I5).</summary>
    Tags = 8,

    Captions = 16,

    Ocr = 32,

    All = Culling | Embedding | Faces | Tags | Captions | Ocr,
}

public enum ReanalysisMode
{
    /// <summary>Only the results made by a model older than the current one (default).</summary>
    OlderModelOnly,

    ForceAll,
}

/// <summary>
/// A regeneration (job <c>Reanalyze</c>, spec §5, D14). Perimeter: a library, a folder of it (relative path prefix),
/// the photos of a job, or chosen photos (a search result, one photo).
/// </summary>
/// <param name="ExcludeReviewed">Leave out the photos already reviewed (a decision of the user or an XMP written).</param>
/// <param name="Sample">A trial on that many photos of the perimeter, chosen at random (repeatable).</param>
public sealed record ReanalysisSettings(
    ReanalysisParts Parts,
    ReanalysisMode Mode = ReanalysisMode.OlderModelOnly,
    long? LibraryId = null,
    string? Folder = null,
    long? SourceJobId = null,
    IReadOnlyList<long>? AssetIds = null,
    bool ExcludeReviewed = false,
    int? Sample = null,
    string ProfileId = "events",
    int Workers = 1);

/// <summary>New results of a photo waiting for confirmation (one JSON per part).</summary>
public sealed record StagedCulling(Analysis.AnalyzerResult Analysis, string ProfileId, Analysis.DecisionProposal Decision, string DecisionVersion);

/// <summary>The versions the current models would record (to find the results of older models).</summary>
/// <param name="AnalyzerId">Id of the culling analyzer whose results carry <paramref name="Culling"/>.</param>
public sealed record ModelVersions(string AnalyzerId, string? Culling, string? Embedding, string? Fingerprints, string? Tags, string? Captions, string? Ocr);

/// <summary>Differences between the staged results of a regeneration and the catalog (spec §5: "Anteprima delle differenze").</summary>
public sealed record ReanalysisPreview(
    int Photos,
    int OutcomesChanged,
    int UserDecisionsKept,
    int TagsAdded,
    int TagsRemoved,
    int TagsProtected,
    int FacesTransferred,
    int FacesNew,
    int FacesLost,
    int FacesKeptManual,
    int CaptionsChanged,
    int CaptionsProtected,
    int Embeddings,
    int OcrChanged,
    IReadOnlyList<string> Examples);
