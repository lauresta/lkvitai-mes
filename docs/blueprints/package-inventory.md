# Package Inventory (P0.S4.T1)

Generated from:

```bash
find src tests -name "*.csproj" -exec grep -H "PackageReference" {} \;
```

## Unique Packages and Versions

| Package | Version(s) found |
| --- | --- |
| ClosedXML | 0.104.2 |
| coverlet.collector | 6.0.0 |
| Cronos | 0.8.0 |
| CsvHelper | 33.0.1 |
| EFCore.BulkExtensions | 8.1.3 |
| FluentAssertions | 6.12.0 |
| FluentValidation | 11.9.0 |
| FluentValidation.DependencyInjectionExtensions | 11.9.0 |
| FsCheck | 2.16.6 |
| FsCheck.Xunit | 2.16.6 |
| Hangfire.AspNetCore | 1.8.14 |
| Hangfire.MemoryStorage | 1.8.1.2 |
| Hangfire.PostgreSql | 1.20.8 |
| LaunchDarkly.ServerSdk | 8.11.1 |
| Marten | 7.0.0 |
| MassTransit | 8.1.3 |
| MassTransit.Marten | 8.1.3 |
| MassTransit.RabbitMQ | 8.1.3 |
| MediatR | 12.2.0 |
| Microsoft.ApplicationInsights.AspNetCore | 2.22.0 |
| Microsoft.AspNetCore.TestHost | 8.0.0 |
| Microsoft.EntityFrameworkCore | 8.0.13 |
| Microsoft.EntityFrameworkCore.Design | 8.0.13 |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.0 |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 |
| Microsoft.NET.Test.Sdk | 17.11.1, 17.8.0 |
| Moq | 4.20.70 |
| NetArchTest.Rules | 1.3.2 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 |
| OpenTelemetry.Exporter.Console | 1.7.0 |
| OpenTelemetry.Exporter.Jaeger | 1.5.1 |
| OpenTelemetry.Extensions.Hosting | 1.7.0 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.9.0 |
| OpenTelemetry.Instrumentation.Http | 1.9.0 |
| Otp.NET | 1.4.0 |
| Polly | 8.2.1 |
| Polly.Contrib.Simmy | 0.3.0 |
| QRCoder | 1.6.0 |
| Serilog | 3.1.1 |
| Serilog.AspNetCore | 8.0.0 |
| Serilog.Sinks.Console | 5.0.1 |
| Serilog.Sinks.File | 5.0.0 |
| SSH.NET | 2024.2.0 |
| StackExchange.Redis | 2.8.24 |
| Swashbuckle.AspNetCore | 6.5.0 |
| Testcontainers.PostgreSql | 3.6.0 |
| Xunit.SkippableFact | 1.5.61 |
| xunit | 2.9.2, 2.6.2 |
| xunit.runner.visualstudio | 2.8.2, 2.5.4 |

## Version Conflict Report

The following package version conflicts were detected and explicitly approved for centralization:

- Microsoft.NET.Test.Sdk: use `17.11.1`
- xunit: use `2.9.2`
- xunit.runner.visualstudio: use `2.8.2`
