using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

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
        "logs/scanning-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: structuredLogTemplate);

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseRouting();

app.MapHealthChecks("/health");

// TODO: tighten auth when roles are defined. Scanning is the cross-cutting mobile
// barcode lookup surface; it stays anonymous until the role model is finalised, at
// which point this group will adopt the shared PortalAuth scheme and enforce a
// minimal "scanner" policy for operator identification.
var scanning = app.MapGroup("/api/scan").AllowAnonymous();

scanning.MapGet("/ping", () => Results.Ok(new
{
    Module = "Scanning",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

await app.RunAsync();
