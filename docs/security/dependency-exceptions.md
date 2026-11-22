# Dependency vulnerability exceptions

Known-vulnerable NuGet packages that are intentionally NOT upgraded, with the reason and the guard that keeps the exception safe.
`dotnet list src/LKvitai.MES.sln package --vulnerable --include-transitive` is expected to list only the packages below.

Last reviewed: 2026-11-22.

## Marten 7.0.0 (and transitive Marten via MassTransit.Marten 8.1.3)

| Advisory | Severity | Affected | Fixed | Vector |
| --- | --- | --- | --- | --- |
| [GHSA-vmw2-qwm8-x84c](https://github.com/advisories/GHSA-vmw2-qwm8-x84c) | Critical | Marten <= 8.36 | 8.37.0 | SQL injection through the `regConfig` parameter of the full-text search APIs (`Search`, `PlainTextSearch`, `PhraseSearch`, `WebStyleSearch`, `PrefixSearch`, sync and async) |
| [GHSA-rfx3-98h7-v3xp](https://github.com/advisories/GHSA-rfx3-98h7-v3xp) | Critical | Marten >= 7.0.0, < 8.37.4 | 8.37.4 | Unescaped string literals in the LINQ provider (`Dictionary` indexer key / `ContainsKey` in `Where`, runtime constants in `Select`) and tenant-id handling (`DeleteProjectionProgressAsync`, tenant partition DDL) |

The listing shows only the first advisory per package; NuGet audit (`NU1904`) reports both.

### Decision

Marten stays on 7.0.0. Every fixed release is a major upgrade (8.x), which also forces MassTransit and the event-store/projection code to move; that is too large and risky for a dependency-security PR.
Both advisories need specific API usage that this codebase does not have, verified on 2026-11-22:

* Full-text search: no use of `Search`/`PlainTextSearch`/`PhraseSearch`/`WebStyleSearch`/`PrefixSearch`/`NgramSearch`, `FullTextIndex` or `regConfig` anywhere in `src/` (`git grep`).
* Tenancy: no Marten multi-tenancy (`MultiTenanted`, `ForTenant`, ...) and no `DeleteProjectionProgressAsync`.
* LINQ: Marten documents/views (`AvailableStockView`, `AdjustmentHistoryView`, ...) have no `Dictionary` members, and no `Where` over a Marten query uses a dictionary indexer or `ContainsKey` (the `ContainsKey` calls in `DashboardController` and `AgnumPriceSyncService` run on in-memory dictionaries after the query was materialized). `Select` projections on Marten queries do not embed runtime string constants.

### Guard

`tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/MartenVulnerableApiUsageTests.cs` scans every `.cs` file under `src/` that mentions Marten and fails when the full-text search or tenancy APIs above appear.
It cannot detect a dictionary-keyed LINQ filter or a runtime constant in a `Select`; reviewers must reject those on Marten queries (or upgrade first).

### What the upgrade to Marten 8 would involve

* Marten 7.0.0 -> >= 8.37.4 (9.13.0+ is the other fixed line), and `MassTransit.Marten` -> a release built against Marten 8 (MassTransit 8.1.3 -> a current 8.x; MassTransit v9 is a licensed product, stay on 8.x if possible).
* Review event-store setup (`AddMarten` options, projection registration and async daemon, `Policies`/schema names), Marten 8 removed obsolete APIs and changed projection base classes and lifecycles.
* Re-run projection rebuild on a copy of production data (AvailableStock, ActiveReservations, ...) and the Warehouse integration tests against PostgreSQL.
* Re-check Npgsql and Weasel versions pulled in by Marten 8 against the central pins in `Directory.Packages.props`.
* When done: remove this section, the `MartenVulnerableApiUsageTests`, and keep `dotnet list ... --vulnerable` clean.

## Removed / resolved in the same review

For reference, these were resolved by upgrades or pins in `Directory.Packages.props` (transitive pinning is enabled with `CentralPackageTransitivePinningEnabled`): Npgsql, System.Text.Json, System.Security.Cryptography.Pkcs, System.Net.Http / System.Security.Cryptography.X509Certificates (pulled by FsCheck -> FSharp.Core 4.2.3, now FSharp.Core 8), SSH.NET, SixLabors.ImageSharp, OpenTelemetry.*, MySqlConnector (pulled by EFCore.BulkExtensions -> Pomelo, unused at runtime) and the deprecated OpenTelemetry.Exporter.Jaeger (replaced by the OTLP exporter).