using LKvitai.MES.BuildingBlocks.PortalAuth;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Serilog — mirrors Warehouse.Api / Portal.WebUI log shape so a single /app/logs
// bind-mount collects daily-rolled sales-YYYYMMDD.log alongside the rest.
const string structuredLogTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [TraceId:{TraceId}] [Req:{RequestMethod} {RequestPath}] {Message:lj}{NewLine}{Exception}";

var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .Filter.ByExcluding(logEvent => logEvent.Level is LogEventLevel.Debug or LogEventLevel.Verbose)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: structuredLogTemplate)
    .WriteTo.File(
        "logs/sales-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: structuredLogTemplate);

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

// Sales replicates Warehouse's "internal user mechanism" by leaning on the shared
// Portal cookie (PortalAuth). Same DataProtection keys, same cookie name, so a user
// signed in via Portal is authenticated here too. Once real role-based authorization
// is added to Sales, this will be tightened to match Warehouse's WarehouseHeader
// scheme (see src/Modules/Warehouse/.../Api/Security/WarehouseAuthenticationHandler.cs).
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

var sales = app.MapGroup("/api/sales").RequireAuthorization();

sales.MapGet("/ping", () => Results.Ok(new
{
    Module = "Sales",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

await app.RunAsync();
