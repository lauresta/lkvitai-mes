# Codex Diagnostic Report

## 1) Timestamp
- 2026-02-18 10:28:01 +02:00

## 2) Suspicious commits list (hash + subject)
- 82b0749 - UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)
- c9d637c - UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)
- 0895845 - docs(audit): add UI-API coverage, gaps, counts, and universe trace

## 3) Files changed per suspicious commit
### 82b0749
- docs/audit/UI-API-COUNTS.md
- docs/audit/UI-API-COVERAGE.md
- docs/audit/UI-API-GAPS.md
- docs/audit/UI-UNIVERSE-TRACE.md
- src/LKvitai.MES.WebUI/Config/GapWorkbenchEndpoints.json
- src/LKvitai.MES.WebUI/Pages/Admin/GapWorkbench.razor
- src/LKvitai.MES.WebUI/Program.cs
- src/LKvitai.MES.WebUI/Services/ApiWorkbenchClient.cs
- src/LKvitai.MES.WebUI/Shared/NavMenu.razor

### c9d637c
- src/LKvitai.MES.WebUI/Services/ProjectionsClient.cs
- src/LKvitai.MES.WebUI/Services/ReservationsClient.cs

### 0895845
- docs/audit/UI-API-COUNTS.md
- docs/audit/UI-API-COVERAGE.md
- docs/audit/UI-API-GAPS.md
- docs/audit/UI-UNIVERSE-TRACE.md

## A) Current history + working tree (requested command outputs)
### `git status --porcelain`
```text
<clean>
```

### `git log -15 --oneline`
```text
82b0749 UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)
c9d637c UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)
0895845 docs(audit): add UI-API coverage, gaps, counts, and universe trace
ec18476 PRD-1660 Go-live checklist
fd40924 PRD-1659 Production runbook
c515fc8 PRD-1658 Feature flags
7a1e346 PRD-1657 Canary releases
d9178e0 PRD-1656 Blue-green deployment
1e939f5 PRD-1655 Rollback procedures
d582387 PRD-1654 Data migration tests
5b56477 PRD-1653 Failover testing
05a5c85 PRD-1652 Chaos engineering
ad11e31 PRD-1651 E2E test suite expansion
14268ee chore: trigger CI
83c6d68 Handle projection rebuild conflicts as idempotency in progress
```

### `git log -5 --oneline --decorate`
```text
82b0749 (HEAD -> main) UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)
c9d637c UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)
0895845 (origin/main, origin/HEAD) docs(audit): add UI-API coverage, gaps, counts, and universe trace
ec18476 PRD-1660 Go-live checklist
fd40924 PRD-1659 Production runbook
```

### `git show --name-only HEAD`
```text
commit 82b0749c60ac917aaa3c8f4504d9aa919b3ea685
Author: Denisas Bykovas <bykovas@icloud.com>
Date:   Wed Feb 18 10:12:51 2026 +0200

    UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)

    How to verify:\n1) Open /warehouse/admin/gap-workbench from Admin nav.\n2) Select a listed endpoint, fill route/body as needed, and run.\n3) Confirm HTTP status and response body are shown for each operation.\n4) Verify /projections and /reservations continue working against canonical APIs.

docs/audit/UI-API-COUNTS.md
docs/audit/UI-API-COVERAGE.md
docs/audit/UI-API-GAPS.md
docs/audit/UI-UNIVERSE-TRACE.md
src/LKvitai.MES.WebUI/Config/GapWorkbenchEndpoints.json
src/LKvitai.MES.WebUI/Pages/Admin/GapWorkbench.razor
src/LKvitai.MES.WebUI/Program.cs
src/LKvitai.MES.WebUI/Services/ApiWorkbenchClient.cs
src/LKvitai.MES.WebUI/Shared/NavMenu.razor
```

### `git show --name-only HEAD~1`
```text
commit c9d637c1518a6972f5f9ace6a6358005ee358cb5
Author: Denisas Bykovas <bykovas@icloud.com>
Date:   Wed Feb 18 10:12:39 2026 +0200

    UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)

src/LKvitai.MES.WebUI/Services/ProjectionsClient.cs
src/LKvitai.MES.WebUI/Services/ReservationsClient.cs
```

### `git show --stat HEAD`
```text
commit 82b0749c60ac917aaa3c8f4504d9aa919b3ea685
Author: Denisas Bykovas <bykovas@icloud.com>
Date:   Wed Feb 18 10:12:51 2026 +0200

    UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)

    How to verify:\n1) Open /warehouse/admin/gap-workbench from Admin nav.\n2) Select a listed endpoint, fill route/body as needed, and run.\n3) Confirm HTTP status and response body are shown for each operation.\n4) Verify /projections and /reservations continue working against canonical APIs.

 docs/audit/UI-API-COUNTS.md                        |   6 +-
 docs/audit/UI-API-COVERAGE.md                      | 116 +++++-----
 docs/audit/UI-API-GAPS.md                          |  67 +-----
 docs/audit/UI-UNIVERSE-TRACE.md                    |  17 +-
 .../Config/GapWorkbenchEndpoints.json              | 234 +++++++++++++++++++++
 .../Pages/Admin/GapWorkbench.razor                 | 142 +++++++++++++
 src/LKvitai.MES.WebUI/Program.cs                   |   1 +
 .../Services/ApiWorkbenchClient.cs                 |  50 +++++
 src/LKvitai.MES.WebUI/Shared/NavMenu.razor         |   1 +
 9 files changed, 505 insertions(+), 129 deletions(-)
```

### `git show --stat HEAD~1`
```text
commit c9d637c1518a6972f5f9ace6a6358005ee358cb5
Author: Denisas Bykovas <bykovas@icloud.com>
Date:   Wed Feb 18 10:12:39 2026 +0200

    UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)

 src/LKvitai.MES.WebUI/Services/ProjectionsClient.cs  | 4 ++--
 src/LKvitai.MES.WebUI/Services/ReservationsClient.cs | 6 +++---
 2 files changed, 5 insertions(+), 5 deletions(-)
```

## B) Filtered suspicious log output
### Equivalent of `git log -50 --oneline | grep -i -E "gap|workbench|audit|ui-api|coverage|reservations|projections"`
```text
82b0749 UI: gap workbench endpoint operations surface (closes audit GAP_NO_UI endpoints)
c9d637c UI: canonical projections and reservations routes (closes /api/warehouse/v1/admin/projections/*, /api/warehouse/v1/reservations/*)
0895845 docs(audit): add UI-API coverage, gaps, counts, and universe trace
7c0e20f 	new file:   docs/repo-audit/2026-02-16-repo-audit-vs-target.md
```

## 4) Build status at HEAD
- Status: FAIL
- Command: `dotnet build src/LKvitai.MES.sln`
- Final 40 lines captured:
```text
EXIT_CODE=1
  Determining projects to restore...
  All projects are up-to-date for restore.
  LKvitai.MES.SharedKernel -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.SharedKernel\bin\Debug\net8.0\LKvitai.MES.SharedKernel.dll
  LKvitai.MES.Tests.E2E -> C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.E2E\bin\Debug\net8.0\LKvitai.MES.Tests.E2E.dll
  LKvitai.MES.WebUI -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.WebUI\bin\Debug\net8.0\LKvitai.MES.WebUI.dll
  LKvitai.MES.Contracts -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Contracts\bin\Debug\net8.0\LKvitai.MES.Contracts.dll
  LKvitai.MES.Domain -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Domain\bin\Debug\net8.0\LKvitai.MES.Domain.dll
  LKvitai.MES.Integration -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Integration\bin\Debug\net8.0\LKvitai.MES.Integration.dll
  LKvitai.MES.Application -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Application\bin\Debug\net8.0\LKvitai.MES.Application.dll
  LKvitai.MES.Projections -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Projections\bin\Debug\net8.0\LKvitai.MES.Projections.dll
  LKvitai.MES.Sagas -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Sagas\bin\Debug\net8.0\LKvitai.MES.Sagas.dll
  LKvitai.MES.Tests.Property -> C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Property\bin\Debug\net8.0\LKvitai.MES.Tests.Property.dll
  LKvitai.MES.Infrastructure -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Infrastructure\bin\Debug\net8.0\LKvitai.MES.Infrastructure.dll
  LKvitai.MES.Api -> C:\Sources\clients\lauresta\lkvitai-mes\src\LKvitai.MES.Api\bin\Debug\net8.0\LKvitai.MES.Api.dll
  LKvitai.MES.Tests.Integration -> C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Integration\bin\Debug\net8.0\LKvitai.MES.Tests.Integration.dll
C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Unit\AdvancedWarehouseStoreTests.cs(16,55): error CS0023: Operator '.' cannot be applied to operand of type 'void' [C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj]

Build FAILED.

C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Unit\AdvancedWarehouseStoreTests.cs(16,55): error CS0023: Operator '.' cannot be applied to operand of type 'void' [C:\Sources\clients\lauresta\lkvitai-mes\src\tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj]
    0 Warning(s)
    1 Error(s)

Time Elapsed 00:00:09.74
```

- Extracted error details:
- File path: `src/tests/LKvitai.MES.Tests.Unit/AdvancedWarehouseStoreTests.cs`
- Line: `16` (column `55`)
- Error code: `CS0023`
- Offending expression (from source line 16): `orders.Reverse().ToArray()`

## D) Failing file context (requested)
```text
   1: using FluentAssertions;
   2: using LKvitai.MES.Api.Services;
   3: using Xunit;
   4: 
   5: namespace LKvitai.MES.Tests.Unit;
   6: 
   7: public class AdvancedWarehouseStoreTests
   8: {
   9:     [Fact]
  10:     public void CreateWave_WithSameOrderSet_ShouldBeIdempotent()
  11:     {
  12:         var store = new AdvancedWarehouseStore();
  13:         var orders = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") };
  14: 
  15:         var first = store.CreateWave(orders, "picker-1");
  16:         var second = store.CreateWave(orders.Reverse().ToArray(), null);
  17: 
  18:         second.Id.Should().Be(first.Id);
  19:         second.WaveNumber.Should().Be(first.WaveNumber);
  20:     }
  21: 
  22:     [Fact]
  23:     public void CreateWave_PickList_ShouldBeSortedByLocation()
  24:     {
  25:         var store = new AdvancedWarehouseStore();
  26:         var orders = Enumerable.Range(1, 5)
  27:             .Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"))
  28:             .ToArray();
  29: 
  30:         var wave = store.CreateWave(orders, null);
  31: 
  32:         var sorted = wave.PickList
  33:             .OrderBy(x => x.Location, StringComparer.Ordinal)
  34:             .Select(x => x.Location)
  35:             .ToArray();
  36: 
  37:         wave.PickList.Select(x => x.Location).ToArray().Should().Equal(sorted);
  38:     }
  39: 
  40:     [Fact]
  41:     public void SerialTransition_ShouldAppendHistory()
  42:     {
  43:         var store = new AdvancedWarehouseStore();
  44:         var serial = store.RegisterSerial(new SerialRegisterRequest(1, "SN-001", "A1", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), "operator"));
  45: 
  46:         var transitioned = store.TransitionSerial(serial.Id, new SerialTransitionRequest("AVAILABLE", "A2", "operator"));
  47: 
  48:         transitioned.Should().NotBeNull();
  49:         transitioned!.Status.Should().Be(SerialStatus.Available);
  50:         transitioned.History.Should().HaveCount(2);
  51:         transitioned.History.Last().NewStatus.Should().Be(SerialStatus.Available);
  52:     }
  53: }
```

## 5) Build status at HEAD~1
- Status: FAIL
- Command sequence used:
- `git worktree add C:/tmp/wt_prev HEAD~1`
- `dotnet build src/LKvitai.MES.sln`
- `git worktree remove C:/tmp/wt_prev --force`
- Final 40 lines captured:
```text
EXIT_CODE=1
  Restored C:\tmp\wt_prev\src\LKvitai.MES.Projections\LKvitai.MES.Projections.csproj (in 454 ms).
  Restored C:\tmp\wt_prev\src\LKvitai.MES.Application\LKvitai.MES.Application.csproj (in 1,17 sec).
  Restored C:\tmp\wt_prev\src\LKvitai.MES.SharedKernel\LKvitai.MES.SharedKernel.csproj (in 8 ms).
  Restored C:\tmp\wt_prev\src\LKvitai.MES.Sagas\LKvitai.MES.Sagas.csproj (in 48 ms).
  Restored C:\tmp\wt_prev\src\LKvitai.MES.Infrastructure\LKvitai.MES.Infrastructure.csproj (in 1,28 sec).
  Restored C:\tmp\wt_prev\src\LKvitai.MES.Api\LKvitai.MES.Api.csproj (in 1,32 sec).
  Restored C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Property\LKvitai.MES.Tests.Property.csproj (in 1,42 sec).
  Restored C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.E2E\LKvitai.MES.Tests.E2E.csproj (in 1,4 sec).
  Restored C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj (in 741 ms).
  Restored C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Integration\LKvitai.MES.Tests.Integration.csproj (in 1,45 sec).
  LKvitai.MES.SharedKernel -> C:\tmp\wt_prev\src\LKvitai.MES.SharedKernel\bin\Debug\net8.0\LKvitai.MES.SharedKernel.dll
  LKvitai.MES.Tests.E2E -> C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.E2E\bin\Debug\net8.0\LKvitai.MES.Tests.E2E.dll
C:\tmp\wt_prev\src\LKvitai.MES.Contracts\Messages\PickStockMessages.cs(5,1): warning CS1587: XML comment is not placed on a valid language element [C:\tmp\wt_prev\src\LKvitai.MES.Contracts\LKvitai.MES.Contracts.csproj]
  LKvitai.MES.Contracts -> C:\tmp\wt_prev\src\LKvitai.MES.Contracts\bin\Debug\net8.0\LKvitai.MES.Contracts.dll
C:\tmp\wt_prev\src\LKvitai.MES.Integration\LabelPrinting\ILabelPrintingService.cs(7,31): warning CS1570: XML comment has badly formed XML -- 'An identifier was expected.' [C:\tmp\wt_prev\src\LKvitai.MES.Integration\LKvitai.MES.Integration.csproj]
C:\tmp\wt_prev\src\LKvitai.MES.Integration\LabelPrinting\ILabelPrintingService.cs(7,31): warning CS1570: XML comment has badly formed XML -- 'The character(s) '5' cannot be used at this location.' [C:\tmp\wt_prev\src\LKvitai.MES.Integration\LKvitai.MES.Integration.csproj]
  LKvitai.MES.Integration -> C:\tmp\wt_prev\src\LKvitai.MES.Integration\bin\Debug\net8.0\LKvitai.MES.Integration.dll
  LKvitai.MES.Domain -> C:\tmp\wt_prev\src\LKvitai.MES.Domain\bin\Debug\net8.0\LKvitai.MES.Domain.dll
  LKvitai.MES.Projections -> C:\tmp\wt_prev\src\LKvitai.MES.Projections\bin\Debug\net8.0\LKvitai.MES.Projections.dll
C:\tmp\wt_prev\src\LKvitai.MES.Application\Projections\IProjectionRebuildService.cs(26,14): warning CS1573: Parameter 'resetProgress' has no matching param tag in the XML comment for 'IProjectionRebuildService.RebuildProjectionAsync(string, bool, bool, CancellationToken)' (but other parameters do) [C:\tmp\wt_prev\src\LKvitai.MES.Application\LKvitai.MES.Application.csproj]
  LKvitai.MES.Application -> C:\tmp\wt_prev\src\LKvitai.MES.Application\bin\Debug\net8.0\LKvitai.MES.Application.dll
  LKvitai.MES.Tests.Property -> C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Property\bin\Debug\net8.0\LKvitai.MES.Tests.Property.dll
  LKvitai.MES.Infrastructure -> C:\tmp\wt_prev\src\LKvitai.MES.Infrastructure\bin\Debug\net8.0\LKvitai.MES.Infrastructure.dll
  LKvitai.MES.WebUI -> C:\tmp\wt_prev\src\LKvitai.MES.WebUI\bin\Debug\net8.0\LKvitai.MES.WebUI.dll
  LKvitai.MES.Sagas -> C:\tmp\wt_prev\src\LKvitai.MES.Sagas\bin\Debug\net8.0\LKvitai.MES.Sagas.dll
  LKvitai.MES.Api -> C:\tmp\wt_prev\src\LKvitai.MES.Api\bin\Debug\net8.0\LKvitai.MES.Api.dll
C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Unit\AdvancedWarehouseStoreTests.cs(16,55): error CS0023: Operator '.' cannot be applied to operand of type 'void' [C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj]
  LKvitai.MES.Tests.Integration -> C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Integration\bin\Debug\net8.0\LKvitai.MES.Tests.Integration.dll

Build FAILED.

C:\tmp\wt_prev\src\LKvitai.MES.Contracts\Messages\PickStockMessages.cs(5,1): warning CS1587: XML comment is not placed on a valid language element [C:\tmp\wt_prev\src\LKvitai.MES.Contracts\LKvitai.MES.Contracts.csproj]
C:\tmp\wt_prev\src\LKvitai.MES.Integration\LabelPrinting\ILabelPrintingService.cs(7,31): warning CS1570: XML comment has badly formed XML -- 'An identifier was expected.' [C:\tmp\wt_prev\src\LKvitai.MES.Integration\LKvitai.MES.Integration.csproj]
C:\tmp\wt_prev\src\LKvitai.MES.Integration\LabelPrinting\ILabelPrintingService.cs(7,31): warning CS1570: XML comment has badly formed XML -- 'The character(s) '5' cannot be used at this location.' [C:\tmp\wt_prev\src\LKvitai.MES.Integration\LKvitai.MES.Integration.csproj]
C:\tmp\wt_prev\src\LKvitai.MES.Application\Projections\IProjectionRebuildService.cs(26,14): warning CS1573: Parameter 'resetProgress' has no matching param tag in the XML comment for 'IProjectionRebuildService.RebuildProjectionAsync(string, bool, bool, CancellationToken)' (but other parameters do) [C:\tmp\wt_prev\src\LKvitai.MES.Application\LKvitai.MES.Application.csproj]
C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Unit\AdvancedWarehouseStoreTests.cs(16,55): error CS0023: Operator '.' cannot be applied to operand of type 'void' [C:\tmp\wt_prev\src\tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj]
    4 Warning(s)
    1 Error(s)

Time Elapsed 00:00:27.52
Preparing worktree (detached HEAD c9d637c)
```

## 6) Recommendation (factual, no changes)
- Revert recommended: NO (for build-break remediation of last 1–3 commits)
- Evidence: same `CS0023` compile failure occurs at `HEAD`, `HEAD~1`, and additionally verified at `HEAD~2` and `HEAD~3`.
- Commits to revert: none for this specific compiler error.
- Optional note: if commits are not pushed and architecture direction is being rolled back for policy reasons, `git reset --hard 0895845` is a workflow option, but it is not indicated by the build-failure evidence.

## 7) Required note
- Do NOT implement GapWorkbench god-page approach; we will replace with proper per-workflow UI or mark intentional-no-UI endpoints.