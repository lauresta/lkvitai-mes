using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LKvitai.MES.Modules.Warehouse.Api.Configuration;

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
                if (configuration.GetValue<bool>("UseOtlpExporter"))
                {
                    // OTLP/gRPC (Jaeger all-in-one accepts it on 4317); replaces the deprecated Jaeger exporter.
                    builder.AddOtlpExporter(options =>
                    {
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        options.Endpoint = new Uri(configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
                    });
                }
            });
        
        return services;
    }
}
