// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using PhotoFlow.Core.IO;
using PhotoFlow.Core.Sources;

namespace PhotoFlow.Sources.Scanning;

/// <summary>
/// Read-only scan of an import source (card or folder): every file is classified from its first bytes
/// (signature), the extension only disambiguates TIFF-based RAW formats.
/// </summary>
public sealed class SourceScanner : ISource
{
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nef", ".nrw", ".cr2", ".cr3", ".crw", ".arw", ".srf", ".sr2", ".raf", ".dng", ".orf", ".rw2", ".pef", ".srw", ".x3f",
        ".3fr", ".iiq", ".rwl", ".erf", ".mef", ".mos", ".kdc", ".mrw", ".raw",
    };

    private readonly IFileSystem _fileSystem;
    private readonly string _root;

    public SourceScanner(IFileSystem fileSystem, string root)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _fileSystem = fileSystem;
        _root = Path.GetFullPath(root);
    }

    public async IAsyncEnumerable<SourceItem> EnumerateAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var file in ScanAsync(ct).ConfigureAwait(false))
        {
            yield return file.Item;
        }
    }

    public async IAsyncEnumerable<ScannedFile> ScanAsync([EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var path in _fileSystem.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            if (name.StartsWith('.') || name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                continue; // system and hidden files (e.g. ._DSC_0001.NEF from macOS) are not photos
            }

            var metadata = _fileSystem.GetFileMetadata(path);
            var header = await ReadHeaderAsync(path, ct).ConfigureAwait(false);
            var item = new SourceItem(path, Path.GetRelativePath(_root, path), metadata.Length, metadata.LastWriteTimeUtc);
            yield return new ScannedFile(item, Classify(header, Path.GetExtension(path)));
        }
    }

    /// <summary>Kind from the signature; the extension only separates RAW from plain TIFF.</summary>
    public static MediaKind Classify(ReadOnlySpan<byte> header, string extension)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return MediaKind.Jpeg;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return MediaKind.Png;
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return MediaKind.WebP;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual("FUJIFILM"u8))
        {
            return MediaKind.Raw;
        }

        if (header.Length >= 4 && (header[..4].SequenceEqual("IIRO"u8) || header[..4].SequenceEqual("IIRS"u8)
                                    || header[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x55, 0x00 })))
        {
            return MediaKind.Raw; // Olympus ORF, Panasonic RW2
        }

        if (header.Length >= 4 && (header[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || header[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })))
        {
            return RawExtensions.Contains(extension) ? MediaKind.Raw : MediaKind.Tiff;
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = header[8..12];
            if (brand.SequenceEqual("crx "u8))
            {
                return MediaKind.Raw; // Canon CR3
            }

            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8)
                || brand.SequenceEqual("heif"u8) || brand.SequenceEqual("avif"u8) || brand.SequenceEqual("hevc"u8))
            {
                return MediaKind.Heif;
            }

            return MediaKind.Video; // isom, mp41/42, qt, M4V…
        }

        if (header.Length >= 8 && (header[4..8].SequenceEqual("moov"u8) || header[4..8].SequenceEqual("mdat"u8) || header[4..8].SequenceEqual("wide"u8)))
        {
            return MediaKind.Video; // older QuickTime
        }

        if (header.Length >= 5 && header[4] == 0x47 && extension.Equals(".mts", StringComparison.OrdinalIgnoreCase))
        {
            return MediaKind.Video; // AVCHD transport stream
        }

        if (header.Length >= 4 && header[..4].SequenceEqual("RIFF"u8) && extension.Equals(".avi", StringComparison.OrdinalIgnoreCase))
        {
            return MediaKind.Video;
        }

        return MediaKind.Other;
    }

    private async Task<byte[]> ReadHeaderAsync(string path, CancellationToken ct)
    {
        var buffer = new byte[16];
        await using var stream = _fileSystem.OpenRead(path);
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
        return buffer[..read];
    }
}
