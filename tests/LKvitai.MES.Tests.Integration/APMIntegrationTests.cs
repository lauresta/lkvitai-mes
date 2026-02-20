using System.Security.Claims;
using LKvitai.MES.Api.Observability;
using LKvitai.MES.Api.Services;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class APMIntegrationTests
{
    [Fact]
    public void ApiProject_ShouldIncludeApplicationInsightsPackage()
    {
        var csproj = File.ReadAllText(ApiPathResolver.ResolveApiFileOrFail("LKvitai.MES.Modules.Warehouse.Api.csproj"));

        Assert.Contains("Microsoft.ApplicationInsights.AspNetCore", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_ShouldWireApplicationInsightsAndBusinessTelemetry()
    {
        var program = File.ReadAllText(ApiPathResolver.ResolveApiFileOrFail("Program.cs"));

        Assert.Contains("AddApplicationInsightsTelemetry", program, StringComparison.Ordinal);
        Assert.Contains("AddApplicationInsightsTelemetryProcessor<SuccessfulRequestSamplingTelemetryProcessor>", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<IBusinessTelemetryService, BusinessTelemetryService>", program, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_ShouldDefineApmAndApplicationInsightsSections()
    {
        var appsettings = File.ReadAllText(ApiPathResolver.ResolveApiFileOrFail("appsettings.json"));

        Assert.Contains("\"ApplicationInsights\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"ConnectionString\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Apm\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"SuccessfulRequestSampleRate\"", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void SamplingProcessor_ShouldDropSuccessfulRequest_WhenSampleRateIsZero()
    {
        var next = new CapturingTelemetryProcessor();
        var options = Options.Create(new ApmOptions { Enabled = true, SuccessfulRequestSampleRate = 0d });
        var processor = new SuccessfulRequestSamplingTelemetryProcessor(next, options);

        processor.Process(new RequestTelemetry
        {
            Success = true,
            ResponseCode = "200"
        });

        Assert.Equal(0, next.ProcessedCount);
    }

    [Fact]
    public void SamplingProcessor_ShouldKeepFailedRequest_WhenSampleRateIsZero()
    {
        var next = new CapturingTelemetryProcessor();
        var options = Options.Create(new ApmOptions { Enabled = true, SuccessfulRequestSampleRate = 0d });
        var processor = new SuccessfulRequestSamplingTelemetryProcessor(next, options);

        processor.Process(new RequestTelemetry
        {
            Success = false,
            ResponseCode = "500"
        });

        Assert.Equal(1, next.ProcessedCount);
    }

    [Fact]
    public void TelemetryInitializer_ShouldSetUserWarehouseAndOrderType()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/warehouse/v1/sales-orders";
        context.Request.Headers["X-Warehouse-Code"] = "WH-01";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123")
        }, authenticationType: "test"));

        var accessor = new HttpContextAccessor { HttpContext = context };
        var options = Options.Create(new ApmOptions { Enabled = true, WarehouseCodeClaimType = "warehouse_code" });
        var initializer = new ApplicationInsightsEnrichmentTelemetryInitializer(accessor, options);
        var telemetry = new RequestTelemetry();

        initializer.Initialize(telemetry);

        Assert.Equal("user-123", telemetry.Context.User.AuthenticatedUserId);
        Assert.Equal("WH-01", telemetry.Context.GlobalProperties["WarehouseCode"]);
        Assert.Equal("Sales", telemetry.Properties["OrderType"]);
    }

    [Fact]
    public void BusinessTelemetryService_ShouldBeNoOp_WhenTelemetryClientMissing()
    {
        var sut = new BusinessTelemetryService(null);

        sut.TrackOrderCreated(Guid.NewGuid(), Guid.NewGuid(), 100.5m, DateTimeOffset.UtcNow, "Sales");
        sut.TrackShipmentDispatched(Guid.NewGuid(), Guid.NewGuid(), "FEDEX", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(3));
        sut.TrackStockAdjusted(Guid.NewGuid(), 7, 2m, "CYCLE_COUNT");
    }

    private sealed class CapturingTelemetryProcessor : ITelemetryProcessor
    {
        public int ProcessedCount { get; private set; }

        public void Process(ITelemetry item)
        {
            ProcessedCount++;
        }
    }
}
