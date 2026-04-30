using MudBlazor.Services;
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
        "logs/frontline-webui-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: structuredLogTemplate);

Log.Logger = loggerConfiguration.CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.AddHttpClient("FrontlineApi", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["FrontlineApi:BaseUrl"] ?? "https://localhost:5031";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UsePathBase(ResolvePathBase(app.Configuration));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/health", () => Results.Ok());
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

static PathString ResolvePathBase(IConfiguration configuration)
{
    var configured = configuration["PathBase"];
    return string.IsNullOrWhiteSpace(configured) ? PathString.Empty : new PathString(configured.TrimEnd('/'));
}
