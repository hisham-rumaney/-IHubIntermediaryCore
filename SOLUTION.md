# Solution Notes

## 1. Scope

This repository implements a small activity-ingestion and statement API for SanlamConnect IHub. It deliberately contains no frontend, message queue, AWS deployment, Gridify integration, unit-test project or Docker test dependency. The two business routes are activity ingestion and statement retrieval; the combined health endpoint is operational.

## 2. Assumptions

- **Delivery:** Activities may arrive late or out of order; statement membership is based on occurrence time.
- **Identity:** The upstream system provides a stable source code and external activity ID. Together, they uniquely identify an activity. Source codes are trimmed and uppercased; external IDs remain case-sensitive.
- **Currency:** Callers do not provide currency, and intermediaries do not configure it. All activities and statements use `ZAR`.
- **Intermediary:** An accepted activity can create its intermediary identity row; the row does not need to exist beforehand.

## 3. Clean Architecture

The solution follows Clean Architecture with dependencies pointing inward: API and Data depend on Application, while Application depends on Domain. Layer names mirror the TopScorers reference; persistence is named `IHub.Data` rather than introducing a separate Infrastructure project.

```text
IHub.Api -> IHub.Services -> IHub.Application -> IHub.Domain
     |                         ^
     +-> IHub.Data ------------+

IHub.IntegrationTests -> IHub.Api (real HTTP host; InMemory EF context)
```

- **Domain:** Activity and Intermediary identity entities plus domain exceptions. It has no ASP.NET Core or EF Core dependency.
- **Application:** Request/response DTOs, `IHubActivityService` contract, repository interfaces and `IUnitOfWork`.
- **Services:** `HubActivityService` implements the Application contract and owns validation, idempotency, business logging and activity outcome instrumentation.
- **Data:** `IHubDbContext`, PostgreSQL mappings, repositories, conflict translation and migrations. This is the persistence implementation boundary.
- **API:** Controllers, `GlobalExceptionHandler`, startup composition, health and OpenTelemetry instrumentation.
- **Integration tests:** xUnit and `WebApplicationFactory` exercise the HTTP application with EF Core InMemory and no repository/service mocks.

Controllers only call Services and translate the result to HTTP status codes. They do not contain business processing or access the DbContext. The statement repository computes aggregates in SQL rather than materializing activities.

## 4. Data and Persistence

### Repositories and Unit of Work

- `IActivityRepository` persists activities and finds the external identity `(SourceSystem, ExternalActivityId)`.
- `IIntermediaryRepository` ensures an intermediary identity row exists. PostgreSQL uses an atomic `INSERT ... ON CONFLICT DO NOTHING` for repeated submissions.
- `IStatementRepository` executes the aggregate and latest-ingestion queries for a requested intermediary and window.
- `IUnitOfWork` provides asynchronous `SaveChangesAsync`; the Data implementation translates only a violation of the named activity identity constraint into an application-level domain exception.

An activity insert is one persistence operation. The intermediary identity upsert is atomic but separate from that insert; if a later activity insert fails transiently, an intermediary row may remain without activities. That row contributes no financial total and can be reused by a retry. There is no explicit multi-row transaction because the activity is a single persisted row and identity creation uses its own atomic upsert.

### Activity Data Model

- Each activity stores an internal `Guid`, source system, external activity ID, intermediary ID, occurrence time, amount, and ingestion time. Currency is fixed to ZAR in the application.
- An intermediary row stores only its ID and creation time; it does not configure currency.
- External identity is `(SourceSystem, ExternalActivityId)`. Different sources can reuse the same external ID without being treated as the same activity.
- PostgreSQL stores timestamps as `timestamp with time zone` and amounts as `numeric(18,2)`.

### Constraints and Indexes

- `Activity.Id`: primary key.
- `Activity.SourceSystem` + `Activity.ExternalActivityId`: unique external identity for idempotency.
- `Intermediary.IntermediaryId`: primary key.
- `Activity.IntermediaryId` + `Activity.OccurredAt`: statement-window lookup index.

### Idempotency and Concurrency

The application first checks the composite external identity for the common duplicate path. An identical payload returns the existing activity without a second contribution; a different intermediary, occurrence time or amount returns conflict without overwriting the original. For a new identity, it ensures an intermediary identity row with an atomic upsert, assigns `ZAR`, and inserts the activity through the Unit of Work.

The pre-check is an optimization, not the integrity boundary. If PostgreSQL reports a violation of the composite unique external identity, the Data Unit of Work translates that specific violation. Application reloads the existing row by the composite identity and returns the identical-duplicate outcome or a conflict. Other database errors are not mislabeled as duplicates. The database unique constraint prevents concurrent requests from both contributing.

The intermediary upsert only creates the identity row; there is no currency race because the currency is fixed in application code. InMemory does not model PostgreSQL's primary-key behavior under concurrent first-time creation.

### Out-of-Order Activities

Statement membership uses `OccurredAt`, never arrival order or `IngestedAt`. A late activity whose occurrence is inside a historical window is included there. No running total or precomputed statement is maintained, so inserting late data does not require repairing stored aggregates.

### Statement Query

The statement repository filters by intermediary and the half-open occurrence-time interval `[start, end)`. It computes `GrossTotal` with a database `SUM` and `LastUpdated` with `MAX(IngestedAt)` over the same matches, without materializing activities. Empty totals are zero and `LastUpdated` is null.

## 5. API Contracts

### `POST /activities`

Request fields: `sourceSystem`, `externalActivityId`, `intermediaryId`, `occurredAt`, `amount`. There is no currency request field.

- New persisted activity: `201 Created` with the activity response.
- Identical duplicate: `200 OK` with the existing activity response.
- Conflicting duplicate: `409 Conflict` ProblemDetails.
- Invalid data: `400 Bad Request` ProblemDetails.
- Unexpected failure: generic `500` ProblemDetails without exception details.

`GlobalExceptionHandler` maps domain validation exceptions to `400`, identity conflicts to `409`, and unexpected exceptions to a sanitized `500` response. It logs the exception with the request trace ID and includes that ID in ProblemDetails.

### `GET /intermediaries/{intermediaryId}/statement?start=...&end=...`

Returns intermediary ID, currency, requested start/end, gross total and last-updated timestamp. Start must be less than end. Statement membership uses the half-open occurrence-time interval documented above.

## 6. Observability

Serilog writes structured JSON console events for API and application logs, including request/trace ID, source, external activity ID, intermediary, outcome and request duration. The Serilog OTLP sink is included as commented configuration and is not active; enable it after an OpenTelemetry Collector and `OTEL_EXPORTER_OTLP_ENDPOINT` are configured. Logs do not include full financial payloads or credentials. ASP.NET Core exceptions are logged through Serilog; HTTP ProblemDetails for unexpected failures is generic.

OpenTelemetry instruments ASP.NET Core request traces and metrics, custom `Meter` counters for accepted activities, identical duplicates, conflicting duplicates, failed ingestion and request errors, and an occurrence-to-ingestion delay histogram. `GET /metrics` exposes metrics through the Prometheus scrape exporter. A request-logging middleware emits `ILogger` events handled by Serilog for every completed HTTP response, including automatic model-validation errors, with trace ID, method, path, status code and duration; request/response bodies are deliberately excluded. The global exception handler logs exception details and returns sanitized ProblemDetails for unexpected failures. OTLP log forwarding is currently commented out; no OTLP trace exporter or CloudWatch integration is configured. The Prometheus exporter dependency is prerelease.

`GET /health` is a custom combined check: an HTTP response establishes process liveness, while the registered database check verifies PostgreSQL connectivity. It returns `200` with a sanitized `healthy` status when connected and `503` with `unhealthy` otherwise; it does not expose database exception details.

## 7. Proposed AWS Alerts

These are initial suggestions only and require tuning against production baselines:

1. **Elevated ingestion/request errors:** alarm when the 5-minute error ratio exceeds 5% with at least 100 requests. Page the service owner, inspect structured logs by request ID and outcome, then check RDS connectivity, validation/conflict rates and the latest deployment. Do not retry permanent validation errors blindly.
2. **Increasing processing delay / queue age:** in a queued design, alarm when SQS `ApproximateAgeOfOldestMessage` exceeds 5 minutes for 10 minutes; also watch ingestion delay percentiles once exported. Check Lambda throttles/concurrency, database connections, RDS CPU/IO, retry/DLQ volume and producer bursts. Scale consumers only within database connection capacity; pause or reduce intake if the database is saturated.

## 8. Proposed AWS Architecture (Design Only)

No AWS resources are provisioned by this repository. A possible high-volume design is:

```text
Product systems
   | HTTPS or direct SQS publish
   v
ALB -> ECS/Fargate .NET API (statements and optional ingestion front door)
                         |
                         | enqueue accepted activity envelope
                         v
                    Amazon SQS (standard queue)
                         |
                         v
              .NET AWS Lambda consumers
                         |
                         v
                 RDS for PostgreSQL

Secrets Manager / Parameter Store -> API and Lambda configuration
CloudWatch Logs, metrics and alarms <- ALB, ECS, Lambda, SQS and RDS
SQS redrive policy -> dead-letter queue (DLQ)
```

A standard SQS queue is a good fit when event ordering is not required: statements use event occurrence time, and the database identity makes retries idempotent. The queue absorbs bursts, separates upstream availability from database write capacity, buffers work during short RDS interruptions and lets Lambda concurrency be controlled independently. If strict per-key delivery ordering becomes a real requirement, evaluate FIFO queues and their throughput/deduplication trade-offs instead.

Two deployment shapes are possible. Upstream systems may publish validated envelopes directly to SQS, or the API may enqueue and return `202 Accepted`. The current local `POST /activities` persists synchronously and returns `201`/`200`; an asynchronous enqueue contract would be a deliberate API change. Lambda consumers should call the same Application ingestion use case and Data persistence boundary rather than duplicate business rules.

SQS and Lambda provide at-least-once delivery, not exactly-once processing. Delete a message only after the database commit. The `(SourceSystem, ExternalActivityId)` constraint remains the final idempotency guard when a Lambda succeeds in PostgreSQL but fails before deleting the message. Set the visibility timeout above the Lambda's maximum useful processing time, return partial batch failures so successful records are not needlessly retried, and bound Lambda reserved concurrency to the RDS connection budget (consider RDS Proxy if justified).

Classify failures deliberately: transient database/network errors should be retried with bounded backoff; permanent payload/schema failures should be surfaced and moved to a DLQ rather than retried indefinitely. Configure a redrive policy after a small number of receives (for example, five), alarm on DLQ depth and oldest-message age, retain diagnostic metadata without logging secrets, and provide an operator-reviewed redrive path after correcting the underlying issue. Avoid automatically replaying a whole DLQ without checking whether the defect is fixed.

Use RDS PostgreSQL in private subnets, an ALB for the API, ECS/Fargate for the synchronous API and statement path, and Lambda only for queued ingestion if the operating model benefits from it. Store credentials in Secrets Manager or Parameter Store, rotate them, and grant each workload least-privilege access. Use CloudWatch structured logs, metrics and alarms. Scale API tasks on request load and Lambda with queue depth while maintaining RDS headroom; queue age is a better signal of unmet ingestion demand than Lambda concurrency alone.

For production schema changes, run one controlled migration task in the deployment pipeline before rolling out code that requires the new schema. Use additive/backward-compatible changes first, deploy consumers that tolerate old and new messages during rollout, then remove obsolete schema only after old tasks are gone. Keep a rollback plan for application versions; database rollback is not always safe after new data has been written, so prefer a forward-fix for destructive migrations.

## 9. Trade-offs and Next Steps

- `IHub.Data` intentionally follows the reference repository's layer naming; it owns the same persistence responsibilities commonly named Infrastructure.
- Serilog writes structured JSON logs to the console. Its OTLP sink is included but not active; OpenTelemetry tracing is instrumented and metrics are exposed through Prometheus, while trace export and CloudWatch integration remain deployment work.
- The current `IUnitOfWork` persists through EF `SaveChangesAsync`; intermediary identity creation uses a database-native atomic upsert.
- The single initial migration is suitable for a fresh local database. Production schema evolution should use reviewed incremental EF migrations and apply them as a single deployment task before rolling out dependent code.
- Add PostgreSQL-backed integration tests and query-plan inspection when a disposable or managed PostgreSQL environment is available.
- Gridify is a possible future enhancement if clients later need controlled filtering, sorting or pagination. The current statement API intentionally supports only one intermediary and one time window.
- A queued ingestion workflow with SQS and Lambda remains a proposal; it is not installed or implemented here.

## 10. AI Assistance

GitHub Copilot was used to assist with repository analysis, implementation, integration-test changes and documentation drafting. The recorded build and test claims in these files refer to commands run locally; no AWS deployment, PostgreSQL-backed concurrency test or external review is claimed.
