// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Metadata;

namespace PhotoFlow.Metadata.ExifTool;

/// <summary>
/// XMP writing with exiftool (spec §4). Keywords are merged (never duplicated, never removed); RAW files only get a
/// <c>name.xmp</c> sidecar (invariant I2). Embedding in a non-RAW file goes through a verified replacement: exiftool
/// writes the new file to a fixed working name, the image data hash (pixels only) must be identical to the original,
/// and only then the original is atomically replaced (optionally keeping <c>name.ext_original</c>).
/// </summary>
public sealed class ExifToolMetadataWriter : IMetadataWriter
{
    public const string WorkingSuffix = ".pfxmp";

    private readonly ExifToolSession _exifTool;
    private readonly IFileSystem _fileSystem;
    private readonly bool _keepOriginalBackup;

    public ExifToolMetadataWriter(ExifToolSession exifTool, IFileSystem fileSystem, bool keepOriginalBackup = true)
    {
        ArgumentNullException.ThrowIfNull(exifTool);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _exifTool = exifTool;
        _fileSystem = fileSystem;
        _keepOriginalBackup = keepOriginalBackup;
    }

    public static string SidecarPath(string filePath) => Path.ChangeExtension(filePath, ".xmp");

    public async Task<MetadataWriteResult> WriteAsync(MetadataTarget target, MetadataChanges changes, WritePolicy policy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(changes);
        if (policy == WritePolicy.CatalogOnly)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.CatalogOnly, null, null);
        }

        var tags = TagArguments(changes);
        if (target.IsRaw || policy == WritePolicy.SidecarOnly)
        {
            return await WriteSidecarAsync(target.FilePath, tags, ct).ConfigureAwait(false);
        }

        var embedded = await WriteEmbeddedAsync(target.FilePath, tags, ct).ConfigureAwait(false);
        return embedded.Outcome == MetadataWriteOutcome.Failed && embedded.Error == CannotVerify
            ? await WriteSidecarAsync(target.FilePath, tags, ct).ConfigureAwait(false)
            : embedded;
    }

    private const string CannotVerify = "The image data of this format cannot be verified: metadata written to a sidecar instead.";

    /// <summary>exiftool arguments for the changes: rating and label set, keywords merged without duplicates.</summary>
    public static IReadOnlyList<string> TagArguments(MetadataChanges changes)
    {
        var args = new List<string>();
        if (changes.Rating is { } rating)
        {
            if (rating is < -1 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(changes), rating, "XMP rating must be between -1 and 5.");
            }

            args.Add("-XMP-xmp:Rating=" + rating.ToString(CultureInfo.InvariantCulture));
        }

        if (changes.Label is { } label)
        {
            args.Add("-XMP-xmp:Label=" + SingleLine(label));
        }

        foreach (var keyword in changes.FlatKeywords.Select(SingleLine).Distinct(StringComparer.Ordinal))
        {
            args.Add("-XMP-dc:Subject-=" + keyword);
            args.Add("-XMP-dc:Subject+=" + keyword);
        }

        foreach (var keyword in changes.HierarchicalKeywords.Select(SingleLine).Distinct(StringComparer.Ordinal))
        {
            args.Add("-XMP-lr:HierarchicalSubject-=" + keyword);
            args.Add("-XMP-lr:HierarchicalSubject+=" + keyword);
        }

        if (changes.Description is { } description)
        {
            args.Add("-XMP-dc:Description=" + SingleLine(description));
        }

        return args;
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private async Task<MetadataWriteResult> WriteSidecarAsync(string filePath, IReadOnlyList<string> tags, CancellationToken ct)
    {
        var sidecar = SidecarPath(filePath);
        IReadOnlyList<string> args = _fileSystem.FileExists(sidecar)
            ? ["-overwrite_original", .. tags, sidecar]
            : ["-o", sidecar, "-tagsFromFile", "@", "-xmp:all", .. tags, filePath]; // -o never overwrites
        var response = await _exifTool.ExecuteAsync(args, ct).ConfigureAwait(false);
        return _fileSystem.FileExists(sidecar) && !response.Errors.Contains("Error", StringComparison.Ordinal)
            ? new MetadataWriteResult(MetadataWriteOutcome.Sidecar, sidecar, null)
            : new MetadataWriteResult(MetadataWriteOutcome.Failed, null, Describe(response));
    }

    private async Task<MetadataWriteResult> WriteEmbeddedAsync(string filePath, IReadOnlyList<string> tags, CancellationToken ct)
    {
        var originalHash = await ImageDataHashAsync(filePath, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(originalHash))
        {
            return new MetadataWriteResult(MetadataWriteOutcome.Failed, null, CannotVerify);
        }

        var (bytes, errors) = await _exifTool.ExecuteBinaryAsync(["-o", "-", .. tags, filePath], ct).ConfigureAwait(false);
        if (bytes.Length == 0 || errors.Contains("Error", StringComparison.Ordinal))
        {
            return new MetadataWriteResult(MetadataWriteOutcome.Failed, null, string.IsNullOrWhiteSpace(errors) ? "exiftool produced no output." : errors);
        }

        // Fixed working name: a leftover from a failed attempt is overwritten next time, never deleted (I4).
        var working = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + WorkingSuffix + Path.GetExtension(filePath));
        await using (var stream = _fileSystem.OpenWrite(working))
        {
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        }

        var newHash = await ImageDataHashAsync(working, ct).ConfigureAwait(false);
        if (!string.Equals(originalHash, newHash, StringComparison.OrdinalIgnoreCase))
        {
            return new MetadataWriteResult(MetadataWriteOutcome.Failed, null, "Image data changed while writing metadata: original left untouched.");
        }

        var backup = filePath + "_original";
        var keepBackup = _keepOriginalBackup && !_fileSystem.FileExists(backup); // the first backup is the true original
        _fileSystem.Replace(working, filePath, keepBackup ? backup : null);
        return new MetadataWriteResult(MetadataWriteOutcome.Embedded, filePath, null);
    }

    private async Task<string?> ImageDataHashAsync(string path, CancellationToken ct)
    {
        var response = await _exifTool.ExecuteAsync(["-s3", "-ImageDataHash", path], ct).ConfigureAwait(false);
        var value = response.Output.Trim();
        return value.Length > 0 && value.All(Uri.IsHexDigit) ? value : null;
    }

    private static string Describe(ExifToolResponse response) =>
        string.IsNullOrWhiteSpace(response.Errors) ? response.Output.Trim() : response.Errors.Trim();
}
