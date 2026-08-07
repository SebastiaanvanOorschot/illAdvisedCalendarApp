using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgendaApi.Services;

public class MonthImageService
{
    private readonly AgendaDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MonthImageService> _logger;

    public MonthImageService(
        AgendaDbContext context,
        IMemoryCache cache,
        ILogger<MonthImageService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    private static string CacheKey(int userId, int month) => $"img:{userId}:{month}";

    public async Task<MonthImageServiceResult<(byte[] Data, string ContentType)>> GetMonthImageAsync(int userId, int month)
    {
        var key = CacheKey(userId, month);

        if (!_cache.TryGetValue(key, out (byte[] Data, string ContentType) cached))
        {
            var monthImage = await _context.MonthImages
                .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

            if (monthImage?.ImageData == null)
                return MonthImageServiceResult<(byte[], string)>.NotFoundResult();

            cached = (monthImage.ImageData, monthImage.ContentType);
            _cache.Set(key, cached, TimeSpan.FromHours(24));
        }

        return MonthImageServiceResult<(byte[], string)>.Ok(cached);
    }

    public async Task<MonthImageServiceResult> SaveMonthImageAsync(int userId, int month, byte[] imageData, string fileName, string contentType)
    {
        var existingImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        if (existingImage != null)
        {
            existingImage.FileName = fileName;
            existingImage.ContentType = contentType;
            existingImage.UploadedAt = DateTime.UtcNow;
            existingImage.ImageData = imageData;
        }
        else
        {
            _context.MonthImages.Add(new MonthImage
            {
                UserId = userId,
                Month = month,
                FileName = fileName,
                ContentType = contentType,
                UploadedAt = DateTime.UtcNow,
                ImageData = imageData
            });
        }

        await _context.SaveChangesAsync();
        _cache.Set(CacheKey(userId, month), (imageData, contentType), TimeSpan.FromHours(24));

        return MonthImageServiceResult.Ok();
    }

    public async Task<MonthImageServiceResult> DeleteMonthImageAsync(int userId, int month)
    {
        var monthImage = await _context.MonthImages
            .FirstOrDefaultAsync(mi => mi.UserId == userId && mi.Month == month);

        if (monthImage == null)
            return MonthImageServiceResult.NotFoundResult();

        _context.MonthImages.Remove(monthImage);
        await _context.SaveChangesAsync();
        _cache.Remove(CacheKey(userId, month));

        return MonthImageServiceResult.Ok();
    }
}

public enum MonthImageServiceStatus
{
    Success,
    NotFound
}

/// <summary>
/// Non-generic result for service operations that don't return data (upload/delete).
/// </summary>
public class MonthImageServiceResult
{
    public MonthImageServiceStatus Status { get; init; }

    public static MonthImageServiceResult Ok() => new() { Status = MonthImageServiceStatus.Success };
    public static MonthImageServiceResult NotFoundResult() => new() { Status = MonthImageServiceStatus.NotFound };
}

/// <summary>
/// Generic result for service operations that return data on success (e.g. get).
/// </summary>
public class MonthImageServiceResult<T>
{
    public MonthImageServiceStatus Status { get; init; }
    public T? Value { get; init; }

    public static MonthImageServiceResult<T> Ok(T value) => new() { Status = MonthImageServiceStatus.Success, Value = value };
    public static MonthImageServiceResult<T> NotFoundResult() => new() { Status = MonthImageServiceStatus.NotFound };
}
