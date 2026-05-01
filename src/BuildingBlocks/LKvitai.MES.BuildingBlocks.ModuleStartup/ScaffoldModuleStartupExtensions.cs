using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;

namespace LKvitai.MES.BuildingBlocks.ModuleStartup;

public static class ScaffoldModuleStartupExtensions
{
    private const string StructuredLogTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [TraceId:{TraceId}] [Req:{RequestMethod} {RequestPath}] {Message:lj}{NewLine}{Exception}";

    public static WebApplicationBuilder UseScaffoldSerilog(this WebApplicationBuilder builder, string logFilePrefix)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePrefix);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .MinimumLevel.Information()
            .Filter.ByExcluding(logEvent => logEvent.Level is LogEventLevel.Debug or LogEventLevel.Verbose)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            // Surface Kestrel lifecycle messages ("Now listening on:", "Application
            // started.", "Hosting environment", "Content root path") so module
            // operators can see at a glance that the host actually came up.
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: StructuredLogTemplate)
            .WriteTo.File(
                $"logs/{logFilePrefix}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: StructuredLogTemplate)
            .CreateLogger();

        builder.Host.UseSerilog();
        return builder;
    }

    public static IServiceCollection AddScaffoldApiCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();
        return services;
    }

    public static IServiceCollection AddScaffoldWebUiCore(
        this IServiceCollection services,
        string httpClientName,
        string baseUrlConfigurationKey,
        string fallbackBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpClientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrlConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackBaseUrl);

        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddMudServices();
        services.AddHttpClient(httpClientName, (sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var baseUrl = configuration[baseUrlConfigurationKey] ?? fallbackBaseUrl;

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    public static WebApplication UseScaffoldApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();
        app.UseRouting();
        app.MapHealthChecks("/health");
        return app;
    }

    public static WebApplication UseScaffoldWebUiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

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
        return app;
    }

    public static PathString ResolvePathBase(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration["PathBase"];
        return string.IsNullOrWhiteSpace(configured)
            ? PathString.Empty
            : new PathString(configured.TrimEnd('/'));
    }
}
