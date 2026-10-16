using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Tests;

/// In-memory SQLite database that lives as long as this object. Every context created from it
/// shares the same open connection, so a fresh context sees what another one saved.
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AgendaDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<AgendaDbContext>();
        Configure(builder);

        return new AgendaDbContext(builder.Options);
    }

    /// Points a context at this database, e.g. for AddDbContext in a test service collection.
    public void Configure(DbContextOptionsBuilder builder) => builder.UseSqlite(_connection);

    public User AddUser(string name, string? email = null)
    {
        using var context = CreateContext();
        var user = new User
        {
            GoogleId = "google-" + name,
            Email = email ?? name.ToLowerInvariant() + "@example.test",
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public CalendarShare AddShare(User owner, User sharedWith, SharePermission permission)
    {
        using var context = CreateContext();
        var share = new CalendarShare
        {
            OwnerUserId = owner.Id,
            SharedWithUserId = sharedWith.Id,
            Permission = permission,
            CreatedAt = DateTime.UtcNow
        };
        context.CalendarShares.Add(share);
        context.SaveChanges();
        return share;
    }

    public Event AddEvent(User owner, string title, DateTime start, Action<Event>? configure = null)
    {
        using var context = CreateContext();
        var evt = new Event
        {
            Title = title,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            UserId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        configure?.Invoke(evt);
        context.Events.Add(evt);
        context.SaveChanges();
        return evt;
    }

    public void Dispose() => _connection.Dispose();
}
