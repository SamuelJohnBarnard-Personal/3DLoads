# 3DLoads

Initial implementation scaffold for a 3D bin packing solver with a phase pipeline:

- InitialGreedy
- Brkga (stubbed for next implementation slice)
- Cleanup (stubbed for next implementation slice)

## Stack

- .NET 8 Web API (controllers)
- PostgreSQL + EF Core (code-first style model)
- JWT authentication
- Async solve jobs via in-process background queue

## Solution layout

- `src/3DLoads.Api` REST API
- `src/3DLoads.Domain` domain entities and enums
- `src/3DLoads.Application` solver pipeline contracts and implementations
- `src/3DLoads.Infrastructure` EF Core persistence + background job processor
- `tests/3DLoads.Tests.Unit` solver baseline tests
- `tests/3DLoads.Tests.Integration` integration test project scaffold

## Configure database

Default connection string is in `src/3DLoads.Api/appsettings.json`.

For local dev:

1. Start PostgreSQL.
2. Create database `3dloads_dev` (or update connection string to match your existing DB).
3. Run API. Startup currently calls `EnsureCreated()`.

## Run

```powershell
dotnet build 3DLoads.slnx
dotnet run --project src/3DLoads.Api
```

Swagger UI is available in development mode at `https://localhost:7273/swagger`.

## Authentication

Get JWT token:

```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "admin"
}
```

Use returned bearer token for protected endpoints.

## Current API workflow

1. `POST /api/v1/scenarios` create scenario.
2. `POST /api/v1/scenarios/{scenarioId}/solve` queue async solve job.
3. `GET /api/v1/solve-jobs/{jobId}` check status.
4. `GET /api/v1/solve-jobs/{jobId}/result` fetch result when completed.

## Current scope in code

Implemented:

- Domain model scaffold for vans, packages, scenarios, solve jobs, solutions
- Orientation enumeration for fixed / this-side-up / free 90-degree rotation
- Baseline feasibility checks (bounds + AABB overlap)
- Initial greedy phase seed implementation
- Configurable phase pipeline contract and execution

Next slice planned:

- Extreme-point anchor space representation
- LIFO depth-claim enforcement by construction
- Support area and load-bearing checks
- BRKGA chromosome, decoder, fitness, and generation loop
- Cleanup local improvement passes
