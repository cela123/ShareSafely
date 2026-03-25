namespace ShareSafely.API.Models;
public class FileUploadResponse
{
    public bool Success { get; set; }
    public string FileName { get; set; }
    public string ShareLink { get; set; }
    public string ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
}
