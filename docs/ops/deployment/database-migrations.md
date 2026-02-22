# Database Migrations

## Scope
PRD-1654 migration testing coverage includes:
- Add column
- Add index
- Add table
- Rename/drop column
- Rollback and integrity checks

## Automated Validation
```bash
dotnet test src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj --filter "FullyQualifiedName~MigrationTests"
dotnet build src/LKvitai.MES.sln
dotnet test src/LKvitai.MES.sln
```

## Operational Commands
```bash
cd src/LKvitai.MES.Infrastructure
dotnet ef database update
dotnet ef database update <PreviousMigrationName>
```

## Notes
- Integration tests validate migration operation coverage and rollback metadata in migration classes.
- Live zero-downtime and load-based migration validation require a running database and load harness (`k6`).
