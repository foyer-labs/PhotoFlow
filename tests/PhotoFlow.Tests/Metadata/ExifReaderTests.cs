// SPDX-License-Identifier: Apache-2.0

using PhotoFlow.Core.Catalog;
using PhotoFlow.Metadata.ExifTool;
using PhotoFlow.Tests.Support;

namespace PhotoFlow.Tests.Metadata;

[Trait(TestCategory.Name, TestCategory.Integration)]
public sealed class ExifReaderTests : IDisposable
{
    private readonly TempFolder _temp = new("pf-exif");

    [Fact]
    public async Task Reads_the_spec_fields_from_real_nefs_in_one_batch()
    {
        await using var exifTool = ExifToolFixture.Start();
        var d40 = await PublicSamples.RequireAsync("nikon-d40-lossy");
        var z8 = await PublicSamples.RequireAsync("nikon-z8-high-efficiency");
        var noExif = SyntheticImages.WriteJpeg(Path.Combine(_temp.Path, "plain.jpg"));
        var reader = new ExifToolExifReader(exifTool, TimeZoneInfo.Utc);

        var infos = await reader.ReadAsync([d40, z8, noExif], TestContext.Current.CancellationToken);

        Assert.Equal([d40, z8, noExif], infos.Select(i => i.Path));
        var nikon = infos[0];
        Assert.Equal("NEF", nikon.FileType);
        Assert.Equal("NIKON D40", nikon.Model);
        Assert.Equal(DateSource.Exif, nikon.CaptureDateSource);
        Assert.NotNull(nikon.CapturedAt);
        Assert.True(nikon.Iso > 0);
        Assert.True(nikon.ExposureTimeSeconds > 0);
        Assert.True(nikon.Aperture > 0);
        Assert.True(nikon.EmbeddedJpegLength > 100_000, "D40 NEFs embed a full-size JPEG");
        Assert.False(nikon.IsHighEfficiencyRaw);

        Assert.Equal("NIKON Z 8", infos[1].Model);
        Assert.True(infos[1].IsHighEfficiencyRaw, $"Z8 HE sample, NEFCompression = {infos[1].RawCompression}");

        Assert.Equal("JPEG", infos[2].FileType);
        Assert.Equal(DateSource.File, infos[2].CaptureDateSource);
    }

    [Fact]
    public async Task Sub_seconds_and_offsets_are_kept_for_burst_ordering()
    {
        await using var exifTool = ExifToolFixture.Start();
        var jpeg = SyntheticImages.WriteJpeg(Path.Combine(_temp.Path, "burst.jpg"));
        await exifTool.ExecuteAsync(
            ["-overwrite_original", "-DateTimeOriginal=2026:10:12 14:41:12", "-SubSecTimeOriginal=83", "-OffsetTimeOriginal=+02:00", jpeg],
            TestContext.Current.CancellationToken);

        var info = Assert.Single(await new ExifToolExifReader(exifTool).ReadAsync([jpeg], TestContext.Current.CancellationToken));

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 14, 41, 12, 830, TimeSpan.FromHours(2)), info.CapturedAt);
        Assert.Equal(DateSource.Exif, info.CaptureDateSource);
    }

    public void Dispose() => _temp.Dispose();
}
