using AgendaApi.Data;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Database -- PostgreSQL via Npgsql
builder.Services.AddDbContext<AgendaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient for services
builder.Services.AddHttpClient<WeatherService>();
builder.Services.AddHttpClient<GoogleAuthService>();

// Register authentication services
builder.Services.AddScoped<JwtService>();

// Register recurrence service
builder.Services.AddScoped<IRecurrenceService, RecurrenceService>();

// Register Google Calendar service
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

// Register Calendar Share service
builder.Services.AddScoped<CalendarShareService>();

// Configure JWT authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
var jwtIssuer    = builder.Configuration["Jwt:Issuer"];
var jwtAudience  = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey!)),
        ValidateIssuer           = true,
        ValidIssuer              = jwtIssuer,
        ValidateAudience         = true,
        ValidAudience            = jwtAudience,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero,
        NameClaimType            = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
    };
});

// CORS -- read allowed origins from config; fall back to localhost + production domain
var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:5173", "http://localhost:5174", "http://localhost:3000",
               "https://calendar.sebaslive.xyz" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB schema exists (no-op if tables already exist; creates schema on fresh deploy)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgendaDbContext>();
    db.Database.EnsureCreated();

    // CalendarSubscriptions was added after the first deploy so EnsureCreated skipped it.
    // Create it now if missing.  All statements use IF NOT EXISTS — safe to re-run.
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""CalendarSubscriptions"" (
            ""Id""                  SERIAL        PRIMARY KEY,
            ""Name""                VARCHAR(255)  NOT NULL,
            ""ICalUrl""             VARCHAR(2000) NOT NULL,
            ""Color""               VARCHAR(7),
            ""SyncIntervalMinutes"" INT           NOT NULL DEFAULT 60,
            ""IsActive""            BOOLEAN       NOT NULL DEFAULT TRUE,
            ""LastSyncedAt""        TIMESTAMP,
            ""LastSyncError""       TEXT,
            ""CreatedAt""           TIMESTAMP     NOT NULL,
            ""UpdatedAt""           TIMESTAMP     NOT NULL,
            ""UserId""              INT           NOT NULL,
            CONSTRAINT ""FK_CalendarSubscriptions_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE
        )");

    // Add performance indexes that were missing from the initial schema
    db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_CalendarSubscriptions_UserId"" ON ""CalendarSubscriptions"" (""UserId"")");
    db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Events_UserId"" ON ""Events"" (""UserId"")");
    db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_Events_UserId_StartDateTime"" ON ""Events"" (""UserId"", ""StartDateTime"")");

    // Store image bytes in DB so images survive redeployments (ephemeral Railway filesystem)
    db.Database.ExecuteSqlRaw(@"ALTER TABLE ""MonthImages"" ADD COLUMN IF NOT EXISTS ""ImageData"" BYTEA");
}

// Pre-warm Ical.Net: force JIT compilation and static timezone/rule initialisation
// at startup so the very first user request doesn't pay this cost.
try
{
    var warmup = new Ical.Net.CalendarComponents.CalendarEvent
    {
        DtStart = new Ical.Net.DataTypes.CalDateTime(DateTime.Today),
        DtEnd   = new Ical.Net.DataTypes.CalDateTime(DateTime.Today.AddHours(1)),
        RecurrenceRules = new List<Ical.Net.DataTypes.RecurrencePattern>
        {
            new Ical.Net.DataTypes.RecurrencePattern("FREQ=WEEKLY")
        }
    };
    warmup.GetOccurrences(new Ical.Net.DataTypes.CalDateTime(DateTime.Today))
          .Take(5)
          .ToList();
}
catch { /* non-fatal — worst case first request pays the init cost */ }

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

if (app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
