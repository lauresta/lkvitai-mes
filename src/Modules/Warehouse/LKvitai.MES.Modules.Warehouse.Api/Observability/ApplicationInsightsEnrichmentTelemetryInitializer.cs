using System.Security.Claims;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Observability;

public sealed class ApplicationInsightsEnrichmentTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApmOptions _options;

    public ApplicationInsightsEnrichmentTelemetryInitializer(
        IHttpContextAccessor httpContextAccessor,
        IOptions<ApmOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var userId = ResolveUserId(httpContext.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            telemetry.Context.User.AuthenticatedUserId = userId;
        }

        var warehouseCode = ResolveWarehouseCode(httpContext);
        if (!string.IsNullOrWhiteSpace(warehouseCode))
        {
            telemetry.Context.GlobalProperties["WarehouseCode"] = warehouseCode;
        }

        if (telemetry is ISupportProperties properties)
        {
            var orderType = ResolveOrderType(httpContext.Request.Path);
            if (!string.IsNullOrWhiteSpace(orderType))
            {
                properties.Properties["OrderType"] = orderType;
            }
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.Identity?.Name;
    }

    private string? ResolveWarehouseCode(HttpContext context)
    {
        return context.User.FindFirstValue(_options.WarehouseCodeClaimType)
            ?? context.Request.Headers["X-Warehouse-Code"].FirstOrDefault();
    }

    private static string? ResolveOrderType(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.Contains("/sales-orders", StringComparison.OrdinalIgnoreCase))
        {
            return "Sales";
        }

        if (value.Contains("/outbound", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/shipments", StringComparison.OrdinalIgnoreCase))
        {
            return "Outbound";
        }

        return null;
    }
}
