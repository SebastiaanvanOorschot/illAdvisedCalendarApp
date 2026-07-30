# Agenda Calendar App

A personal calendar application with recurring events, shared calendars, weather integration, iCal subscriptions, Google Calendar sync, and custom month images.

## Features

- JWT authentication with refresh tokens, plus Google OAuth login
- Recurring events via RRULE (RFC 5545)
- Shared calendars with per-user permissions (read / read-write)
- iCal subscriptions (read-only external calendars)
- Google Calendar sync
- Weather integration
- Installable as a PWA
- Custom month images

## Tech Stack

| | Backend | Frontend |
|---|---|---|
| Framework | ASP.NET Core 8 | Vue 3 + TypeScript |
| Build tool | — | Vite |
| Data | Entity Framework Core, PostgreSQL (Npgsql) | — |
| Other | Ical.Net, JWT auth | Bootstrap Vue Next, Axios |

## Architecture

Monorepo with two independently deployed services:

| Service | Path | URL |
|---|---|---|
| ASP.NET Core API | `AgendaApi/` | https://calendar-api.sebaslive.xyz |
| Vue 3 frontend | `AgendaFrontend/` | https://calendar.sebaslive.xyz |

Schema management uses `EnsureCreated()` plus manual `ExecuteSqlRaw` calls in `Program.cs` — there's no migrations tool. See [CLAUDE.md](CLAUDE.md) for deeper implementation notes (recurrence performance, caching, etc.).

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js + [Yarn](https://yarnpkg.com/)
- PostgreSQL
- A Google OAuth Client ID (see [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md))

### Backend setup

```bash
cd AgendaApi/AgendaApi
cp appsettings.Development.example.json appsettings.Development.json
```

Fill in `ConnectionStrings:DefaultConnection` with your PostgreSQL connection details (and the other placeholder values). Alternatively, keep secrets out of the file entirely with `dotnet user-secrets`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=...;Username=...;Password=..."
```

Run it:

```bash
dotnet run
```

### Frontend setup

```bash
cd AgendaFrontend
cp .env.example .env.development
```

Fill in `VITE_API_URL` and `VITE_GOOGLE_CLIENT_ID`. Then:

```bash
yarn install
yarn dev
```

For the full Google OAuth setup (creating the client ID, configuring redirect URIs), see [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md).

### Building

```bash
yarn build     # frontend — runs type-check (vue-tsc) then vite build
dotnet build   # backend
```

## Project Structure

```
AgendaApi/AgendaApi/       ASP.NET Core API
  Controllers/               API endpoints
  Models/                    EF Core entities
  Services/                  Business logic (e.g. RecurrenceService)
  Data/                      DbContext
  Migrations/                EF Core migrations

AgendaFrontend/            Vue 3 + TypeScript frontend
  src/api/                   Axios API clients
  src/components/            Vue components
  src/composables/           Vue composables (shared reactive logic)
  src/views/                 Route-level views
  src/router/                Vue Router config
  src/types/                 Shared TypeScript types
```

## Documentation

- [CLAUDE.md](CLAUDE.md) — architecture and implementation notes
- [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md) — Google OAuth setup
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) — deployment notes
