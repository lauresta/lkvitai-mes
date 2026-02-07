# LKvitai.MES Solution Structure

## Generated Solution Overview

This document describes the production-grade .NET 8 modular monolith solution skeleton generated for LKvitai.MES Warehouse Core Phase 1.

## Solution File Tree

```
src/
├── LKvitai.MES.sln                          # Solution file
├── global.json                              # .NET SDK version
├── Directory.Build.props                    # Common build properties
├── .gitignore                               # Git ignore rules
├── README.md                                # Getting started guide
├── SOLUTION_STRUCTURE.md                    # This file
│
├── LKvitai.MES.Api/                         # ASP.NET Core Web API
│   ├── LKvitai.MES.Api.csproj
│   ├── Program.cs                           # Application entry point
│   ├── appsettings.json                     # Configuration
│   ├── appsettings.Development.json
│   └── Configuration/
│       ├── MediatRConfiguration.cs          # Command pipeline setup
│       ├── MassTransitConfiguration.cs      # Saga orchestration setup
│       └── OpenTelemetryConfiguration.cs    # Observability setup
│
├── LKvitai.MES.Application/                 # Application layer
│   ├── LKvitai.MES.Application.csproj
│   └── Behaviors/
│       ├── IdempotencyBehavior.cs           # Command idempotency
│       ├── ValidationBehavior.cs            # FluentValidation
│       └── LoggingBehavior.cs               # Structured logging
│
├── LKvitai.MES.Domain/                      # Domain layer
│   ├── LKvitai.MES.Domain.csproj
│   └── Aggregates/
│       ├── StockLedger.cs                   # Event-sourced aggregate
│       ├── Reservation.cs                   # Event-sourced aggregate
│       ├── HandlingUnit.cs                  # State-based aggregate
│       ├── Valuation.cs                     # Event-sourced aggregate
│       └── WarehouseLayout.cs               # State-based aggregate
│
├── LKvitai.MES.Infrastructure/              # Infrastructure layer
│   ├── LKvitai.MES.Infrastructure.csproj
│   ├── Persistence/
│   │   ├── MartenConfiguration.cs           # Marten event store setup
│   │   └── WarehouseDbContext.cs            # EF Core DbContext
│   └── Outbox/
│       ├── OutboxMessage.cs                 # Outbox schema
│       └── OutboxProcessor.cs               # Background processor
│
├── LKvitai.MES.Projections/                 # Read models
│   ├── LKvitai.MES.Projections.csproj
│   ├── LocationBalanceProjection.cs         # Balance projection
│   └── ActiveHardLocksProjection.cs         # HARD locks projection (R-4)
│
├── LKvitai.MES.Sagas/                       # Saga orchestration
│   ├── LKvitai.MES.Sagas.csproj
│   ├── PickStockSaga.cs                     # Pick workflow (V-3)
│   └── ReceiveGoodsSaga.cs                  # Receipt workflow
│
├── LKvitai.MES.Integration/                 # External integrations
│   ├── LKvitai.MES.Integration.csproj
│   ├── LabelPrinting/
│   │   └── ILabelPrintingService.cs         # Operational integration
│   └── Agnum/
│       └── IAgnumExportService.cs           # Financial integration
│
├── LKvitai.MES.Contracts/                   # Events and DTOs
│   ├── LKvitai.MES.Contracts.csproj
│   └── Events/
│       ├── StockMovedEvent.cs
│       └── ReservationEvents.cs
│
├── LKvitai.MES.SharedKernel/                # Common abstractions
│   ├── LKvitai.MES.SharedKernel.csproj
│   ├── Result.cs                            # Result pattern
│   ├── ICommand.cs                          # Command interface
│   └── DomainEvent.cs                       # Event base class
│
└── tests/
    ├── LKvitai.MES.Tests.Unit/              # Unit tests
    │   ├── LKvitai.MES.Tests.Unit.csproj
    │   └── UnitTest1.cs
    │
    ├── LKvitai.MES.Tests.Property/          # Property-based tests
    │   ├── LKvitai.MES.Tests.Property.csproj
    │   └── PropertyTest1.cs
    │
    └── LKvitai.MES.Tests.Integration/       # Integration tests
        ├── LKvitai.MES.Tests.Integration.csproj
        └── IntegrationTest1.cs
```

## Project Dependencies

```
LKvitai.MES.Api
├── LKvitai.MES.Application
├── LKvitai.MES.Infrastructure
├── LKvitai.MES.Projections
├── LKvitai.MES.Sagas
└── LKvitai.MES.Integration

LKvitai.MES.Application
├── LKvitai.MES.Domain
└── LKvitai.MES.Infrastructure

LKvitai.MES.Domain
├── LKvitai.MES.SharedKernel
└── LKvitai.MES.Contracts

LKvitai.MES.Infrastructure
├── LKvitai.MES.Domain
└── LKvitai.MES.Contracts

LKvitai.MES.Projections
└── LKvitai.MES.Contracts

LKvitai.MES.Sagas
├── LKvitai.MES.Contracts
└── LKvitai.MES.Application

LKvitai.MES.Integration
└── LKvitai.MES.Contracts

LKvitai.MES.Contracts
└── LKvitai.MES.SharedKernel
```

## Key NuGet Packages

### Infrastructure
- **Marten 7.0.0** - Event store and document database
- **Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0** - PostgreSQL provider
- **Microsoft.EntityFrameworkCore 8.0.0** - ORM for state-based aggregates

### Application
- **MediatR 12.2.0** - Command pipeline
- **FluentValidation 11.9.0** - Command validation

### Messaging & Sagas
- **MassTransit 8.1.3** - Saga orchestration and messaging
- **MassTransit.Marten 8.1.3** - Saga persistence
- **MassTransit.RabbitMQ 8.1.3** - RabbitMQ transport

### Observability
- **Serilog.AspNetCore 8.0.0** - Structured logging
- **OpenTelemetry.Extensions.Hosting 1.7.0** - Distributed tracing
- **OpenTelemetry.Instrumentation.AspNetCore 1.7.0** - ASP.NET Core instrumentation

### Testing
- **xunit 2.6.2** - Test framework
- **FsCheck 2.16.6** - Property-based testing
- **FluentAssertions 6.12.0** - Assertion library
- **Moq 4.20.70** - Mocking framework
- **Testcontainers.PostgreSql 3.6.0** - Integration test containers

## Architectural Patterns Implemented

### 1. Modular Monolith
- Clear module boundaries via projects
- Dependency flow: Api → Application → Domain
- Infrastructure as cross-cutting concern

### 2. Event Sourcing
- Marten event store for critical aggregates
- Event-sourced: StockLedger, Reservation, Valuation
- State-based: HandlingUnit, WarehouseLayout

### 3. CQRS
- Commands via MediatR pipeline
- Queries via projections (read models)
- Separation of write and read concerns

### 4. Saga Pattern
- MassTransit state machines
- Saga persistence via Marten
- Orchestration of multi-aggregate workflows

### 5. Transactional Outbox
- Atomic event publishing
- At-least-once delivery guarantee
- Background processor for outbox

### 6. Command Pipeline
- Idempotency behavior
- Validation behavior
- Logging behavior
- Transaction behavior (implicit in Marten)

## Architectural Mitigations Integrated

Per CHANGELOG.md:

- **V-2**: StockLedger uses expected-version append (atomic balance validation)
- **V-3**: PickStockSaga simplified (no projection wait)
- **R-3**: StartPicking uses optimistic concurrency (HARD lock acquisition)
- **R-4**: ActiveHardLocks inline projection (conflict detection)
- **V-5**: Projection rebuild with shadow table verification

## Configuration

### appsettings.json
- **ConnectionStrings:WarehouseDb** - PostgreSQL connection
- **UseInMemoryTransport** - MassTransit transport mode
- **UseJaegerExporter** - Tracing exporter
- **RabbitMQ** - RabbitMQ connection settings
- **Jaeger** - Jaeger agent settings
- **Serilog** - Logging configuration

## Build and Run

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API
cd LKvitai.MES.Api
dotnet run

# Run tests
dotnet test
```

## Next Steps

1. **Review Spec Documents**
   - `.kiro/specs/warehouse-core-phase1/requirements.md`
   - `.kiro/specs/warehouse-core-phase1/design.md`
   - `.kiro/specs/warehouse-core-phase1/implementation-blueprint.md`

2. **Implement Business Logic**
   - Follow tasks in `.kiro/specs/warehouse-core-phase1/tasks.md`
   - Start with Task 1: Solution Structure (COMPLETE)
   - Continue with Task 2: StockLedger aggregate

3. **Write Tests**
   - Unit tests for aggregates
   - Property tests for correctness properties
   - Integration tests for workflows

## Status

✅ **COMPLETE**: Solution skeleton and infrastructure baseline
⏳ **PENDING**: Business logic implementation per tasks.md

This is a **compile-ready** solution with all infrastructure wired up. Business logic to be implemented incrementally per implementation plan.
