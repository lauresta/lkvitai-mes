# E2E Test Suite

## Overview
PRD-1651 introduces a dedicated E2E test project at `src/tests/LKvitai.MES.Tests.E2E` with data-driven workflow scenarios and xUnit parallel execution.

## Workflow Coverage
- Inbound workflow tests (5 methods)
- Outbound workflow tests (7 methods)
- Valuation workflow tests (4 methods)
- Cycle-count workflow tests (3 methods)
- Transfer workflow tests (3 methods)

Total methods: 22

## Data-Driven Scenarios
Scenario files are stored in `src/tests/LKvitai.MES.Tests.E2E/tests/data`:
- `inbound-scenarios.json` (10 scenarios)
- `outbound-scenarios.json` (12 scenarios)
- `valuation-scenarios.json` (10 scenarios)
- `cycle-count-scenarios.json` (8 scenarios)
- `transfer-scenarios.json` (14 scenarios)

Total scenario records: 54

## Parallel Execution
`xunit.runner.json` configures:
- `parallelizeTestCollections=true`
- `maxParallelThreads=4`

The suite uses thread-bound database aliases (`test-db-1` to `test-db-4`) via `ParallelDatabaseAllocator`.

## Local Validation
```bash
cd src/tests/LKvitai.MES.Tests.E2E
dotnet test --logger "console;verbosity=detailed"
dotnet test --logger "console;verbosity=detailed" -- xUnit.ParallelizeTestCollections=true xUnit.MaxParallelThreads=4
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## CI Integration
GitHub Actions workflow: `.github/workflows/e2e-tests.yml`
- Trigger: every pull request and pushes to `main`
- Behavior: fails workflow when E2E tests fail
