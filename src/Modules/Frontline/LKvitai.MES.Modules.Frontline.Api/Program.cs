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
        "logs/frontline-.log",
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

// TODO: tighten auth when roles are defined. Frontline is the safe field/branch
// surface (e.g. fabric availability); it stays anonymous until the role model
// is finalised, at which point this group will adopt the shared PortalAuth
// scheme and a read-only policy for operators in the field. Until then the
// endpoints are reachable without authentication because this app intentionally
// does not register AddAuthorization()/UseAuthentication/UseAuthorization — no
// AllowAnonymous() metadata is emitted here to avoid suggesting otherwise.
var frontline = app.MapGroup("/api/frontline");

frontline.MapGet("/ping", () => Results.Ok(new
{
    Module = "Frontline",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

await app.RunAsync();
