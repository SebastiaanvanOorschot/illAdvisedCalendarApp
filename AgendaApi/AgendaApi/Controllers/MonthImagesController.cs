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
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MonthImagesController> _logger;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public MonthImagesController(
        AgendaDbContext context,
        IWebHostEnvironment environment,
        ILogger<MonthImagesController> logger)
    {
        _context = context;
        _environment = environment;
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

        if (monthImage == null)
        {
            return NotFound();
        }

        var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "month-images");
        var filePath = Path.Combine(uploadsPath, monthImage.FileName);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning($"Image file not found: {filePath}");
            return NotFound();
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, monthImage.ContentType);
    }

    [HttpPost]
    public async Task<IActionResult> UploadMonthImage(
        [FromForm] IFormFile file,
        [FromForm] int month)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest($"File size exceeds maximum of {MaxFileSize / 1024 / 1024}MB");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest($"File type {extension} is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
        }

        if (month < 1 || month > 12)
        {
            return BadRequest("Month must be between 1 and 12");
        }

        var userId = GetCurrentUserId();

        // Check if image already exists for this month
        var existingImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        // Create uploads directory if it doesn't exist
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "month-images");
        Directory.CreateDirectory(uploadsPath);

        // Generate unique filename
        var fileName = $"{userId}_{month}_{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        if (existingImage != null)
        {
            // Delete old file
            var oldFilePath = Path.Combine(uploadsPath, existingImage.FileName);
            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }

            // Update existing record
            existingImage.FileName = fileName;
            existingImage.ContentType = file.ContentType;
            existingImage.UploadedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new record
            var monthImage = new MonthImage
            {
                UserId = userId,
                Month = month,
                FileName = fileName,
                ContentType = file.ContentType,
                UploadedAt = DateTime.UtcNow
            };

            _context.MonthImages.Add(monthImage);
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
        {
            return NotFound();
        }

        // Delete file
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "month-images");
        var filePath = Path.Combine(uploadsPath, monthImage.FileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        // Delete record
        _context.MonthImages.Remove(monthImage);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Image deleted successfully" });
    }
}
