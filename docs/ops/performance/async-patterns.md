# Async Patterns (PRD-1644)

## Standards Applied
- Controller HTTP actions return `Task<IActionResult>` or `Task<ActionResult<T>>`.
- Cancellation tokens are passed through async I/O operations.
- Blocking sync-over-async patterns are disallowed:
  - `.Result`
  - `.Wait()`
  - `.GetAwaiter().GetResult()`
- `async void` is avoided for HTTP actions.

## Key Fixes
- Removed sync-over-async publish wait in `WarehouseDbContext.SaveChanges(...)`:
  - Replaced blocking `.GetAwaiter().GetResult()` with non-blocking fire-and-forget publish call.

## Validation Coverage
- `AsyncOperationTests` enforces:
  - No blocking async patterns in API/Infrastructure/Sagas source files.
  - Controller HTTP methods are task-based.
  - No async-void HTTP actions.
