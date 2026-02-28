using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Middleware;
using LKvitai.MES.Modules.Warehouse.Api.Observability;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.EventVersioning;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure;
using LKvitai.MES.Modules.Warehouse.Infrastructure.BackgroundJobs;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using LKvitai.MES.Modules.Warehouse.Integration.Carrier;
using LKvitai.MES.Modules.Warehouse.Projections;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
const string structuredLogTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [TraceParent:{TraceParent}] [TraceId:{TraceId}] [CorrelationId:{CorrelationId}] [Req:{RequestMethod} {RequestPath}] {Message:lj}{NewLine}{Exception}";

// Configure Serilog per blueprint
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .Filter.ByExcluding(logEvent => logEvent.Level is LogEventLevel.Debug or LogEventLevel.Verbose)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Marten", LogEventLevel.Error)
    .MinimumLevel.Override("Npgsql", LogEventLevel.Error)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Error)
    .MinimumLevel.Override("JasperFx", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: structuredLogTemplate)
    .WriteTo.File(
        "logs/warehouse-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: structuredLogTemplate);

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

// Add services per blueprint
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient("AgnumExportApi");
builder.Services.AddHttpClient("FedExApi");
builder.Services.AddHttpClient("OAuthProvider");
builder.Services.AddHttpClient("PagerDuty");
builder.Services.Configure<ApmOptions>(builder.Configuration.GetSection(ApmOptions.SectionName));
builder.Services.Configure<PagerDutyOptions>(builder.Configuration.GetSection(PagerDutyOptions.SectionName));
builder.Services.Configure<AlertEscalationOptions>(builder.Configuration.GetSection(AlertEscalationOptions.SectionName));
builder.Services.Configure<SlaMonitoringOptions>(builder.Configuration.GetSection(SlaMonitoringOptions.SectionName));
builder.Services.Configure<CapacityPlanningOptions>(builder.Configuration.GetSection(CapacityPlanningOptions.SectionName));
builder.Services.Configure<FeatureFlagsOptions>(builder.Configuration.GetSection(FeatureFlagsOptions.SectionName));
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection(DevAuthOptions.SectionName));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));
builder.Services.Configure<MfaOptions>(builder.Configuration.GetSection(MfaOptions.SectionName));
builder.Services.Configure<LabelPrintingConfig>(builder.Configuration.GetSection("LabelPrinting"));
builder.Services.AddSingleton<IDevAuthService, DevAuthService>();
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddSingleton<ConnectionPoolMonitoringInterceptor>();
builder.Services.AddSingleton<IAdminUserStore, InMemoryAdminUserStore>();
builder.Services.AddSingleton<IOAuthRoleMapper, OAuthRoleMapper>();
builder.Services.AddSingleton<IOAuthOpenIdConfigurationProvider, OAuthOpenIdConfigurationProvider>();
builder.Services.AddSingleton<IOAuthLoginStateStore, OAuthLoginStateStore>();
builder.Services.AddSingleton<IMfaSessionTokenService, MfaSessionTokenService>();
builder.Services.AddScoped<IOAuthTokenValidator, OAuthTokenValidator>();
builder.Services.AddScoped<IOAuthUserProvisioningService, OAuthUserProvisioningService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
builder.Services
    .AddAuthentication(WarehouseAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, WarehouseAuthenticationHandler>(
        WarehouseAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(WarehouseAuthenticationDefaults.Scheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(WarehousePolicies.AdminOnly, policy =>
        policy.RequireRole(WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.ManagerOrAdmin, policy =>
        policy.RequireRole(WarehouseRoles.WarehouseManager, WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.QcOrManager, policy =>
        policy.RequireRole(
            WarehouseRoles.QCInspector,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.OperatorOrAbove, policy =>
        policy.RequireRole(
            WarehouseRoles.Operator,
            WarehouseRoles.QCInspector,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.SalesAdminOrManager, policy =>
        policy.RequireRole(
            WarehouseRoles.SalesAdmin,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.PackingOperatorOrManager, policy =>
        policy.RequireRole(
            WarehouseRoles.PackingOperator,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.DispatchClerkOrManager, policy =>
        policy.RequireRole(
            WarehouseRoles.DispatchClerk,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.InventoryAccountantOrManager, policy =>
        policy.RequireRole(
            WarehouseRoles.InventoryAccountant,
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.CfoOrAdmin, policy =>
        policy.RequireRole(
            WarehouseRoles.CFO,
            WarehouseRoles.WarehouseAdmin));

    options.AddPolicy(WarehousePolicies.AdminOrAuditor, policy =>
        policy.RequireRole(
            WarehouseRoles.WarehouseAdmin,
            WarehouseRoles.Auditor));

    options.AddPolicy(WarehousePolicies.ManagerOrAuditor, policy =>
        policy.RequireRole(
            WarehouseRoles.WarehouseManager,
            WarehouseRoles.Auditor));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Marten event store configuration
builder.Services.AddMartenEventStore(builder.Configuration, options =>
{
    // Register projections from Projections module
    // Composition root wires projections without Infrastructure referencing Projections
    options.RegisterProjections();
});

// EF Core DbContext for state-based aggregates
builder.Services.AddWarehouseDbContext(builder.Configuration);

// Outbox processor background service
builder.Services.AddHostedService<LKvitai.MES.Modules.Warehouse.Infrastructure.Outbox.OutboxProcessor>();
builder.Services.AddHostedService<SchemaValidationService>();
builder.Services.AddHostedService<ReservationExpiryJob>();
builder.Services.AddHostedService<IdempotencyCleanupHostedService>();

// Infrastructure services (projection rebuild, etc.)
builder.Services.AddInfrastructureServices();

// MediatR command pipeline
builder.Services.AddMediatRPipeline();

// MassTransit saga orchestration
builder.Services.AddMassTransitConfiguration(builder.Configuration);

// IEventBus — MassTransit implementation (composition root wires this)
builder.Services.AddScoped<IEventBus, MassTransitEventBus>();
builder.Services.AddScoped<ICarrierApiService, FedExApiService>();
builder.Services.AddScoped<IAvailableStockQuantityResolver, MartenAvailableStockQuantityResolver>();
builder.Services.AddScoped<IAgnumSecretProtector, AgnumDataProtector>();
builder.Services.AddScoped<IAgnumExportOrchestrator, AgnumExportOrchestrator>();
builder.Services.AddScoped<AgnumExportRecurringJob>();
builder.Services.AddSingleton<IAgnumReconciliationReportStore, InMemoryAgnumReconciliationReportStore>();
builder.Services.AddScoped<IAgnumReconciliationService, AgnumReconciliationService>();
builder.Services.AddSingleton<LabelTemplateEngine>();
builder.Services.AddScoped<ILotTraceabilityService, LotTraceabilityService>();
builder.Services.AddSingleton<ILotTraceStore, InMemoryLotTraceStore>();
builder.Services.AddSingleton<ILabelPrinterTransport, TcpLabelPrinterTransport>();
builder.Services.AddSingleton<ILabelPrintQueueStore, InMemoryLabelPrintQueueStore>();
builder.Services.AddScoped<ILabelPrinterClient, TcpLabelPrinterClient>();
builder.Services.AddScoped<ILabelPrintOrchestrator, LabelPrintOrchestrator>();
builder.Services.AddScoped<LabelPrintOrchestrator>();
builder.Services.AddScoped<ILabelPrintQueueProcessor, LabelPrintQueueProcessor>();
builder.Services.AddScoped<LabelPrintQueueRecurringJob>();
builder.Services.AddScoped<ITransferStockAvailabilityService, MartenTransferStockAvailabilityService>();
builder.Services.AddScoped<ICycleCountQuantityResolver, MartenCycleCountQuantityResolver>();
builder.Services.AddSingleton<IAdvancedWarehouseStore, AdvancedWarehouseStore>();
builder.Services.AddScoped<IWarehouseSettingsService, WarehouseSettingsService>();
builder.Services.AddScoped<IReasonCodeService, ReasonCodeService>();
builder.Services.AddScoped<IApprovalRuleService, ApprovalRuleService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<ISecurityAuditLogService, SecurityAuditLogService>();
builder.Services.Configure<TransactionExportOptions>(builder.Configuration.GetSection("Compliance:TransactionExport"));
builder.Services.AddScoped<ITransactionEventReader, MartenTransactionEventReader>();
builder.Services.AddSingleton<ITransactionExportSftpClient, SshNetTransactionExportSftpClient>();
builder.Services.AddScoped<ITransactionExportService, TransactionExportService>();
builder.Services.Configure<ComplianceReportOptions>(builder.Configuration.GetSection("Compliance:Reports"));
builder.Services.AddScoped<IComplianceReportService, ComplianceReportService>();
builder.Services.AddScoped<ScheduledReportsRecurringJob>();
builder.Services.AddScoped<IElectronicSignatureService, ElectronicSignatureService>();
builder.Services.AddScoped<IRetentionPolicyService, RetentionPolicyService>();
builder.Services.AddScoped<RetentionPolicyRecurringJob>();
builder.Services.AddScoped<IPiiEncryptionService, PiiEncryptionService>();
builder.Services.AddScoped<PiiReencryptionJob>();
builder.Services.AddScoped<IGdprErasureService, GdprErasureService>();
builder.Services.AddScoped<GdprErasureJob>();
builder.Services.AddScoped<ISchemaDriftHealthService, SchemaDriftHealthService>();
builder.Services.AddScoped<IBusinessTelemetryService, BusinessTelemetryService>();
builder.Services.AddScoped<IAlertEscalationService, PagerDutyAlertEscalationService>();
builder.Services.AddSingleton<SlaRequestMetricsStore>();
builder.Services.AddScoped<ISlaMonitoringService, SlaMonitoringService>();
builder.Services.AddScoped<ICapacityPlanningService, CapacityPlanningService>();
builder.Services.AddSingleton<IChaosResilienceService, ChaosResilienceService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<DailyBackupRecurringJob>();
builder.Services.AddScoped<MonthlyRestoreTestRecurringJob>();
builder.Services.AddScoped<IDisasterRecoveryService, DisasterRecoveryService>();
builder.Services.AddScoped<QuarterlyDisasterRecoveryDrillJob>();

var applicationInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = applicationInsightsConnectionString;
        options.EnableAdaptiveSampling = false;
    });
    builder.Services.AddSingleton<ITelemetryInitializer, ApplicationInsightsEnrichmentTelemetryInitializer>();
    builder.Services.AddApplicationInsightsTelemetryProcessor<SuccessfulRequestSamplingTelemetryProcessor>();
}

builder.Services.AddSingleton<ICacheService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
    var redisConnectionString = builder.Configuration["Caching:RedisConnectionString"];
    return new RedisCacheService(logger, redisConnectionString);
});

var warehouseConnectionString =
    builder.Configuration.GetConnectionString("WarehouseDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Warehouse")
    ?? builder.Configuration["ConnectionStrings:WarehouseDb"];

builder.Services.AddHangfire(configuration =>
{
    if (!string.IsNullOrWhiteSpace(warehouseConnectionString))
    {
        configuration.UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(warehouseConnectionString));
        return;
    }

    configuration.UseMemoryStorage();
});
builder.Services.AddHangfireServer();

// OpenTelemetry observability
builder.Services.AddOpenTelemetryConfiguration(builder.Configuration);

var app = builder.Build();

// Validate and initialize event schema version registry at startup.
_ = app.Services.GetService<IEventSchemaVersionRegistry>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapPost(
            "/api/auth/dev-token",
            (DevTokenRequest request, IDevAuthService authService) =>
            {
                var response = authService.GenerateToken(request);
                return response is null
                    ? Results.Unauthorized()
                    : Results.Ok(response);
            })
        .AllowAnonymous();

    app.Logger.LogWarning("Dev auth enabled - DO NOT USE IN PRODUCTION");
}

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP request completed. StatusCode={StatusCode}, ElapsedMs={Elapsed:0.0000}";
    options.GetLevel = (_, elapsed, ex) =>
    {
        if (ex is not null)
        {
            return LogEventLevel.Error;
        }

        return elapsed > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;
    };
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("ResponseStatusCode", httpContext.Response.StatusCode);
    };
});
app.UseMiddleware<SlaMetricsMiddleware>();
app.UseMiddleware<ApiRateLimitingMiddleware>();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseMiddleware<IdempotencyReplayHeaderMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SecurityAuditLoggingMiddleware>();
app.UseMiddleware<MfaEnforcementMiddleware>();
app.UseMiddleware<ApiKeyScopeMiddleware>();
app.UseMiddleware<PermissionPolicyMiddleware>();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");
app.MapControllers();

RecurringJob.AddOrUpdate<AgnumExportRecurringJob>(
    AgnumRecurringJobs.DailyExportJobId,
    job => job.ExecuteAsync("SCHEDULED", null, 0),
    "0 23 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<LabelPrintQueueRecurringJob>(
    "labels-print-queue-retry-5m",
    job => job.ExecuteAsync(),
    "*/5 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<ScheduledReportsRecurringJob>(
    "compliance-scheduled-reports",
    job => job.ExecuteAsync(),
    "*/10 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<RetentionPolicyRecurringJob>(
    "retention-policy-daily",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<DailyBackupRecurringJob>(
    "backup-daily-2am",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<MonthlyRestoreTestRecurringJob>(
    "backup-monthly-restore-test",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 1 * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

RecurringJob.AddOrUpdate<QuarterlyDisasterRecoveryDrillJob>(
    "dr-quarterly-drill",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 4 1 1,4,7,10 *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

Log.Information("Starting LKvitai.MES Warehouse API");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
    dbContext.Database.Migrate();
}

app.Run();
