using LKvitai.MES.Api.Configuration;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Middleware;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.EventVersioning;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Infrastructure;
using LKvitai.MES.Infrastructure.BackgroundJobs;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Infrastructure.Projections;
using LKvitai.MES.Integration.Carrier;
using LKvitai.MES.Projections;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

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
    .WriteTo.Console()
    .WriteTo.File("logs/warehouse-.log", rollingInterval: RollingInterval.Day);

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
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection(DevAuthOptions.SectionName));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));
builder.Services.Configure<LabelPrintingConfig>(builder.Configuration.GetSection("LabelPrinting"));
builder.Services.AddSingleton<IDevAuthService, DevAuthService>();
builder.Services.AddSingleton<IAdminUserStore, InMemoryAdminUserStore>();
builder.Services.AddSingleton<IOAuthRoleMapper, OAuthRoleMapper>();
builder.Services.AddSingleton<IOAuthOpenIdConfigurationProvider, OAuthOpenIdConfigurationProvider>();
builder.Services.AddSingleton<IOAuthLoginStateStore, OAuthLoginStateStore>();
builder.Services.AddScoped<IOAuthTokenValidator, OAuthTokenValidator>();
builder.Services.AddScoped<IOAuthUserProvisioningService, OAuthUserProvisioningService>();
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
builder.Services.AddHostedService<LKvitai.MES.Infrastructure.Outbox.OutboxProcessor>();
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
app.UseMiddleware<ApiRateLimitingMiddleware>();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseMiddleware<IdempotencyReplayHeaderMiddleware>();
app.UseAuthentication();
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

Log.Information("Starting LKvitai.MES Warehouse API");

app.Run();
