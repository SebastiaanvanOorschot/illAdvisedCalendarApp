using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MonthImagesController : ControllerBase
{
    private readonly MonthImageService _monthImageService;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public MonthImagesController(MonthImageService monthImageService)
    {
        _monthImageService = monthImageService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            throw new UnauthorizedAccessException("User ID not found in token");
        return userId;
    }

    [HttpGet("{month}")]
    public async Task<IActionResult> GetMonthImage(int month)
    {
        var userId = GetCurrentUserId();
        var result = await _monthImageService.GetMonthImageAsync(userId, month);

        if (result.Status == MonthImageServiceStatus.NotFound)
            return NoContent();

        return File(result.Value.Data, result.Value.ContentType);
    }

    [HttpPost]
    public async Task<IActionResult> UploadMonthImage(
        [FromForm] IFormFile file,
        [FromForm] int month)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (file.Length > MaxFileSize)
            return BadRequest($"File size exceeds maximum of {MaxFileSize / 1024 / 1024}MB");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest($"File type {extension} is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");

        if (month < 1 || month > 12)
            return BadRequest("Month must be between 1 and 12");

        var userId = GetCurrentUserId();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var imageData = ms.ToArray();

        await _monthImageService.SaveMonthImageAsync(userId, month, imageData, file.FileName, file.ContentType);

        return Ok(new { message = "Image uploaded successfully" });
    }

    [HttpDelete("{month}")]
    public async Task<IActionResult> DeleteMonthImage(int month)
    {
        var userId = GetCurrentUserId();
        var result = await _monthImageService.DeleteMonthImageAsync(userId, month);

        if (result.Status == MonthImageServiceStatus.NotFound)
            return NotFound();

        return Ok(new { message = "Image deleted successfully" });
    }
}
