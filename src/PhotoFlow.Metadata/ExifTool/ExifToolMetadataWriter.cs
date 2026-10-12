// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Metadata;

namespace PhotoFlow.Metadata.ExifTool;

/// <summary>XMP writing through exiftool: merge, never overwrite; RAW files only get sidecars (invariant I2).</summary>
public sealed class ExifToolMetadataWriter : IMetadataWriter
{
    public Task<MetadataWriteResult> WriteAsync(MetadataTarget target, MetadataChanges changes, WritePolicy policy, CancellationToken ct) =>
        throw new NotImplementedException();
}
