# IHub Intermediary Activity Statement

A small ASP.NET Core API for ingesting intermediary activities and calculating gross statements over an occurrence-time window. The implementation uses PostgreSQL and EF Core for the application, with HTTP integration tests backed by EF Core InMemory.

## Architecture

```text
src/
  IHub.Domain/       Entities and domain exceptions
  IHub.Application/  DTOs, service/repository/Unit of Work contracts
  IHub.Services/     Activity and statement service implementation
  IHub.Data/         EF Core context, PostgreSQL repositories and migrations
  IHub.Api/          HTTP controllers, error handling, health and OpenTelemetry instrumentation

tests/
  IHub.IntegrationTests/  WebApplicationFactory HTTP tests
```

`IHub.Services` contains business use-case logic and implements the Application service interface. `IHub.Data` is the persistence project, following the `Data` layer naming used by the TopScorers reference. There is no separate Infrastructure project. The API controllers delegate to the Services layer and map results to HTTP; only the Data project uses EF Core.

## Prerequisites

- .NET 8 SDK (LTS)
- PostgreSQL 14 or newer for running the API
- `dotnet-ef` 9.x for migration commands

The automated integration tests do not require PostgreSQL or Docker.

## PostgreSQL on macOS

### Postgres.app

1. Install and open [Postgres.app](https://postgresapp.com/).
2. Initialize a server if prompted, then start it. The default local port is `5432`.
3. Create the development database from a terminal with `psql` on your PATH:

```sh
psql -d postgres -c 'CREATE DATABASE ihub;'
```

If the database already exists, skip that command. Postgres.app also includes `psql`; add its command-line tools to your PATH if needed.

### Homebrew alternative

Install and start a local PostgreSQL service using your preferred version from Homebrew, then create the `ihub` database with `createdb ihub` or `psql`.

### Connection string

The committed default is a local, credential-free connection string:

```text
Host=localhost;Port=5432;Database=ihub
```

If your local PostgreSQL setup requires a username or password, supply the connection string through environment configuration instead of committing credentials:

```sh
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=ihub;Username=YOUR_LOCAL_USER;Password=YOUR_LOCAL_PASSWORD'
```

For shared or production environments, use a secret manager and not shell history or committed settings files.

## Reset the Local Database and Run

The repository contains one fresh `InitialCreate` migration for the current model. To discard an existing local `ihub` database and start fresh, stop the API first, then run:

```sh
dropdb --if-exists ihub
createdb ihub
dotnet ef database update \
  --project src/IHub.Data/IHub.Data.csproj \
  --startup-project src/IHub.Api/IHub.Api.csproj
```

If `dotnet-ef` is not installed, install it with `dotnet tool install --global dotnet-ef --version 9.0.0`; if an older version is installed, update it with `dotnet tool update --global dotnet-ef --version 9.0.0`. The API also applies pending migrations at startup. From the repository root:

```sh
dotnet run --project src/IHub.Api/IHub.Api.csproj
```

The HTTP launch profile listens on `http://localhost:5204`. If the database cannot be reached, ensure PostgreSQL is running before launching the API. Dropping the database deletes all its data; use this reset only for the disposable local database, never shared or production data.

## API

There are exactly two business endpoints.

### Ingest an activity

`POST /activities`

```sh
curl -i http://localhost:5204/activities \
  -H 'Content-Type: application/json' \
  -d '{
    "sourceSystem": "PolicyAdministration",
    "externalActivityId": "ACT-10001",
    "intermediaryId": "BROKER-123",
    "occurredAt": "2026-10-15T10:30:00Z",
    "amount": 1250.50
  }'
```

`sourceSystem` is trimmed and canonicalized to uppercase. It must be at most 50 characters and contain letters, digits, dots, underscores or hyphens. `externalActivityId` is preserved, including case, and is limited to 200 characters. Currency is not accepted in the request; the application assigns `ZAR`. Amount must be positive and fit `NUMERIC(18,2)`.

Responses:

- `201 Created`: new activity persisted.
- `200 OK`: identical submission for the same `(sourceSystem, externalActivityId)`.
- `409 Conflict`: conflicting payload for that identity.
- `400 Bad Request`: invalid activity.

The response contains the canonical `sourceSystem`, external ID, intermediary, occurrence timestamp, amount, `ZAR` currency and ingestion timestamp.

### Retrieve a statement

`GET /intermediaries/{intermediaryId}/statement?start=...&end=...`

```sh
curl -i 'http://localhost:5204/intermediaries/BROKER-123/statement?start=2026-10-01T00:00:00Z&end=2026-11-01T00:00:00Z'
```

Example response:

```json
{
  "intermediaryId": "BROKER-123",
  "currency": "ZAR",
  "start": "2026-10-01T00:00:00+00:00",
  "end": "2026-11-01T00:00:00+00:00",
  "grossTotal": 1250.50,
  "lastUpdated": "2026-10-30T12:00:00+00:00"
}
```

The requested window is half-open: start inclusive, end exclusive. Only `OccurredAt` is used for inclusion, so a late-arriving activity is included in its historical statement. `LastUpdated` is the latest `IngestedAt` among matching activities, or `null` when the window is empty. Empty totals are `0`. Currency is always reported as `ZAR`; no currency selection or conversion is performed.

## Operational Endpoints

- `GET /health`: combined process/database health. Returns `200` with `healthy` when PostgreSQL is reachable and `503` with `unhealthy` otherwise.
OpenTelemetry collects ASP.NET Core request traces and metrics, activity outcome metrics, and structured `ILogger` records. `GET /metrics` exposes the metrics through the Prometheus scrape exporter. A request-completion log records trace ID, method, path, status and duration, including automatic validation responses; request and response bodies are not logged. The delay is clamped at zero for future-dated activities; historical late arrivals increase it. CloudWatch export remains proposed deployment work and is not configured here.

## Integration Tests

Run all tests from the repository root:

```sh
dotnet test IHub.sln --nologo
```

The `WebApplicationFactory` replaces the app's EF context with a uniquely named InMemory database for each test. No PostgreSQL server or Docker is used. The HTTP tests cover ingestion without a currency field, ZAR statement responses, source-scoped duplicates, conflicts, out-of-order events, window boundaries, invalid requests, concurrent request handling and the health endpoint.

EF Core InMemory is not relational: its concurrent duplicate test does not prove PostgreSQL uniqueness, transaction or locking behavior. PostgreSQL correctness is implemented with a named database constraint and an atomic intermediary identity upsert, but this environment did not run PostgreSQL-backed tests.

## Troubleshooting

- **Connection refused on port 5432:** Start Postgres.app or your local PostgreSQL service and verify it listens on port `5432`.
- **Password authentication failed:** Set `ConnectionStrings__DefaultConnection` for your local PostgreSQL account; do not put credentials in the repository.
- **Database does not exist:** Create `ihub`, then run `dotnet ef database update` as shown above.
- **Schema differs from the current model:** For this local assignment database, stop the API, drop/recreate `ihub`, and reapply `InitialCreate`. Future schema changes should add reviewed migrations rather than modify an applied migration.
- **Tests try to connect to PostgreSQL:** Run them with `dotnet test IHub.sln --nologo`; the test factory configures a separate InMemory context.
