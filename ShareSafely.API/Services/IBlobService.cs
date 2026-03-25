namespace ShareSafely.API.Services;

public interface IBlobService
{
    Task<string> UploadFileAsync(IFormFile file, CancellationToken ct = default);
    Task<string> GenerateSasLinkAsync(string blobName, int expiryHours);
    Task DeleteExpiredBlobsAsync(int olderThanDays = 7, CancellationToken ct = default);
}