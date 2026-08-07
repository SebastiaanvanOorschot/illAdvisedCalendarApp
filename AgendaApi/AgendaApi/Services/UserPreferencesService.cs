using AgendaApi.Data;
using AgendaApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class UserPreferencesService
{
    private readonly AgendaDbContext _context;

    public UserPreferencesService(AgendaDbContext context)
    {
        _context = context;
    }

    public async Task<UserPreferencesServiceResult<UserPreferencesDto>> GetPreferencesAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return UserPreferencesServiceResult<UserPreferencesDto>.NotFoundResult();

        return UserPreferencesServiceResult<UserPreferencesDto>.Ok(new UserPreferencesDto
        {
            ShowEventTitleInMonthView = user.ShowEventTitleInMonthView
        });
    }

    public async Task<UserPreferencesServiceResult<UserPreferencesDto>> UpdatePreferencesAsync(int userId, UpdateUserPreferencesRequest request)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return UserPreferencesServiceResult<UserPreferencesDto>.NotFoundResult();

        if (request.ShowEventTitleInMonthView.HasValue)
        {
            user.ShowEventTitleInMonthView = request.ShowEventTitleInMonthView.Value;
        }

        await _context.SaveChangesAsync();

        return UserPreferencesServiceResult<UserPreferencesDto>.Ok(new UserPreferencesDto
        {
            ShowEventTitleInMonthView = user.ShowEventTitleInMonthView
        });
    }
}

public enum UserPreferencesServiceStatus
{
    Success,
    NotFound
}

/// <summary>
/// Generic result for service operations that return data on success (get/update).
/// </summary>
public class UserPreferencesServiceResult<T>
{
    public UserPreferencesServiceStatus Status { get; init; }
    public T? Value { get; init; }

    public static UserPreferencesServiceResult<T> Ok(T value) => new() { Status = UserPreferencesServiceStatus.Success, Value = value };
    public static UserPreferencesServiceResult<T> NotFoundResult() => new() { Status = UserPreferencesServiceStatus.NotFound };
}
