# Dependency Baseline Report

Generated: 2026-02-19 16:00:14 +02:00

## Violations

- src/LKvitai.MES.Application/LKvitai.MES.Application.csproj | PackageReference | Marten | Application references Marten
- src/LKvitai.MES.Contracts/LKvitai.MES.Contracts.csproj | ProjectReference | ..\LKvitai.MES.SharedKernel\LKvitai.MES.SharedKernel.csproj | Contracts references SharedKernel
- src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj | PackageReference | MediatR | SharedKernel references MediatR

## Known baseline violations (expected at this stage)

These findings are expected in early refactor phases and are report-only at P0.S2.T1. Strict enforcement is deferred to later phases.
