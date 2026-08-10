# Architecture

## Overview

Agenda is a personal calendar application: recurring events, shared calendars with
per-user permissions, iCal subscriptions, Google Calendar sync, weather, and custom
month images.

It's a monorepo with two independently deployed services:

| Service | Path | URL |
|---|---|---|
| ASP.NET Core API | `AgendaApi/` | https://calendar-api.sebaslive.xyz |
| Vue 3 frontend | `AgendaFrontend/` | https://calendar.sebaslive.xyz |

Both build from their own Dockerfile and deploy independently on push to `master`.

## Stack

**Backend** (`AgendaApi/AgendaApi/AgendaApi.csproj`):
- ASP.NET Core 8 (`net8.0`)
- Entity Framework Core + Npgsql (PostgreSQL)
- Ical.Net — RRULE parsing fallback for complex recurrence
- JWT bearer auth (`System.IdentityModel.Tokens.Jwt`) + Google OAuth (`Microsoft.AspNetCore.Authentication.Google`)
- `Google.Apis.Calendar.v3` — Google Calendar API client
- Swashbuckle (Swagger/OpenAPI)

**Frontend** (`AgendaFrontend/package.json`):
- Vue 3 + TypeScript, built with Vite
- Vue Router, Axios (via `vue-axios`)
- `@vuelidate/core` for form validation
- `vue3-google-login` for Google sign-in
- `dayjs` for date handling
- NSwag (`nswag`, dev dependency) generates the typed API client from the backend's OpenAPI spec

Note: no Bootstrap/Bootstrap Vue Next dependency is present — styling is hand-written
CSS with custom properties (see [Frontend structure](#frontend-structure)).

## Backend: layered architecture

The API follows a strict three-layer structure:

```
Controllers/   HTTP concerns only: routing, [Authorize], extracting the user ID from
               the JWT claims, translating a service result into an HTTP response.
Services/      All business logic and all EF Core (_context) access. One service class
               per feature area (EventService, CalendarShareService, AuthService, ...).
DTOs/          The API's public contract: plain classes, "...Dto" suffix, one file per
               shape, living in DTOs/.
Mapping/       Static mapper classes (EventMapper, CalendarSubscriptionMapper) that
               convert between EF Core entities (Models/) and DTOs by hand.
```

No controller accesses `_context` directly — verified by grepping `Controllers/` for
`_context.` (zero matches). Every controller depends only on a service class.

### Why these choices

- **Services are concrete classes, not interfaces**, registered directly with
  `AddScoped<EventService>()` etc. in `Program.cs` (the one exception is
  `IRecurrenceService` / `IGoogleCalendarService`, which are interfaces — used where
  a second implementation or test substitution is plausible). Most services have
  exactly one real implementation and aren't mocked in tests, so an interface would
  be pure ceremony.
- **DTOs are plain classes, not records, with a `Dto` suffix**, kept in `DTOs/`
  separate from `Models/` (the EF Core entities). This keeps the wire contract
  decoupled from the database schema — a column rename or new EF navigation property
  doesn't silently change what the API returns.
- **Mapping is manual, not AutoMapper.** Each mapper is a static class with explicit
  field-by-field assignment (e.g. `EventMapper.ToDto`, `EventMapper.ToWithOwnerDto`).
  This trades boilerplate for explicit control — a new field on `Event` doesn't
  silently leak into a DTO (or silently fail to map) via reflection-based conventions.
- **Services return typed result objects, not exceptions, for expected failure
  cases.** The pattern repeats across `EventService`, `CalendarSubscriptionService`,
  `MonthImageService`, `UserPreferencesService`, `AuthService`, and
  `GoogleCalendarConnectionService`: each defines its own `...ServiceStatus` enum
  (`Success`, `NotFound`, `Forbidden`, `BadRequest`, ...) plus a `...ServiceResult`
  (and generic `...ServiceResult<T>`) wrapper with static factory methods
  (`Ok()`, `NotFoundResult()`, `ForbiddenResult(message)`, ...). Controllers switch
  on `result.Status` to pick the HTTP response:

  ```csharp
  return result.Status switch
  {
      EventServiceStatus.Success   => NoContent(),
      EventServiceStatus.NotFound  => NotFound(),
      EventServiceStatus.Forbidden => Forbid(result.ErrorMessage ?? string.Empty),
      EventServiceStatus.BadRequest => BadRequest(result.ErrorMessage),
      _ => BadRequest(result.ErrorMessage)
  };
  ```

  This keeps HTTP status-code decisions in the controller (where they belong) while
  keeping the service layer free of any ASP.NET Core dependency.

## Schema management

There's no EF Core migrations tool in use. `Program.cs` calls
`db.Database.EnsureCreated()` on startup, which creates the full schema from the
current `Models/` on a fresh database (no-op if tables already exist). Schema changes
made *after* the first deploy are applied via idempotent `ExecuteSqlRaw` statements
(`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT
EXISTS`) run immediately after `EnsureCreated()`. Current examples include adding the
`CalendarSubscriptions` table, performance indexes on `Events`, an `ImageData BYTEA`
column on `MonthImages` (so uploaded images survive Railway's ephemeral filesystem
across redeploys), and relaxing `Events.EndDateTime` to nullable.

## Recurrence

`RecurrenceService` (`Services/RecurrenceService.cs`) avoids calling
`new RecurrencePattern(rruleString)` (Ical.Net) for common cases, because that
constructor costs roughly 150ms on Railway's shared CPU — expensive when it runs once
per recurring event per occurrences request.

Instead, `ParseRRule` hand-parses the RRULE string into a `SimpleRRule` (freq,
interval, count, until, byday, and an `IsComplex` flag) and, when the pattern is one
of `DAILY` / `WEEKLY` / `MONTHLY` / `YEARLY` with no `COUNT` and no unsupported
modifiers, expands occurrences with pure-C# date arithmetic
(`ExpandDaily`/`ExpandWeekly`/`ExpandMonthly`/`ExpandYearly`). `WEEKLY` also supports
`BYDAY` (e.g. `FREQ=WEEKLY;BYDAY=MO,WE`) on the fast path.

Anything the fast parser flags as complex — `COUNT`, `BYSETPOS`, `BYWEEKNO`,
`BYHOUR`/`BYMINUTE`/`BYSECOND`, `BYYEARDAY`, ordinal `BYDAY` (e.g. `1MO`, `-1FR`),
multi-value or non-anchor-matching `BYMONTH`/`BYMONTHDAY` — falls back to
`GetIcalNetOccurrences`, which does use Ical.Net's `RecurrencePattern`. A `FastCount`
/ `SlowCount` pair on the service tracks how often each path is taken (reset via
`ResetCounters()`), useful for local diagnostics.

Deleted individual occurrences of a recurring event are tracked as **exception
dates**: `Event.ExceptionDates` is a comma-separated string of dates, parsed by
`ParseExceptionDates` with `DateTimeStyles.RoundtripKind` (so `"...Z"`-suffixed UTC
timestamps round-trip as UTC rather than being silently reinterpreted as local time).
Both the fast-path expanders and the Ical.Net fallback filter generated occurrences
against this exception set before returning them.

Legacy events using the older `IsRecurring` + `RecurrencePattern` (string:
daily/weekly/monthly/yearly) + `RecurrenceInterval` fields — predating the RRULE
column — are still supported via `GetSimpleRecurrenceOccurrences`, which the RRULE
path also falls back to if parsing throws.

## Caching

Two independent `IMemoryCache` regions, both registered via `builder.Services.AddMemoryCache()`:

**Occurrences** (`EventService`): key is
`occ:{userId}:{buster}:{start:yyyyMMddHHmm}:{end:yyyyMMddHHmm}`, TTL 10 minutes. The
`{buster}` is a per-user `long` (`DateTime.UtcNow.Ticks`) stored under
`occ_buster:{userId}` with `CacheItemPriority.NeverRemove`. Any event mutation calls
`BustOccurrencesCache(userId)`, which overwrites that buster value — every
previously-issued cache key for that user becomes unreachable immediately, without
needing to enumerate or explicitly evict them. Stale entries simply age out via TTL.

**Month images** (`MonthImageService`): key is `img:{userId}:{month}`, TTL 24 hours.
Populated lazily on first read and refreshed on save; explicitly removed via
`_cache.Remove` on delete.

## Auth

JWT access tokens are issued by `JwtService`; `AuthService` handles login and refresh.

- **Login** is Google OAuth only (`AuthService.GoogleLoginAsync`): the frontend
  obtains a Google ID token (`vue3-google-login`), the backend validates it via
  `GoogleAuthService`, checks the token's `aud` claim against the configured
  `Google:ClientId`, and finds-or-creates a `User` by `GoogleId`.
- **Refresh tokens** are rotated, not reused: `AuthService.RefreshAsync` looks up the
  presented token, and if valid (`refreshToken.IsActive`), marks it
  `IsRevoked = true` / stamps `RevokedAt`, then issues and persists a brand-new
  `RefreshToken` row (30-day expiry) alongside a new access token — both writes
  happen in the same `SaveChangesAsync()` call. `LogoutAsync` revokes a token the
  same way, without issuing a replacement.
- Failure cases (invalid Google token, wrong audience, invalid/expired/revoked
  refresh token) return `AuthServiceResult<T>.UnauthorizedResult(...)`, which
  `AuthController` turns into a 401 — following the same service-result pattern as
  the rest of the backend.

## Other integrations

- **iCal subscriptions**: `ICalSyncService` + `CalendarSubscriptionService` support
  read-only external calendars. Sync is triggered manually through the API
  (`POST` to the subscription's sync endpoint → `SyncSubscriptionAsync`); each
  `CalendarSubscription` row carries a `SyncIntervalMinutes` field but there is no
  active scheduler driving automatic sync — a `CalendarSyncBackgroundService` class
  exists in `Services/` but is not registered in `Program.cs`, so it does not run.
- **Google Calendar sync**: `GoogleCalendarService` + `GoogleCalendarConnectionService`,
  exposed through `GoogleCalendarController`, let a user connect their Google account
  and import events.
- **Weather**: `WeatherService`, backed by a registered `HttpClient`
  (`AddHttpClient<WeatherService>()`), exposed through `WeatherController`.

## Frontend structure

`AgendaFrontend/src/`:

```
api/            Axios client generated by NSwag (agenda-api-swagger.ts, from
                AgendaApi.nswag) plus axios-config.ts / base.ts for auth headers
                and base URL wiring.
components/     Vue components, grouped by feature (calendar/, navigation/, weather/).
composables/    Shared reactive logic — useAuth, useEventOperations, useWeather,
                useBackButton.
views/          Route-level pages (LoginView, ProfileView, CalendarSettingsView,
                LocalizationView, index.vue).
router/         Vue Router config.
types/          Shared TypeScript types (calendar.ts).
constants/      Static lookup data (eventColors.ts).
utils/          Small helpers (dateFormat.ts).
css/            Style.css — hand-written styles using CSS custom properties as
                design tokens (--color-primary, --color-accent, --color-danger,
                --color-text-muted, etc.) defined once on :root.
images/         Static image assets.
```

**PWA**: the app is installable, backed by `public/sw.js`. The service worker only
intercepts same-origin `GET` requests and explicitly ignores `/api/*` (API calls
always hit the network). For everything else it applies a stale-while-revalidate
strategy: serve the cached response immediately if present while a fresh fetch
updates the cache in the background.
