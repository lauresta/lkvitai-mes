# ADR-002: Event Schema Versioning

## Status
Accepted

## Context
- Events are immutable once persisted.
- Consumers need a stable way to process older event payloads after schema changes.
- Unknown schema versions must fail fast to avoid silent data corruption.

## Decision
- `DomainEvent` carries `SchemaVersion` (default `v1`).
- Version support is tracked by `IEventSchemaVersionRegistry`.
- Upcasters implement `IEventUpcaster<TSource, TTarget>` and define `SourceVersion`/`TargetVersion`.
- A sample upcaster (`StockMovedV1Event -> StockMovedEvent`) demonstrates field rename/defaulting.
- Event publishing validates known versions and upcasts to the latest schema before publish.

## Consequences
- Existing event types are backward-compatible with `v1` by default.
- New schema evolution should add an upcaster chain (`v1 -> v2 -> v3`) instead of rewriting history.
- Unknown versions throw immediately with explicit type/version details.
