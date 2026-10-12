// SPDX-License-Identifier: Apache-2.0

namespace PhotoFlow.Core.Sources;

/// <summary>Import source (SD card or folder). Read-only by contract: invariant I1.</summary>
public interface ISource
{
    IAsyncEnumerable<SourceItem> EnumerateAsync(CancellationToken ct);
}

public sealed record SourceItem(string FullPath, string RelativePath, long Length, DateTimeOffset LastWriteTime);
