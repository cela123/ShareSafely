using ShareSafely.API.Models;
using ShareSafely.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ShareSafely.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IBlobService _blobService;
    private readonly IConfiguration _config;
    private readonly ILogger<FileController> _logger;

    public FileController(
        IBlobService blobService,
        IConfiguration config,
        ILogger<FileController> logger)
    {
        _blobService = blobService;
        _config = config;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(104_857_600)] // 100 MB
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] UploadRequest request,
        CancellationToken ct = default)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new ErrorResponse()
            {
                Error = "No file provided"
            });

        var maxMb = _config.GetValue<int>("FileUpload:MaxFileSizeMB", 100);
        if (request.File.Length > maxMb * 1024 * 1024)
            return BadRequest(new ErrorResponse()
            {
                Error = $"File exceeds {maxMb}MB limit"
            });

        var expiryHours = Math.Clamp(request.ExpiryHours == 0 ? 24 : request.ExpiryHours, 1, 168);

        try
        {
            var blobName = await _blobService.UploadFileAsync(request.File, ct);
            var shareLink = await _blobService.GenerateSasLinkAsync(blobName, expiryHours);
            var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

            return Ok(new FileUploadResponse()
            {
                Success = true,
                FileName = request.File.FileName,
                ShareLink = shareLink,
                ExpiresIn = $"{expiryHours} hour{(expiryHours != 1 ? "s" : "")}",
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for {FileName}", request.File.FileName);

            return StatusCode(500, new ErrorResponse()
            {
                Error = "Upload failed",
                Message = ex.Message
            });
        }
    }

    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] int olderThanDays = 7)
    {
        await _blobService.DeleteExpiredBlobsAsync(olderThanDays);
        return Ok(new { message = "Cleanup complete" });
    }
}