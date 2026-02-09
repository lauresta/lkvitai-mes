# Implementation Plan and Testing Strategy

## Phase 1 Implementation Timeline

**Total Duration**: 7-8 weeks

**Team Assumptions**:
- 1 Backend Developer (C# / .NET)
- 1 Frontend Developer (Blazor)
- 1 QA Engineer (Test automation)
- Part-time: DevOps (CI/CD setup)

---

## Week-by-Week Plan

### Week 1-2: Foundation & Master Data (EF Core)

**Backend Tasks**:
1. Create EF Core DbContext for master data
2. Implement entity models (Item, Supplier, Location, etc.)
3. Configure relationships, constraints, indexes
4. Create initial migration
5. Write seed data SQL script (UoM, virtual locations, reason codes)
6. Apply migration to dev database
7. Implement repository pattern (optional, or use DbContext directly)
8. Write unit tests for entity validation

**Frontend Tasks**:
1. Set up Blazor project structure (Pages, Components, Services)
2. Create layout template (navigation menu, header, footer)
3. Implement pagination component (reuse existing)
4. Implement filter bar component (reuse existing)
5. Create empty state component

**Deliverables**:
- EF Core model complete, migration applied
- Seed data loaded in dev database
- Blazor project structure ready
- 50+ unit tests for entity models

**Validation Checklist**:
- [ ] All entities have correct PK/FK constraints
- [ ] Unique indexes on SKU, Barcode, Code fields
- [ ] Seed data script runs without errors
- [ ] DbContext SaveChanges propagates audit fields (CreatedBy, UpdatedAt)

---

### Week 3: Import APIs & Validation

**Backend Tasks**:
1. Implement Excel parsing service (EPPlus or ClosedXML)
2. Create import endpoints (Items, Suppliers, Locations, etc.)
3. Implement validation logic:
   - Header validation (column names match template)
   - Row validation (data types, required fields)
   - FK validation (CategoryId, BaseUoM exist)
   - Uniqueness validation (SKU, Barcode)
4. Implement upsert logic (insert new, update existing)
5. Implement dry-run mode (validation only, no DB writes)
6. Generate Excel templates programmatically
7. Write integration tests (import 100-row file)

**Frontend Tasks**:
1. Implement Import Wizard UI (tabbed interface)
2. File upload component (drag & drop)
3. Validation results display (errors table)
4. Download template button
5. Progress indicator during import

**Deliverables**:
- Import API endpoints functional
- Excel template generator
- Import wizard UI complete
- 30+ integration tests for import validation

**Validation Checklist**:
- [ ] Import 500-row file completes in <5 minutes
- [ ] Duplicate SKUs detected and reported
- [ ] FK validation catches invalid CategoryCode
- [ ] Dry-run mode does not write to DB
- [ ] Error report downloaded as Excel

---

### Week 4: Projections & Event Store

**Backend Tasks**:
1. **CRITICAL**: Fix projection rebuild issue (shadow table error)
   - Root cause analysis (see ops runbook)
   - Test projection rebuild on clean database
   - Document rebuild procedure
2. Implement Marten event store configuration
3. Create event contracts (GoodsReceived, StockMoved, etc.)
4. Implement AvailableStock projection
5. Implement LocationBalance projection
6. Implement ActiveReservations projection
7. Implement InboundShipmentSummary projection
8. Add projection health check endpoint
9. Write projection tests (apply events, verify state)

**Frontend Tasks**:
1. Implement stock visibility pages (Available Stock, Reservations)
2. Display projection timestamp ("Stock as of HH:MM:SS")
3. Refresh button (poll projection API)
4. Staleness warning banner (if lag > 10 seconds)

**Deliverables**:
- Projection rebuild issue resolved
- 4 core projections implemented
- Projection health check API
- 40+ projection tests

**Validation Checklist**:
- [ ] Projection rebuild completes without errors
- [ ] AvailableStock projection updates in <1 second
- [ ] Projection lag health check works
- [ ] UI shows projection timestamp
- [ ] Staleness warning triggers at 10 seconds

---

### Week 5: Receiving & Putaway Workflows

**Backend Tasks**:
1. Implement InboundShipment CRUD APIs
2. Implement GoodsReceived event handler
3. Implement QC pass/fail APIs (emit StockMoved events)
4. Implement Putaway API (emit StockMoved event)
5. Add barcode lookup API
6. Write workflow tests (receive → QC → putaway)

**Frontend Tasks**:
1. Implement Inbound Shipments list page
2. Implement Shipment detail & Receive Goods modal
3. Implement QC panel (pass/fail actions)
4. Implement Putaway tasks page
5. Barcode scanner component (auto-submit on Enter)
6. Capacity warning display (location utilization)

**Deliverables**:
- Receiving workflow complete (shipment → receive → QC → putaway)
- Barcode scanning functional
- 25+ workflow tests

**Validation Checklist**:
- [ ] Receive goods creates GoodsReceived event
- [ ] QC pass moves stock from QC_HOLD to RECEIVING
- [ ] Putaway moves stock to storage location
- [ ] Barcode scan validates item
- [ ] Lot tracking enforced for RequiresLotTracking items

---

### Week 6: Picking & Adjustments

**Backend Tasks**:
1. Implement PickTask CRUD APIs
2. Implement PickCompleted event handler
3. Implement Stock Adjustment API
4. Implement StockAdjusted event handler
5. Implement Adjustment History API
6. Write picking workflow tests

**Frontend Tasks**:
1. Implement Pick Tasks list page
2. Implement Pick Execution page (location selection, scan, confirm)
3. Implement Adjustments page (create adjustment, history)
4. Confirmation dialogs for destructive actions

**Deliverables**:
- Picking workflow complete (task → execute → complete)
- Adjustments workflow complete (create → confirm → history)
- 20+ workflow tests

**Validation Checklist**:
- [ ] Pick task creates PickCompleted event
- [ ] Stock adjustment emits StockAdjusted event
- [ ] Adjustment requires reason code
- [ ] Pick execution validates barcode
- [ ] Partial picks update task status

---

### Week 7: UI Polish & Testing

**Backend Tasks**:
1. Performance optimization (query tuning, indexing)
2. Add API rate limiting (import endpoints)
3. Implement distributed tracing (OpenTelemetry)
4. Error handling improvements (ProblemDetails)
5. API documentation (Swagger/OpenAPI)

**Frontend Tasks**:
1. Error banner component with traceId
2. Toast notification component
3. Loading states for all actions
4. Empty states for all lists
5. CSV export for all reports
6. Mobile responsiveness (tablet)

**Deliverables**:
- All UI pages polished
- Error handling consistent (traceId in all errors)
- Performance targets met
- 15+ UI component tests

**Validation Checklist**:
- [ ] All buttons show loading state during API calls
- [ ] All errors display traceId
- [ ] CSV export works for 10k rows in <3 seconds
- [ ] Page load < 2 seconds
- [ ] Tablet viewport (768px) works

---

### Week 8: End-to-End Testing & Deployment

**Backend Tasks**:
1. Deploy to staging environment
2. Run database migrations on staging
3. Import production-like data (500 items)
4. Load testing (1000 concurrent requests)
5. Security audit (OWASP Top 10)

**Frontend Tasks**:
1. UAT with 2-3 warehouse operators
2. Bug fixes from UAT feedback
3. User training materials (screenshots + steps)
4. Deployment checklist

**QA Tasks**:
1. End-to-end test suite (Playwright/Selenium)
2. Regression test suite (rerun after bug fixes)
3. Performance test suite (JMeter/k6)
4. Smoke test suite (critical paths only)

**Deliverables**:
- Staging deployment successful
- UAT sign-off
- Production deployment plan
- User training materials

**Validation Checklist**:
- [ ] Import 500 items in <5 minutes (staging)
- [ ] Receive 10-item shipment in <15 minutes (UAT)
- [ ] Complete 5-item pick in <5 minutes (UAT)
- [ ] Projection lag < 1 second (load test)
- [ ] Zero critical bugs from UAT

---

## Testing Strategy

### Unit Tests (Target: 200+ tests)

**Tools**: xUnit, FluentAssertions, Moq

**Coverage**:
- Entity model validation (required fields, constraints)
- UoM conversion logic (rounding rules)
- InternalSKU generation logic
- Barcode validation logic
- Projection event handlers (apply events, verify state changes)

**Example Test**:
```csharp
[Fact]
public void ItemUoMConversion_RoundingUp_Should_CeilQty()
{
    // Arrange
    var conversion = new ItemUoMConversion
    {
        FromUoM = "BOX",
        ToUoM = "PCS",
        Factor = 12,
        RoundingRule = RoundingRule.Up
    };
    
    // Act
    var result = conversion.Convert(1.3m); // 1.3 boxes
    
    // Assert
    result.Should().Be(16); // 16 pieces (rounded up from 15.6)
}
```

**Docker-Gated Pattern**:
- Tests that require database run only if Docker is available
- Use Testcontainers to spin up Postgres for integration tests
- Skip tests if Docker not running (local dev without Docker)

---

### Integration Tests (Target: 100+ tests)

**Tools**: xUnit, Testcontainers, FluentAssertions

**Coverage**:
- EF Core CRUD operations (insert, update, delete)
- Import API (upload Excel, validate, insert/update)
- Event store append (optimistic concurrency)
- Projection rebuild (from empty to full state)
- API endpoints (full request/response cycle)

**Example Test**:
```csharp
[Fact]
public async Task ImportItems_ValidFile_Should_InsertRecords()
{
    // Arrange
    await using var container = new PostgresContainer();
    await container.StartAsync();
    
    var dbContext = CreateDbContext(container.ConnectionString);
    var importService = new ItemImportService(dbContext);
    
    var file = CreateExcelFile(new[]
    {
        new { InternalSKU = "RM-0001", Name = "Bolt", CategoryCode = "FASTENERS", BaseUoM = "PCS" }
    });
    
    // Act
    var result = await importService.ImportAsync(file, dryRun: false);
    
    // Assert
    result.InsertedRows.Should().Be(1);
    dbContext.Items.Should().ContainSingle(x => x.InternalSKU == "RM-0001");
}
```

**Testcontainers Setup**:
```csharp
public class PostgresContainer : IAsyncLifetime
{
    private readonly DotNet.Testcontainers.Containers.Container _container;
    
    public PostgresContainer()
    {
        _container = new ContainerBuilder()
            .WithImage("postgres:16")
            .WithPortBinding(5432, true)
            .WithEnvironment("POSTGRES_PASSWORD", "test")
            .Build();
    }
    
    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();
    
    public string ConnectionString => 
        $"Host=localhost;Port={_container.GetMappedPublicPort(5432)};Database=test;Username=postgres;Password=test";
}
```

---

### Projection Tests (Target: 50+ tests)

**Tools**: xUnit, Marten, FluentAssertions

**Coverage**:
- Event handler logic (apply event, verify projection state)
- Multi-event scenarios (receive → putaway → pick → adjust)
- Projection rebuild (apply 1000 events, verify final state)
- Projection lag (measure update time)

**Example Test**:
```csharp
[Fact]
public async Task AvailableStock_GoodsReceived_Should_AddQty()
{
    // Arrange
    var store = CreateEventStore();
    var projection = new AvailableStockProjection();
    
    var evt = new GoodsReceived
    {
        ItemId = 1,
        DestinationLocationId = 5,
        LotId = Guid.NewGuid(),
        ReceivedQty = 1000
    };
    
    // Act
    await store.Events.Append(Guid.NewGuid(), evt);
    await store.SaveChangesAsync();
    
    // Assert
    var stock = await store.Query<AvailableStock>()
        .FirstAsync(x => x.ItemId == 1 && x.LocationId == 5);
    
    stock.Qty.Should().Be(1000);
}
```

---

### Workflow Tests (Target: 30+ tests)

**Tools**: xUnit, Marten, FluentAssertions

**Coverage**:
- End-to-end workflows (receive → putaway → pick → ship)
- Error scenarios (insufficient stock, barcode mismatch)
- Saga compensation (reservation failed → release stock)

**Example Test**:
```csharp
[Fact]
public async Task ReceiveAndPutaway_Workflow_Should_UpdateProjection()
{
    // Arrange
    var shipmentId = Guid.NewGuid();
    var itemId = 1;
    var lotId = Guid.NewGuid();
    
    // Act: Receive goods
    var receiveCmd = new ReceiveGoodsCommand
    {
        ShipmentId = shipmentId,
        ItemId = itemId,
        ReceivedQty = 1000,
        LotNumber = "LOT-001"
    };
    await _receivingService.ReceiveAsync(receiveCmd);
    
    // Assert: Stock in RECEIVING location
    var receivingStock = await GetAvailableStock(itemId, Locations.RECEIVING);
    receivingStock.Qty.Should().Be(1000);
    
    // Act: Putaway to storage
    var putawayCmd = new PutawayCommand
    {
        ItemId = itemId,
        Qty = 1000,
        FromLocationId = Locations.RECEIVING,
        ToLocationId = 15, // Storage
        LotId = lotId
    };
    await _putawayService.PutawayAsync(putawayCmd);
    
    // Assert: Stock moved to storage
    var storageStock = await GetAvailableStock(itemId, 15);
    storageStock.Qty.Should().Be(1000);
    
    var receivingStockAfter = await GetAvailableStock(itemId, Locations.RECEIVING);
    receivingStockAfter.Qty.Should().Be(0);
}
```

---

### UI Tests (Target: 20+ tests)

**Tools**: bUnit (Blazor component testing)

**Coverage**:
- Component rendering (pagination, filter bar, barcode scanner)
- User interactions (button clicks, form submissions)
- Validation (field errors, form errors)
- State management (loading, success, error states)

**Example Test**:
```csharp
[Fact]
public void BarcodeScanner_OnScan_Should_CallOnScanCallback()
{
    // Arrange
    var onScanCalled = false;
    var scannedValue = "";
    
    var component = RenderComponent<BarcodeScanner>(parameters => parameters
        .Add(p => p.OnScan, (value) =>
        {
            onScanCalled = true;
            scannedValue = value;
        })
    );
    
    // Act
    var input = component.Find("input");
    input.Change("8594156780187");
    input.KeyPress(Key.Enter);
    
    // Assert
    onScanCalled.Should().BeTrue();
    scannedValue.Should().Be("8594156780187");
}
```

---

### End-to-End Tests (Target: 15+ tests)

**Tools**: Playwright (C# bindings)

**Coverage**:
- Critical user paths (import → receive → putaway → pick)
- Browser compatibility (Chrome, Edge)
- Mobile responsiveness (tablet viewport)

**Example Test**:
```csharp
[Fact]
public async Task E2E_ImportAndReceive_Should_UpdateStock()
{
    // Arrange
    var page = await Browser.NewPageAsync();
    await page.GotoAsync("http://localhost:5000/admin/import");
    
    // Act: Upload file
    await page.Locator("input[type='file']").SetInputFilesAsync("Items.xlsx");
    await page.ClickAsync("button:has-text('Import')");
    
    // Assert: Import success
    await page.WaitForSelectorAsync("text=Inserted: 500");
    
    // Act: Navigate to receiving
    await page.ClickAsync("a:has-text('Receiving')");
    await page.ClickAsync("button:has-text('Create Shipment')");
    
    // ... rest of test
}
```

---

### Performance Tests (Target: 10+ tests)

**Tools**: k6 (load testing), BenchmarkDotNet (micro-benchmarks)

**Coverage**:
- Import API throughput (concurrent uploads)
- Projection update latency (event append → projection update)
- API response time (p50, p95, p99)
- Database query performance (explain plans)

**Example Test** (k6):
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 50 },  // Ramp up to 50 users
    { duration: '1m', target: 50 },   // Stay at 50 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
};

export default function () {
  let res = http.get('http://localhost:5000/api/warehouse/v1/stock/available');
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  sleep(1);
}
```

---

## Test Organization

### Folder Structure

```
tests/
├── LKvitai.MES.Warehouse.UnitTests/
│   ├── Entities/
│   │   ├── ItemTests.cs
│   │   ├── ItemUoMConversionTests.cs
│   ├── Services/
│   │   ├── ItemImportServiceTests.cs
│   ├── Projections/
│   │   ├── AvailableStockProjectionTests.cs
│
├── LKvitai.MES.Warehouse.IntegrationTests/
│   ├── Api/
│   │   ├── ItemsControllerTests.cs
│   │   ├── ImportControllerTests.cs
│   ├── Workflows/
│   │   ├── ReceivingWorkflowTests.cs
│   ├── Infrastructure/
│   │   ├── TestcontainersFixture.cs
│
├── LKvitai.MES.Warehouse.E2ETests/
│   ├── ImportAndReceiveTests.cs
│   ├── PutawayAndPickTests.cs
│
└── LKvitai.MES.Warehouse.PerformanceTests/
    ├── k6/
    │   ├── import_load_test.js
    ├── Benchmarks/
    │   ├── ProjectionBenchmark.cs
```

---

## CI/CD Pipeline

### GitHub Actions Workflow

```yaml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run unit tests
      run: dotnet test tests/LKvitai.MES.Warehouse.UnitTests --no-build --verbosity normal
    
    - name: Run integration tests
      run: dotnet test tests/LKvitai.MES.Warehouse.IntegrationTests --no-build --verbosity normal
      env:
        ConnectionStrings__Warehouse: "Host=localhost;Port=5432;Database=test;Username=postgres;Password=test"
    
    - name: Code coverage
      run: dotnet test --collect:"XPlat Code Coverage"
    
    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v3
```

---

## Docker-Gated Tests Pattern

### Implementation

```csharp
public class DockerGatedFactAttribute : FactAttribute
{
    public DockerGatedFactAttribute()
    {
        if (!DockerHelper.IsDockerAvailable())
        {
            Skip = "Docker is not available. Test skipped.";
        }
    }
}

public static class DockerHelper
{
    public static bool IsDockerAvailable()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
```

### Usage

```csharp
[DockerGatedFact]
public async Task ImportItems_ShouldInsertRecords_IntegrationTest()
{
    // This test runs only if Docker is available
    await using var container = new PostgresContainer();
    await container.StartAsync();
    
    // ... test logic
}
```

---

## Performance Considerations

### Import Optimization

**Batch Insert** (EF Core):
```csharp
public async Task<ImportResult> ImportAsync(Stream file, bool dryRun)
{
    var items = ParseExcel(file);
    
    // Validate all rows first
    var errors = ValidateAll(items);
    if (errors.Any() && !dryRun)
        return new ImportResult { Errors = errors };
    
    // Batch insert (use EF Extensions for >1000 rows)
    if (items.Count > 1000)
    {
        await _dbContext.BulkInsertAsync(items);
    }
    else
    {
        _dbContext.Items.AddRange(items);
        await _dbContext.SaveChangesAsync();
    }
    
    return new ImportResult { InsertedRows = items.Count };
}
```

### Query Optimization

**Index Strategy**:
- All FK columns indexed
- Composite indexes for common filter combinations
- Covering indexes for read-heavy queries

**Example**:
```sql
-- Composite index for stock queries filtered by item + location
CREATE INDEX ix_availablestock_item_location 
ON mt_doc_availablestock(item_id, location_id) 
INCLUDE (qty, reserved_qty, lot_id);
```

### Projection Performance

**Parallel Processing**:
```csharp
// Marten async projections (background daemon)
services.AddMarten(options =>
{
    options.Connection(connectionString);
    
    options.Projections.Add<AvailableStockProjection>(
        ProjectionLifecycle.Async,  // Process in background
        asyncConfiguration: async =>
        {
            async.Workers = 4;  // 4 parallel workers
        });
});
```

---

## Test Data Management

### Seed Data for Tests

**Shared Test Data** (in-memory):
```csharp
public static class TestData
{
    public static Item CreateItem(int id = 1, string sku = "RM-0001")
    {
        return new Item
        {
            Id = id,
            InternalSKU = sku,
            Name = "Test Item",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = ItemStatus.Active
        };
    }
    
    public static Location CreateLocation(int id = 1, string code = "WH01-A")
    {
        return new Location
        {
            Id = id,
            Code = code,
            Barcode = $"QR:{code}",
            Type = LocationType.Bin,
            Status = LocationStatus.Active
        };
    }
}
```

### Realistic Data Generation

**Use Bogus library** for large datasets:
```csharp
var itemFaker = new Faker<Item>()
    .RuleFor(i => i.InternalSKU, f => $"RM-{f.Random.Number(1, 9999):D4}")
    .RuleFor(i => i.Name, f => f.Commerce.ProductName())
    .RuleFor(i => i.Weight, f => f.Random.Decimal(0.01m, 10m));

var items = itemFaker.Generate(500);
```

---

## Monitoring & Observability

### Application Insights (or equivalent)

**Track**:
- API request duration (p50, p95, p99)
- Event append rate (events/second)
- Projection lag (seconds)
- Import throughput (rows/second)
- Error rate (errors/total requests)

**Alerts**:
- Projection lag > 10 seconds (warning)
- API error rate > 5% (critical)
- Import API > 10 minutes (critical)

---

## Summary

Phase 1 testing strategy includes **415+ tests** across 6 categories (unit, integration, projection, workflow, UI, E2E). All integration tests use **Testcontainers** for isolated Postgres instances. CI/CD pipeline runs tests on every PR, with code coverage tracked via Codecov. Performance targets: import 500 items <5 min, projection lag <1 sec, API response <500ms.
