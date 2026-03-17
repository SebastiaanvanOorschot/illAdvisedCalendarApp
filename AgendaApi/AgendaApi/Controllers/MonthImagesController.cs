using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MonthImagesController : ControllerBase
{
    private readonly AgendaDbContext _context;
    private readonly ILogger<MonthImagesController> _logger;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public MonthImagesController(
        AgendaDbContext context,
        ILogger<MonthImagesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    [HttpGet("{month}")]
    public async Task<IActionResult> GetMonthImage(int month)
    {
        var userId = GetCurrentUserId();

        var monthImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        if (monthImage?.ImageData == null)
        {
            return NotFound();
        }

        return File(monthImage.ImageData, monthImage.ContentType);
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

        var existingImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        if (existingImage != null)
        {
            existingImage.FileName = file.FileName;
            existingImage.ContentType = file.ContentType;
            existingImage.UploadedAt = DateTime.UtcNow;
            existingImage.ImageData = imageData;
        }
        else
        {
            _context.MonthImages.Add(new MonthImage
            {
                UserId = userId,
                Month = month,
                FileName = file.FileName,
                ContentType = file.ContentType,
                UploadedAt = DateTime.UtcNow,
                ImageData = imageData
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Image uploaded successfully" });
    }

    [HttpDelete("{month}")]
    public async Task<IActionResult> DeleteMonthImage(int month)
    {
        var userId = GetCurrentUserId();

        var monthImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        if (monthImage == null)
            return NotFound();

        _context.MonthImages.Remove(monthImage);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Image deleted successfully" });
    }
}
