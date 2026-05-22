using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Storage;

public class FileSystemUploadBlobStore(
    IOptions<UploadStorageOptions> options,
    ILogger<FileSystemUploadBlobStore> logger) : IUploadBlobStore
{
    private readonly string _root = ResolveRoot(options.Value.Path);

    private static string ResolveRoot(string configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taxreader-uploads")
            : configured;

    private string PathFor(Guid id) => System.IO.Path.Combine(_root, id.ToString("N") + ".bin");

    public async Task SaveAsync(Guid receiptFileId, Stream content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        await using var file = File.Create(PathFor(receiptFileId));
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(Guid receiptFileId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(receiptFileId);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public Task DeleteAsync(Guid receiptFileId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(receiptFileId);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException ex) { logger.LogWarning(ex, "Could not delete upload blob {ReceiptFileId}", receiptFileId); }
        return Task.CompletedTask;
    }
}
