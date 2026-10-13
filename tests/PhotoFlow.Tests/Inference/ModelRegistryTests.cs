// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using PhotoFlow.Core.Inference;
using PhotoFlow.Inference.Models;
using PhotoFlow.Tests.Support;

namespace PhotoFlow.Tests.Inference;

[Trait(TestCategory.Name, TestCategory.Unit)]
public sealed class ModelRegistryTests : IDisposable
{
    private readonly TempFolder _temp = new("pf-models");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_embedded_manifest_matches_the_repository_manifest()
    {
        var models = ModelRegistry.EmbeddedManifest();
        Assert.Equal(9, models.Count);
        Assert.Equal(ModelFunction.Ocr, models.Single(m => m.Id == "ppocr-en").Function);
        Assert.True(models.Single(m => m.Id == "insightface-buffalo-l").License.RequiresAcceptance);
        Assert.Equal(ModelFunction.ObjectDetection, models.Single(m => m.Id == "dfine-s-obj2coco").Function);
    }

    [Fact]
    public async Task A_model_whose_license_requires_acceptance_is_not_downloaded_without_it()
    {
        var (registry, server) = Arrange(requiresAcceptance: true, zip: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.InstallAsync("m", licenseAccepted: false, null, Ct));

        Assert.Equal(0, server.Requests);
        Assert.False(registry.IsInstalled("m"));
    }

    [Fact]
    public async Task Archives_are_verified_and_only_the_used_members_are_extracted()
    {
        var (registry, server) = Arrange(requiresAcceptance: true, zip: true);

        await registry.InstallAsync("m", licenseAccepted: true, null, Ct);
        await registry.InstallAsync("m", licenseAccepted: true, null, Ct);

        Assert.True(registry.IsInstalled("m"));
        Assert.Equal(1, server.Requests);
        var descriptor = registry.Resolve("m", "det.onnx");
        Assert.True(File.Exists(descriptor.FilePath));
        Assert.False(File.Exists(Path.Combine(_temp.Path, "m", "unused.onnx")));
        Assert.True(await registry.VerifyAsync("m", Ct));
    }

    [Fact]
    public async Task A_tampered_download_is_rejected()
    {
        var (registry, server) = Arrange(requiresAcceptance: false, zip: false);
        server.Tamper = true;

        await Assert.ThrowsAsync<InvalidDataException>(() => registry.InstallAsync("m", licenseAccepted: false, null, Ct));

        Assert.False(registry.IsInstalled("m"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_offline_package_installs_without_network_and_leaves_the_folder_untouched(bool inModelFolder)
    {
        var (registry, server, payload) = ArrangeWithPayload(requiresAcceptance: true, zip: true, Path.Combine(_temp.Path, "models"));
        var package = Directory.CreateDirectory(Path.Combine(_temp.Path, "package", inModelFolder ? "m" : string.Empty)).FullName;
        File.WriteAllBytes(Path.Combine(package, "pack.zip"), payload);

        Assert.Empty(await registry.ImportOfflineAsync(Path.Combine(_temp.Path, "package"), _ => false, Ct));
        Assert.False(registry.IsInstalled("m"));

        Assert.Equal(["m"], await registry.ImportOfflineAsync(Path.Combine(_temp.Path, "package"), _ => true, Ct));
        Assert.True(registry.IsInstalled("m"));
        Assert.Equal(0, server.Requests);
        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(package, "pack.zip")));
        Assert.True(await registry.VerifyAsync("m", Ct));
    }

    [Fact]
    public async Task A_tampered_offline_file_is_rejected()
    {
        var (registry, _, payload) = ArrangeWithPayload(requiresAcceptance: false, zip: false, Path.Combine(_temp.Path, "models"));
        var package = Directory.CreateDirectory(Path.Combine(_temp.Path, "package")).FullName;
        payload[0] ^= 0xFF;
        File.WriteAllBytes(Path.Combine(package, "model.onnx"), payload);

        await Assert.ThrowsAsync<InvalidDataException>(() => registry.ImportOfflineAsync(package, _ => true, Ct));

        Assert.False(registry.IsInstalled("m"));
    }

    private (ModelRegistry Registry, Server Server) Arrange(bool requiresAcceptance, bool zip)
    {
        var (registry, server, _) = ArrangeWithPayload(requiresAcceptance, zip, _temp.Path);
        return (registry, server);
    }

    private static (ModelRegistry Registry, Server Server, byte[] Payload) ArrangeWithPayload(bool requiresAcceptance, bool zip, string modelsDirectory)
    {
        byte[] payload;
        if (zip)
        {
            using var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in new[] { "pack/det.onnx", "pack/unused.onnx" })
                {
                    using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                    writer.Write(name);
                }
            }

            payload = buffer.ToArray();
        }
        else
        {
            payload = [1, 2, 3, 4];
        }

        var file = new ModelFile(zip ? "pack.zip" : "model.onnx", "https://example.invalid/m", Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
            payload.Length, zip ? "zip" : null, zip ? ["det.onnx"] : []);
        var model = new ModelInfo("m", ModelFunction.Faces, "Test", "1", 1, null, "test", new ModelLicense("X", "https://example.invalid", false, requiresAcceptance, null), [file]);
        var server = new Server(payload);
        return (new ModelRegistry(new HttpClient(server), [model], modelsDirectory), server, (byte[])payload.Clone());
    }

    public void Dispose() => _temp.Dispose();

    private sealed class Server(byte[] payload) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public bool Tamper { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var body = (byte[])payload.Clone();
            if (Tamper)
            {
                body[0] ^= 0xFF;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
}
