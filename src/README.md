# LKvitai.MES Warehouse Core Phase 1

Production-grade .NET 8 modular monolith for warehouse management system.

## Architecture

This solution implements a **modular monolith** architecture with:

- **Event Sourcing** for critical aggregates (StockLedger, Reservation, Valuation)
- **Marten** for event store and projections
- **MassTransit** for saga orchestration and messaging
- **PostgreSQL** for relational storage
- **EF Core** for state-based aggregates
- **MediatR** command pipeline with idempotency
- **OpenTelemetry** for observability

## Solution Structure

```
LKvitai.MES.sln
├── LKvitai.MES.Api              # ASP.NET Core Web API
├── LKvitai.MES.Application      # Command handlers, behaviors
├── LKvitai.MES.Domain           # Aggregates, domain logic
├── LKvitai.MES.Infrastructure   # Marten, EF Core, outbox
├── LKvitai.MES.Projections      # Read models
├── LKvitai.MES.Sagas            # MassTransit sagas
├── LKvitai.MES.Integration      # External system adapters
├── LKvitai.MES.Contracts        # Events, DTOs
├── LKvitai.MES.SharedKernel     # Common abstractions
└── tests/
    ├── LKvitai.MES.Tests.Unit
    ├── LKvitai.MES.Tests.Property
    └── LKvitai.MES.Tests.Integration
```

## Prerequisites

- .NET 8 SDK
- PostgreSQL 15+
- Docker (optional, for local PostgreSQL)

## Getting Started

### 1. Setup PostgreSQL

```bash
docker run --name warehouse-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=lkvitai_warehouse_dev \
  -p 5432:5432 \
  -d postgres:15
```

For local broker-dependent scenarios, include RabbitMQ from dev compose:

```bash
docker compose -f src/docker-compose.yml --profile dev-broker up -d
```

### 2. Build Solution

```bash
cd src
dotnet restore
dotnet build
```

### 3. Run API

```bash
cd LKvitai.MES.Api
dotnet run
```

API will be available at: `https://localhost:5001`

Swagger UI: `https://localhost:5001/swagger`

### 4. Run Tests

```bash
# All tests (integration tests will be SKIPPED unless Docker is opted-in)
dotnet test LKvitai.MES.sln

# Unit tests only
dotnet test tests/LKvitai.MES.Tests.Unit

# Property-based tests only
dotnet test tests/LKvitai.MES.Tests.Property

# Integration tests (requires Docker — uses Testcontainers)
TESTCONTAINERS_ENABLED=1 dotnet test tests/LKvitai.MES.Tests.Integration
```

> **Note:** Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to
> spin up a real PostgreSQL instance in Docker. They are **opt-in** via the
> `TESTCONTAINERS_ENABLED=1` environment variable. When the variable is not set,
> `dotnet test` reports them as **Skipped** (not Failed), so the full solution
> test suite always passes on machines without Docker.

## Configuration

Configuration is in `appsettings.json`:

- **ConnectionStrings:WarehouseDb** - PostgreSQL connection string
- **UseInMemoryTransport** - Use in-memory transport for MassTransit (dev mode)
- **UseJaegerExporter** - Enable Jaeger tracing exporter

## Key Architectural Decisions

Per implementation blueprint:

1. **StockLedger** uses Marten expected-version append for atomic balance validation (MITIGATION V-2)
2. **PickStockSaga** is two-step saga without projection wait (MITIGATION V-3)
3. **StartPicking** uses optimistic concurrency for HARD lock acquisition (MITIGATION R-3)
4. **ActiveHardLocks** inline projection for efficient conflict detection (MITIGATION R-4)
5. **Projection rebuild** uses shadow table with verification (MITIGATION V-5)

## Implementation Status

This is a **skeleton/scaffold** with infrastructure baseline. Business logic to be implemented per `tasks.md`.

## Next Steps

1. Review `.kiro/specs/warehouse-core-phase1/tasks.md`
2. Implement aggregates per blueprint
3. Implement command handlers
4. Implement sagas
5. Implement projections
6. Write tests (unit, property, integration)

## Documentation

- Requirements: `.kiro/specs/warehouse-core-phase1/requirements.md`
- Design: `.kiro/specs/warehouse-core-phase1/design.md`
- Implementation Blueprint: `.kiro/specs/warehouse-core-phase1/implementation-blueprint.md`
- Tasks: `.kiro/specs/warehouse-core-phase1/tasks.md`
- Changelog: `.kiro/specs/warehouse-core-phase1/CHANGELOG.md`
