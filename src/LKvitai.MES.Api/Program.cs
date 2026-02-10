using LKvitai.MES.Api.Configuration;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Infrastructure;
using LKvitai.MES.Infrastructure.BackgroundJobs;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Infrastructure.Projections;
using LKvitai.MES.Projections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog per blueprint
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/warehouse-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services per blueprint
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
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

// Infrastructure services (projection rebuild, etc.)
builder.Services.AddInfrastructureServices();

// MediatR command pipeline
builder.Services.AddMediatRPipeline();

// MassTransit saga orchestration
builder.Services.AddMassTransitConfiguration(builder.Configuration);

// IEventBus — MassTransit implementation (composition root wires this)
builder.Services.AddScoped<IEventBus, MassTransitEventBus>();

// OpenTelemetry observability
builder.Services.AddOpenTelemetryConfiguration(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Log.Information("Starting LKvitai.MES Warehouse API");

app.Run();
