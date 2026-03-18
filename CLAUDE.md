# Agenda Calendar App

Personal calendar application with recurring events, shared calendars, weather, and custom month images.

## Architecture
Monorepo with two independently deployed services:

| Service | Path | URL |
|---|---|---|
| ASP.NET Core API | `AgendaApi/` | https://calendar-api.sebaslive.xyz |
| Vue 3 frontend | `AgendaFrontend/` | https://calendar.sebaslive.xyz |

## Stack
- **API:** ASP.NET Core 8, Entity Framework Core, PostgreSQL (Npgsql), Ical.Net, JWT auth
- **Frontend:** Vue 3 + TypeScript + Vite, Bootstrap Vue Next, Axios

## Deployment
- **Platform:** Railway
- **Database:** PostgreSQL hosted on Railway (`postgres.railway.internal` internally, `shinkansen.proxy.rlwy.net` externally)
- **Trigger:** push to `master` → Railway auto-builds both services from their Dockerfiles
- **No migrations tool** — schema managed via `EnsureCreated()` + manual `ExecuteSqlRaw` in `Program.cs` for tables EF won't auto-create

## Key implementation notes

### Recurrence performance
`new RecurrencePattern(rruleString)` in Ical.Net costs ~150ms per call on Railway's shared CPU. The app bypasses this with a custom lightweight parser (`ParseRRule` in `RecurrenceService.cs`) that handles DAILY/WEEKLY/MONTHLY/YEARLY without touching Ical.Net. Complex patterns (COUNT, BYSETPOS, ordinal BYDAY) fall back to Ical.Net.

### Caching
- **Occurrences:** `IMemoryCache` keyed by `occ:{userId}:{cacheBuster}:{start}:{end}`, TTL 10 min. Busted on any event mutation.
- **Month images:** `IMemoryCache` keyed by `img:{userId}:{month}`, TTL 24h. Also cached as blob URLs in a frontend `Map<month, blobUrl>`.
- **Shared owner permissions:** Pre-fetched once per unique owner before the event loop (avoids N+1 DB calls).

### X-Timing header
Every `/api/Events/occurrences` response includes `X-Timing: shares=Xms db=Xms recurrence=Xms fast=N slow=N events=N total=Xms` for diagnostics.

### PWA
Frontend is installable as a PWA. Service worker at `public/sw.js` (stale-while-revalidate for static assets, passthrough for `/api/`).

## Local dev
```bash
# API
cd AgendaApi/AgendaApi
dotnet run

# Frontend
cd AgendaFrontend
npm install
npm run dev
```

Frontend expects API at the URL configured in `.env` / `authenticatedAxios` base URL.
