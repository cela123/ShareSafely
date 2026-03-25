// BlobService.cs
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace ShareSafely.API.Services;

public class BlobService : IBlobService
{
    private readonly KeyVaultService _keyVault;
    private readonly string _containerName;
    private readonly ILogger<BlobService> _logger;
    private BlobServiceClient? _blobClient;

    public BlobService(
        KeyVaultService keyVault,
        IConfiguration config,
        ILogger<BlobService> logger)
    {
        _keyVault = keyVault;
        _containerName = config["Azure:BlobContainerName"] ?? "uploads";
        _logger = logger;
    }

    private async Task<BlobContainerClient> GetContainerAsync()
    {
        if (_blobClient is null)
        {
            var connStr = await _keyVault.GetSecretAsync("StorageConnectionString");
            _blobClient = new BlobServiceClient(connStr);
        }
        return _blobClient.GetBlobContainerClient(_containerName);
    }

    public async Task<string> UploadFileAsync(IFormFile file, CancellationToken ct = default)
    {
        var container = await GetContainerAsync();

        // Unique name prevents overwrites/collisions
        var blobName = $"{Guid.NewGuid()}-{Path.GetFileName(file.FileName)}";
        var blobClient = container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();

        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            },
            // Tag with original name for cleanup/audit
            Metadata = new Dictionary<string, string>
            {
                ["originalName"] = file.FileName,
                ["uploadedAt"] = DateTime.UtcNow.ToString("O")
            }
        }, ct);

        _logger.LogInformation("Uploaded blob: {BlobName}", blobName);
        return blobName;
    }

    public async Task<string> GenerateSasLinkAsync(string blobName, int expiryHours)
    {
        var connStr = await _keyVault.GetSecretAsync("StorageConnectionString");
        var container = await GetContainerAsync();

        // Parse account credentials from connection string
        var accountName = ParseValue(connStr, "AccountName");
        var accountKey = ParseValue(connStr, "AccountKey");
        var credential = new StorageSharedKeyCredential(accountName, accountKey);

        var expiresOn = DateTimeOffset.UtcNow.AddHours(expiryHours);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = expiresOn,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        var uri = container.GetBlobClient(blobName).Uri;

        return $"{uri}?{sasToken}";
    }

    public async Task DeleteExpiredBlobsAsync(int olderThanDays = 7, CancellationToken ct = default)
    {
        var container = await GetContainerAsync();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        var deleted = 0;

        await foreach (var blob in container.GetBlobsAsync(cancellationToken: ct))
        {
            if (blob.Properties.LastModified < cutoff)
            {
                await container.DeleteBlobAsync(blob.Name, cancellationToken: ct);
                deleted++;
                _logger.LogInformation("Cleaned up expired blob: {Name}", blob.Name);
            }
        }

        _logger.LogInformation("Cleanup complete. Deleted {Count} blobs.", deleted);
    }

    private static string ParseValue(string connStr, string key)
    {
        var match = connStr.Split(';')
            .FirstOrDefault(s => s.StartsWith(key + "="));
        return match?.Substring(key.Length + 1)
            ?? throw new InvalidOperationException($"Missing {key} in connection string");
    }
}