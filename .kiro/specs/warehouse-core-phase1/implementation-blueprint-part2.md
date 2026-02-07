# Implementation Blueprint Part 2: Warehouse Core Phase 1

**Continuation of:** `implementation-blueprint.md`

---

## 6. Projection Runtime Architecture

### 6.1 Marten Async Daemon Configuration

**Setup:**

```csharp
services.AddMarten(options =>
{
    // ... other configuration
    
    // Enable async daemon for projections
    options.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
    options.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
    options.Projections.Add<OnHandValueProjection>(ProjectionLifecycle.Async);
    
    // Configure daemon
    options.Projections.AsyncMode = DaemonMode.HotCold;
    options.Projections.StaleSequenceThreshold = TimeSpan.FromSeconds(30);
});

// Start daemon
services.AddHostedService<MartenDaemonHost>();
```

---

### 6.2 LocationBalance Projection

**Implementation:**

```csharp
public class LocationBalanceProjection : MultiStreamProjection<LocationBalanceView, Guid>
{
    public LocationBalanceProjection()
    {
        // Subscribe to StockMoved events
        Identity<StockMovedEvent>(e => CombinedGuid(e.ToLocation, e.SKU));
        
        // Projection logic
        CustomGrouping(new LocationBalanceGrouper());
    }
}

public class LocationBalanceGrouper : IAggregationRuntime<Guid, LocationBalanceView>
{
    public void Apply(IEvent<StockMovedEvent> @event, LocationBalanceView aggregate)
    {
        var evt = @event.Data;
        
        // Update balance for TO location
        if (aggregate.Location == evt.ToLocation && aggregate.SKU == evt.SKU)
        {
            aggregate.Quantity += evt.Quantity;
            aggregate.LastUpdated = evt.Timestamp;
        }
        
        // Update balance for FROM location
        if (aggregate.Location == evt.FromLocation && aggregate.SKU == evt.SKU)
        {
            aggregate.Quantity -= evt.Quantity;
            aggregate.LastUpdated = evt.Timestamp;
        }
    }
}

public class LocationBalanceView
{
    public Guid Id { get; set; }
    public string Location { get; set; }
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

---

### 6.3 Projection Lag Monitoring

**Implementation:**

```csharp
public class ProjectionLagMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectionLagMonitor> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckProjectionLag(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking projection lag");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    
    private async Task CheckProjectionLag(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        
        // Get latest event timestamp
        var latestEvent = await session.Events.QueryAllRawEvents()
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(ct);
        
        if (latestEvent == null)
            return;
        
        // Check LocationBalance projection lag
        var locationBalanceLag = await session.Query<LocationBalanceView>()
            .OrderByDescending(v => v.LastUpdated)
            .Select(v => v.LastUpdated)
            .FirstOrDefaultAsync(ct);
        
        var lag = latestEvent.Timestamp - locationBalanceLag;
        
        if (lag > TimeSpan.FromSeconds(30))
        {
            _logger.LogWarning(
                "LocationBalance projection lag exceeded threshold: {Lag} seconds",
                lag.TotalSeconds);
            
            // Trigger alert
            await TriggerAlert("LocationBalance projection lag", lag);
        }
    }
    
    private async Task TriggerAlert(string message, TimeSpan lag)
    {
        // Send alert to monitoring system (e.g., PagerDuty, Slack)
        // Implementation depends on alerting infrastructure
    }
}
```

---

## 7. Event Schema Versioning

### 7.1 Event Versioning Strategy

**Event Base Class:**

```csharp
public abstract class DomainEvent
{
    public int Version { get; set; } = 1;
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

**Versioned Event Example:**

```csharp
// Version 1
public class StockMovedEvent_V1 : DomainEvent
{
    public Guid MovementId { get; set; }
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public MovementType MovementType { get; set; }
    public Guid OperatorId { get; set; }
}

// Version 2 (added HandlingUnitId)
public class StockMovedEvent_V2 : DomainEvent
{
    public Guid MovementId { get; set; }
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public MovementType MovementType { get; set; }
    public Guid OperatorId { get; set; }
    public Guid? HandlingUnitId { get; set; }  // NEW FIELD
    public string Reason { get; set; }  // NEW FIELD
}

// Current version (alias)
public class StockMovedEvent : StockMovedEvent_V2 { }
```

---

### 7.2 Upcasting Implementation

**Upcaster Interface:**

```csharp
public interface IEventUpcaster<TFrom, TTo>
{
    TTo Upcast(TFrom oldEvent);
}
```

**Upcaster Implementation:**

```csharp
public class StockMovedEventUpcaster : IEventUpcaster<StockMovedEvent_V1, StockMovedEvent_V2>
{
    public StockMovedEvent_V2 Upcast(StockMovedEvent_V1 oldEvent)
    {
        return new StockMovedEvent_V2
        {
            Version = 2,
            EventId = oldEvent.EventId,
            Timestamp = oldEvent.Timestamp,
            MovementId = oldEvent.MovementId,
            SKU = oldEvent.SKU,
            Quantity = oldEvent.Quantity,
            FromLocation = oldEvent.FromLocation,
            ToLocation = oldEvent.ToLocation,
            MovementType = oldEvent.MovementType,
            OperatorId = oldEvent.OperatorId,
            HandlingUnitId = null,  // Default for old events
            Reason = null  // Default for old events
        };
    }
}
```

**Marten Upcasting Configuration:**

```csharp
services.AddMarten(options =>
{
    // Register upcasters
    options.Events.Upcast<StockMovedEvent_V1, StockMovedEvent_V2>(
        evt => new StockMovedEventUpcaster().Upcast(evt));
});
```

---

## 8. Offline Sync Protocol

### 8.1 Edge Agent Queue Schema

**SQLite Schema (Edge Agent):**

```sql
CREATE TABLE offline_command_queue (
    queue_id INTEGER PRIMARY KEY AUTOINCREMENT,
    command_id TEXT UNIQUE NOT NULL,
    timestamp DATETIME NOT NULL,
    command_type TEXT NOT NULL,
    payload TEXT NOT NULL,  -- JSON
    status TEXT NOT NULL,   -- QUEUED, SYNCING, SYNCED, FAILED
    retry_count INTEGER DEFAULT 0,
    last_error TEXT,
    created_at DATETIME NOT NULL
);

CREATE INDEX idx_status ON offline_command_queue(status);
CREATE INDEX idx_timestamp ON offline_command_queue(timestamp);
```

---

### 8.2 Offline Command Queueing

**Edge Agent Implementation:**

```csharp
public class OfflineCommandQueue
{
    private readonly SQLiteConnection _connection;
    private readonly ILogger<OfflineCommandQueue> _logger;
    
    public async Task<Result> QueueCommand(ICommand command)
    {
        // Check whitelist
        if (!IsAllowedOffline(command))
        {
            return Result.Fail($"{command.GetType().Name} is not allowed offline");
        }
        
        // Check queue size
        var queueSize = await GetQueueSize();
        if (queueSize >= 100)
        {
            return Result.Fail("Queue full. Please sync before continuing.");
        }
        
        // Insert into queue
        var sql = @"
            INSERT INTO offline_command_queue 
            (command_id, timestamp, command_type, payload, status, created_at)
            VALUES (@CommandId, @Timestamp, @CommandType, @Payload, 'QUEUED', @CreatedAt)";
        
        await _connection.ExecuteAsync(sql, new
        {
            CommandId = command.CommandId.ToString(),
            Timestamp = DateTime.UtcNow,
            CommandType = command.GetType().Name,
            Payload = JsonSerializer.Serialize(command),
            CreatedAt = DateTime.UtcNow
        });
        
        _logger.LogInformation("Command {CommandId} queued offline", command.CommandId);
        
        return Result.Ok();
    }
    
    private bool IsAllowedOffline(ICommand command)
    {
        var allowedTypes = new[]
        {
            nameof(PickStockCommand),
            nameof(TransferStockCommand)
        };
        
        return allowedTypes.Contains(command.GetType().Name);
    }
    
    private async Task<int> GetQueueSize()
    {
        var sql = "SELECT COUNT(*) FROM offline_command_queue WHERE status = 'QUEUED'";
        return await _connection.ExecuteScalarAsync<int>(sql);
    }
}
```

---

### 8.3 Sync Engine

**Implementation:**

```csharp
public class SyncEngine
{
    private readonly SQLiteConnection _localConnection;
    private readonly IWarehouseApiClient _apiClient;
    private readonly ILogger<SyncEngine> _logger;
    
    public async Task<SyncResult> SyncQueuedCommands()
    {
        var result = new SyncResult();
        
        // Fetch queued commands
        var commands = await _localConnection.QueryAsync<QueuedCommand>(
            "SELECT * FROM offline_command_queue WHERE status = 'QUEUED' ORDER BY timestamp");
        
        foreach (var cmd in commands)
        {
            try
            {
                // Mark as syncing
                await UpdateCommandStatus(cmd.QueueId, "SYNCING");
                
                // Send to server
                var response = await _apiClient.SendCommand(cmd.CommandType, cmd.Payload);
                
                if (response.IsSuccess)
                {
                    // Mark as synced
                    await UpdateCommandStatus(cmd.QueueId, "SYNCED");
                    result.SuccessfulCommands.Add(cmd);
                    
                    _logger.LogInformation("Command {CommandId} synced successfully", cmd.CommandId);
                }
                else
                {
                    // Mark as failed
                    await UpdateCommandStatus(cmd.QueueId, "FAILED", response.Error);
                    result.FailedCommands.Add(new FailedCommand
                    {
                        Command = cmd,
                        Error = response.Error,
                        SuggestedAction = GetSuggestedAction(response.Error)
                    });
                    
                    _logger.LogWarning("Command {CommandId} failed: {Error}", cmd.CommandId, response.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing command {CommandId}", cmd.CommandId);
                
                await UpdateCommandStatus(cmd.QueueId, "FAILED", ex.Message);
                result.FailedCommands.Add(new FailedCommand
                {
                    Command = cmd,
                    Error = ex.Message,
                    SuggestedAction = "Retry sync or contact support"
                });
            }
        }
        
        return result;
    }
    
    private async Task UpdateCommandStatus(int queueId, string status, string error = null)
    {
        var sql = @"
            UPDATE offline_command_queue 
            SET status = @Status, last_error = @Error, retry_count = retry_count + 1
            WHERE queue_id = @QueueId";
        
        await _localConnection.ExecuteAsync(sql, new { QueueId = queueId, Status = status, Error = error });
    }
    
    private string GetSuggestedAction(string error)
    {
        if (error.Contains("Reservation cancelled"))
            return "Return stock to shelf or contact manager";
        
        if (error.Contains("HU moved"))
            return "Verify current HU location and retry";
        
        if (error.Contains("Insufficient quantity"))
            return "Pick available quantity or find alternative stock";
        
        return "Contact supervisor for guidance";
    }
}

public class SyncResult
{
    public List<QueuedCommand> SuccessfulCommands { get; set; } = new();
    public List<FailedCommand> FailedCommands { get; set; } = new();
}

public class FailedCommand
{
    public QueuedCommand Command { get; set; }
    public string Error { get; set; }
    public string SuggestedAction { get; set; }
}
```

---

### 8.4 Server-Side Validation

**API Endpoint:**

```csharp
[ApiController]
[Route("api/commands")]
public class CommandController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CommandController> _logger;
    
    [HttpPost("sync")]
    public async Task<IActionResult> SyncCommand([FromBody] SyncCommandRequest request)
    {
        _logger.LogInformation("Received sync command {CommandId} of type {CommandType}", 
            request.CommandId, request.CommandType);
        
        // Deserialize command
        var commandType = Type.GetType(request.CommandType);
        var command = JsonSerializer.Deserialize(request.Payload, commandType) as ICommand;
        
        // Re-validate preconditions
        var validationResult = await ValidateOfflineCommand(command);
        if (!validationResult.IsSuccess)
        {
            _logger.LogWarning("Offline command {CommandId} validation failed: {Error}", 
                command.CommandId, validationResult.Error);
            
            return BadRequest(new { error = validationResult.Error });
        }
        
        // Execute command
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing synced command {CommandId}", command.CommandId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    private async Task<Result> ValidateOfflineCommand(ICommand command)
    {
        return command switch
        {
            PickStockCommand pickCmd => await ValidatePickStock(pickCmd),
            TransferStockCommand transferCmd => await ValidateTransferStock(transferCmd),
            _ => Result.Fail("Command type not allowed offline")
        };
    }
    
    private async Task<Result> ValidatePickStock(PickStockCommand command)
    {
        // Check reservation still PICKING
        var reservation = await _mediator.Send(new GetReservationQuery { ReservationId = command.ReservationId });
        if (reservation.Status != ReservationStatus.PICKING)
            return Result.Fail($"Reservation {command.ReservationId} is no longer in PICKING state");
        
        // Check HU still allocated
        var line = reservation.Lines.FirstOrDefault(l => l.SKU == command.SKU);
        if (line == null || !line.AllocatedHUs.Contains(command.HandlingUnitId))
            return Result.Fail($"HU {command.HandlingUnitId} is no longer allocated to reservation");
        
        // Check balance sufficient
        var balance = await _mediator.Send(new GetBalanceQuery 
        { 
            Location = command.FromLocation, 
            SKU = command.SKU 
        });
        
        if (balance < command.Quantity)
            return Result.Fail($"Insufficient balance at {command.FromLocation}");
        
        return Result.Ok();
    }
    
    private async Task<Result> ValidateTransferStock(TransferStockCommand command)
    {
        // Check HU not moved by another operator
        var hu = await _mediator.Send(new GetHandlingUnitQuery { HUId = command.HandlingUnitId });
        if (hu.Location != command.FromLocation)
            return Result.Fail($"HU {command.HandlingUnitId} already moved to {hu.Location}");
        
        // Check destination location exists
        var locationExists = await _mediator.Send(new CheckLocationExistsQuery 
        { 
            Location = command.ToLocation 
        });
        
        if (!locationExists)
            return Result.Fail($"Destination location {command.ToLocation} does not exist");
        
        return Result.Ok();
    }
}
```

---

## 9. Integration Adapter Contracts

### 9.1 Label Printing Adapter

**Interface:**

```csharp
public interface ILabelPrintingService
{
    Task<Result> PrintLabel(Guid handlingUnitId, CancellationToken ct = default);
    Task<Result> ReprintLabel(Guid handlingUnitId, CancellationToken ct = default);
}
```

**Implementation:**

```csharp
public class ZebraLabelPrintingService : ILabelPrintingService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ZebraLabelPrintingService> _logger;
    
    public async Task<Result> PrintLabel(Guid handlingUnitId, CancellationToken ct)
    {
        try
        {
            // Fetch HU details
            var hu = await FetchHandlingUnit(handlingUnitId);
            
            // Generate ZPL
            var zpl = GenerateZPL(hu);
            
            // Send to printer
            var printerIp = _configuration["LabelPrinting:PrinterIP"];
            var printerPort = int.Parse(_configuration["LabelPrinting:PrinterPort"]);
            
            using var client = new TcpClient();
            await client.ConnectAsync(printerIp, printerPort, ct);
            
            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(zpl);
            await stream.WriteAsync(bytes, 0, bytes.Length, ct);
            
            _logger.LogInformation("Label printed for HU {HUId}", handlingUnitId);
            
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print label for HU {HUId}", handlingUnitId);
            return Result.Fail(ex.Message);
        }
    }
    
    private string GenerateZPL(HandlingUnit hu)
    {
        return $@"
^XA
^FO50,50^A0N,50,50^FD{hu.LPN}^FS
^FO50,120^A0N,30,30^FDLocation: {hu.Location}^FS
^FO50,160^A0N,25,25^FDCreated: {hu.CreatedAt:yyyy-MM-dd HH:mm}^FS
^FO50,200^BY3^BCN,100,Y,N,N^FD{hu.LPN}^FS
^XZ";
    }
}
```

---

### 9.2 Agnum Export Adapter

**Interface:**

```csharp
public interface IAgnumExportService
{
    Task<Result> ExportSnapshot(ExportMode mode, CancellationToken ct = default);
}

public enum ExportMode
{
    ByPhysicalWarehouse,
    ByLogicalWarehouse,
    ByCategory,
    TotalSum
}
```

**Implementation:**

```csharp
public class AgnumExportService : IAgnumExportService
{
    private readonly IDocumentSession _session;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgnumExportService> _logger;
    
    public async Task<Result> ExportSnapshot(ExportMode mode, CancellationToken ct)
    {
        try
        {
            var exportId = $"EXP-{DateTime.UtcNow:yyyy-MM-dd}-{Guid.NewGuid().ToString("N")[..6]}";
            
            // Query data
            var balances = await QueryBalances();
            var valuations = await QueryValuations();
            var mappings = await QueryMappings();
            
            // Apply mapping rules
            var exportData = ApplyMappings(balances, valuations, mappings, mode);
            
            // Generate CSV
            var csv = GenerateCSV(exportData);
            
            // Send to Agnum API
            var apiEndpoint = _configuration["Agnum:ApiEndpoint"];
            var apiKey = _configuration["Agnum:ApiKey"];
            
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            
            var content = new StringContent(csv, Encoding.UTF8, "text/csv");
            var response = await client.PostAsync($"{apiEndpoint}/inventory/import?exportId={exportId}", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new Exception($"Agnum API returned {response.StatusCode}: {error}");
            }
            
            _logger.LogInformation("Agnum export {ExportId} completed successfully", exportId);
            
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agnum export failed");
            return Result.Fail(ex.Message);
        }
    }
}
```

---

## 10. Observability Implementation

### 10.1 Structured Logging

**Configuration:**

```csharp
services.AddLogging(builder =>
{
    builder.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.File("logs/warehouse-.log", rollingInterval: RollingInterval.Day)
        .CreateLogger());
});
```

**Correlation ID Middleware:**

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                         ?? Guid.NewGuid().ToString();
        
        correlationContext.CorrelationId = Guid.Parse(correlationId);
        correlationContext.CausationId = Guid.NewGuid();
        
        context.Response.Headers.Add("X-Correlation-ID", correlationId);
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

---

### 10.2 Metrics with Prometheus

**Configuration:**

```csharp
services.AddPrometheusMetrics();

app.UseMetricServer();  // Expose /metrics endpoint
app.UseHttpMetrics();   // Track HTTP metrics
```

**Custom Metrics:**

```csharp
public class WarehouseMetrics
{
    private static readonly Counter StockMovementsCounter = Metrics.CreateCounter(
        "warehouse_stock_movements_total",
        "Total number of stock movements",
        new CounterConfiguration
        {
            LabelNames = new[] { "movement_type", "warehouse" }
        });
    
    private static readonly Histogram CommandLatency = Metrics.CreateHistogram(
        "warehouse_command_duration_seconds",
        "Command processing duration",
        new HistogramConfiguration
        {
            LabelNames = new[] { "command_type" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        });
    
    public static void RecordStockMovement(MovementType type, string warehouse)
    {
        StockMovementsCounter.WithLabels(type.ToString(), warehouse).Inc();
    }
    
    public static IDisposable MeasureCommandDuration(string commandType)
    {
        return CommandLatency.WithLabels(commandType).NewTimer();
    }
}
```

---

### 10.3 Distributed Tracing

**OpenTelemetry Configuration:**

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("Warehouse.*")
            .AddJaegerExporter(options =>
            {
                options.AgentHost = Configuration["Jaeger:AgentHost"];
                options.AgentPort = int.Parse(Configuration["Jaeger:AgentPort"]);
            });
    });
```

**Manual Span Creation:**

```csharp
public class StockLedgerCommandHandler
{
    private readonly ActivitySource _activitySource = new("Warehouse.StockLedger");
    
    public async Task<Result> Handle(RecordStockMovementCommand command)
    {
        using var activity = _activitySource.StartActivity("RecordStockMovement");
        activity?.SetTag("sku", command.SKU);
        activity?.SetTag("quantity", command.Quantity);
        activity?.SetTag("from_location", command.FromLocation);
        activity?.SetTag("to_location", command.ToLocation);
        
        // ... command logic
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Result.Ok();
    }
}
```

---

## Implementation Checklist

- [ ] Marten event store configured with PostgreSQL
- [ ] Command pipeline with idempotency, validation, transaction behaviors
- [ ] Transactional outbox implemented with background processor
- [ ] Saga runtime with state persistence and step idempotency
- [ ] Projection runtime with async daemon and lag monitoring
- [ ] Event schema versioning with upcasting support
- [ ] Offline sync protocol with queue, validation, and reconciliation
- [ ] Integration adapters for label printing, Agnum export, ERP
- [ ] Observability with structured logging, metrics, and tracing
- [ ] All architectural decisions (1-5) enforced in implementation

---

**End of Implementation Blueprint Part 2**



---

## 11. Projection Rebuild Tooling (MITIGATION V-5)

### 11.1 Rebuild Command

**Purpose:** Provide safe, verifiable projection rebuild with shadow table approach.

**Implementation:**

```csharp
public class ProjectionRebuildCommand
{
    public string ProjectionName { get; set; }
    public bool VerifyBeforeSwap { get; set; } = true;
    public bool AutoSwap { get; set; } = false;
}

public class ProjectionRebuildService
{
    private readonly IDocumentSession _session;
    private readonly ILogger<ProjectionRebuildService> _logger;
    
    public async Task<RebuildResult> RebuildProjection(ProjectionRebuildCommand command)
    {
        _logger.LogInformation("Starting rebuild for projection {ProjectionName}", command.ProjectionName);
        
        var result = new RebuildResult { ProjectionName = command.ProjectionName };
        
        try
        {
            // Step 1: Create shadow table
            await CreateShadowTable(command.ProjectionName);
            result.ShadowTableCreated = true;
            
            // Step 2: Replay events to shadow table
            var eventsProcessed = await ReplayEventsToShadow(command.ProjectionName);
            result.EventsProcessed = eventsProcessed;
            
            // Step 3: Compute checksums
            var productionChecksum = await ComputeChecksum(command.ProjectionName, isShadow: false);
            var shadowChecksum = await ComputeChecksum(command.ProjectionName, isShadow: true);
            
            result.ProductionChecksum = productionChecksum;
            result.ShadowChecksum = shadowChecksum;
            result.ChecksumsMatch = productionChecksum == shadowChecksum;
            
            // Step 4: Verify and optionally swap
            if (command.VerifyBeforeSwap)
            {
                if (!result.ChecksumsMatch)
                {
                    _logger.LogWarning(
                        "Checksum mismatch for projection {ProjectionName}. Production: {ProductionChecksum}, Shadow: {ShadowChecksum}",
                        command.ProjectionName, productionChecksum, shadowChecksum);
                    
                    // Generate diff report
                    result.DiffReport = await GenerateDiffReport(command.ProjectionName);
                    result.Success = false;
                    
                    return result;
                }
            }
            
            // Step 5: Swap tables
            if (command.AutoSwap || !command.VerifyBeforeSwap)
            {
                await SwapTables(command.ProjectionName);
                result.TablesSwapped = true;
                _logger.LogInformation("Tables swapped for projection {ProjectionName}", command.ProjectionName);
            }
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding projection {ProjectionName}", command.ProjectionName);
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }
    
    private async Task CreateShadowTable(string projectionName)
    {
        var tableName = GetTableName(projectionName);
        var shadowTableName = $"{tableName}_shadow";
        
        var sql = $@"
            DROP TABLE IF EXISTS {shadowTableName};
            CREATE TABLE {shadowTableName} (LIKE {tableName} INCLUDING ALL);
        ";
        
        await _session.Connection.ExecuteAsync(sql);
        
        _logger.LogInformation("Shadow table created: {ShadowTableName}", shadowTableName);
    }
    
    private async Task<int> ReplayEventsToShadow(string projectionName)
    {
        // [MITIGATION V-5 Rule A] Order by sequence number, not timestamp
        var events = await _session.Events.QueryAllRawEvents()
            .OrderBy(e => e.Sequence)
            .ToListAsync();
        
        var projectionHandler = GetProjectionHandler(projectionName);
        var eventsProcessed = 0;
        
        foreach (var evt in events)
        {
            // [MITIGATION V-5 Rule B] Use only self-contained event data
            await projectionHandler.ApplyToShadow(evt);
            eventsProcessed++;
            
            if (eventsProcessed % 1000 == 0)
            {
                _logger.LogInformation("Processed {EventsProcessed} events for {ProjectionName}", 
                    eventsProcessed, projectionName);
            }
        }
        
        _logger.LogInformation("Replay complete. Total events processed: {EventsProcessed}", eventsProcessed);
        
        return eventsProcessed;
    }
    
    private async Task<string> ComputeChecksum(string projectionName, bool isShadow)
    {
        var tableName = GetTableName(projectionName);
        if (isShadow)
            tableName += "_shadow";
        
        // Compute checksum based on projection type
        var sql = projectionName switch
        {
            "LocationBalance" => $@"
                SELECT MD5(STRING_AGG(location || sku || quantity::text, '' ORDER BY location, sku))
                FROM {tableName}",
            
            "AvailableStock" => $@"
                SELECT MD5(STRING_AGG(location || sku || physical_quantity::text || reserved_quantity::text, '' ORDER BY location, sku))
                FROM {tableName}",
            
            "ActiveHardLocks" => $@"
                SELECT MD5(STRING_AGG(reservation_id::text || location || sku || hard_locked_qty::text, '' ORDER BY reservation_id, location, sku))
                FROM {tableName}",
            
            _ => throw new ArgumentException($"Unknown projection: {projectionName}")
        };
        
        var checksum = await _session.Connection.ExecuteScalarAsync<string>(sql);
        
        _logger.LogInformation("Checksum for {TableName}: {Checksum}", tableName, checksum);
        
        return checksum;
    }
    
    private async Task<string> GenerateDiffReport(string projectionName)
    {
        var tableName = GetTableName(projectionName);
        var shadowTableName = $"{tableName}_shadow";
        
        // Find rows that differ
        var sql = projectionName switch
        {
            "LocationBalance" => $@"
                SELECT 'PRODUCTION_ONLY' as source, location, sku, quantity
                FROM {tableName}
                WHERE NOT EXISTS (
                    SELECT 1 FROM {shadowTableName} s
                    WHERE s.location = {tableName}.location AND s.sku = {tableName}.sku
                )
                UNION ALL
                SELECT 'SHADOW_ONLY' as source, location, sku, quantity
                FROM {shadowTableName}
                WHERE NOT EXISTS (
                    SELECT 1 FROM {tableName} p
                    WHERE p.location = {shadowTableName}.location AND p.sku = {shadowTableName}.sku
                )
                UNION ALL
                SELECT 'QUANTITY_DIFF' as source, p.location, p.sku, p.quantity - s.quantity as diff
                FROM {tableName} p
                JOIN {shadowTableName} s ON p.location = s.location AND p.sku = s.sku
                WHERE p.quantity != s.quantity
                ORDER BY source, location, sku
                LIMIT 100",
            
            _ => throw new ArgumentException($"Unknown projection: {projectionName}")
        };
        
        var diffs = await _session.Connection.QueryAsync(sql);
        
        var report = new StringBuilder();
        report.AppendLine($"Diff Report for {projectionName}");
        report.AppendLine($"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        
        foreach (var diff in diffs)
        {
            report.AppendLine(diff.ToString());
        }
        
        return report.ToString();
    }
    
    private async Task SwapTables(string projectionName)
    {
        var tableName = GetTableName(projectionName);
        var shadowTableName = $"{tableName}_shadow";
        var oldTableName = $"{tableName}_old";
        
        // [MITIGATION V-5 Rule C] Atomic swap with rollback capability
        var sql = $@"
            BEGIN;
            ALTER TABLE {tableName} RENAME TO {oldTableName};
            ALTER TABLE {shadowTableName} RENAME TO {tableName};
            COMMIT;
        ";
        
        await _session.Connection.ExecuteAsync(sql);
        
        _logger.LogInformation("Tables swapped: {ShadowTableName} -> {TableName}", shadowTableName, tableName);
        
        // Schedule old table deletion after 24 hours
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromHours(24));
            await _session.Connection.ExecuteAsync($"DROP TABLE IF EXISTS {oldTableName}");
            _logger.LogInformation("Old table dropped: {OldTableName}", oldTableName);
        });
    }
    
    private string GetTableName(string projectionName)
    {
        return projectionName switch
        {
            "LocationBalance" => "location_balance",
            "AvailableStock" => "available_stock",
            "ActiveHardLocks" => "active_hard_locks",
            "OnHandValue" => "on_hand_value",
            _ => throw new ArgumentException($"Unknown projection: {projectionName}")
        };
    }
    
    private IProjectionHandler GetProjectionHandler(string projectionName)
    {
        // Return appropriate projection handler
        // Implementation depends on projection architecture
        throw new NotImplementedException();
    }
}

public class RebuildResult
{
    public string ProjectionName { get; set; }
    public bool Success { get; set; }
    public bool ShadowTableCreated { get; set; }
    public int EventsProcessed { get; set; }
    public string ProductionChecksum { get; set; }
    public string ShadowChecksum { get; set; }
    public bool ChecksumsMatch { get; set; }
    public bool TablesSwapped { get; set; }
    public string DiffReport { get; set; }
    public string Error { get; set; }
}
```

---

### 11.2 CLI Command

**Usage:**

```bash
# Rebuild with verification (manual swap)
dotnet run -- rebuild-projection LocationBalance --verify

# Rebuild with auto-swap (use with caution)
dotnet run -- rebuild-projection LocationBalance --auto-swap

# Rebuild without verification (testing only)
dotnet run -- rebuild-projection LocationBalance --no-verify
```

**Implementation:**

```csharp
public class RebuildProjectionCliCommand : ICommand
{
    [Argument(0, Description = "Projection name to rebuild")]
    public string ProjectionName { get; set; }
    
    [Option("--verify", Description = "Verify checksums before swapping")]
    public bool Verify { get; set; } = true;
    
    [Option("--auto-swap", Description = "Automatically swap tables if checksums match")]
    public bool AutoSwap { get; set; } = false;
    
    public async Task<int> OnExecuteAsync(IConsole console)
    {
        var service = new ProjectionRebuildService(_session, _logger);
        
        var command = new ProjectionRebuildCommand
        {
            ProjectionName = ProjectionName,
            VerifyBeforeSwap = Verify,
            AutoSwap = AutoSwap
        };
        
        var result = await service.RebuildProjection(command);
        
        console.WriteLine($"Projection: {result.ProjectionName}");
        console.WriteLine($"Success: {result.Success}");
        console.WriteLine($"Events Processed: {result.EventsProcessed}");
        console.WriteLine($"Production Checksum: {result.ProductionChecksum}");
        console.WriteLine($"Shadow Checksum: {result.ShadowChecksum}");
        console.WriteLine($"Checksums Match: {result.ChecksumsMatch}");
        console.WriteLine($"Tables Swapped: {result.TablesSwapped}");
        
        if (!result.ChecksumsMatch && !string.IsNullOrEmpty(result.DiffReport))
        {
            console.WriteLine();
            console.WriteLine("Diff Report:");
            console.WriteLine(result.DiffReport);
        }
        
        if (!string.IsNullOrEmpty(result.Error))
        {
            console.WriteLine($"Error: {result.Error}");
        }
        
        return result.Success ? 0 : 1;
    }
}
```

---

**End of Implementation Blueprint Part 2**
