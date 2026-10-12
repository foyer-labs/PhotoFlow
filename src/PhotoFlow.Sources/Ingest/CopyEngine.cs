// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Ingest;
using PhotoFlow.Core.IO;

namespace PhotoFlow.Sources.Ingest;

/// <summary>Verified copy: single read of the source, parallel writes, xxHash3 while reading, re-read to verify.</summary>
public sealed class CopyEngine : ICopyEngine
{
    private readonly IFileSystem _fileSystem;

    public CopyEngine(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public Task<CopyResult> CopyAsync(IngestFile file, IReadOnlyList<IngestDestination> destinations, CancellationToken ct) =>
        throw new NotImplementedException();
}
