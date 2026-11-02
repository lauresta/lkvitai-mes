using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Reports;

namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

public static class ReportsEndpoints
{
    /// <summary>Roles that may read the management reports (on top of the caller's legacy SQL permissions).</summary>
    public static readonly string[] ReportRoles = ["SalesAdmin", "Reports"];

    private static readonly string[] FilterNames =
        ["type", "unit", "series", "state", "duty", "employee", "department", "salaryGroup", "method", "branch", "branchCode"];

    /// <summary>
    /// <c>GET /api/sales/reports/{report}?from=&amp;to=&amp;groupBy=period,type&amp;granularity=month&amp;type=…</c> and
    /// <c>GET /api/sales/reports/{report}/dimensions</c>, report = production | work | payments.
    /// </summary>
    public static RouteGroupBuilder MapReportsEndpoints(this RouteGroupBuilder sales)
    {
        var reports = sales.MapGroup("/reports").RequireAuthorization(policy => policy.RequireRole(ReportRoles));
        reports.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (SalesReportException ex) { return Results.Problem(statusCode: 400, title: ex.Message); }
        });

        reports.MapGet("/{report}", async (string report, DateOnly? from, DateOnly? to, string? groupBy, string? granularity,
            HttpContext http, ISalesReports service, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = from ?? new DateOnly(today.Year, today.Month, 1);
            var end = to ?? today.AddDays(1);
            var filters = FilterNames
                .Select(name => (name, value: http.Request.Query[name].ToString()))
                .Where(x => !string.IsNullOrWhiteSpace(x.value))
                .ToDictionary(x => x.name, x => x.value);
            var dims = (groupBy ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Results.Ok(await service.RunAsync(new SalesReportQuery(report, start, end, dims, granularity ?? "month", filters), ct));
        });

        reports.MapGet("/{report}/dimensions", async (string report, ISalesReports service, CancellationToken ct) =>
            Results.Ok(await service.DimensionsAsync(report, ct)));

        return sales;
    }
}
