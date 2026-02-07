using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LKvitai.MES.Api.Configuration;

/// <summary>
/// OpenTelemetry observability configuration per blueprint
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("LKvitai.MES.Warehouse"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("Warehouse.*");
                
                // Configure exporter based on environment
                if (configuration.GetValue<bool>("UseJaegerExporter"))
                {
                    builder.AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["Jaeger:AgentHost"] ?? "localhost";
                        options.AgentPort = configuration.GetValue<int>("Jaeger:AgentPort", 6831);
                    });
                }
                else
                {
                    builder.AddConsoleExporter();
                }
            });
        
        return services;
    }
}
