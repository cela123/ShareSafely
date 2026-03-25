namespace ShareSafely.API.Models;

public class UploadRequest
{
    public IFormFile File { get; init; } = null!;
    public int ExpiryHours { get; init; } = 24;
}
