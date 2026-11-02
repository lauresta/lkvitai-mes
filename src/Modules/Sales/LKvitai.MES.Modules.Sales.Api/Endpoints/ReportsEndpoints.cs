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

        reports.MapGet("/{report}", async ([AsParameters] ReportRequest request, HttpContext http, ISalesReports service, CancellationToken ct) =>
            Results.Ok(await service.RunAsync(request.ToQuery(http.Request.Query), ct)));

        reports.MapGet("/{report}/dimensions", async (string report, ISalesReports service, CancellationToken ct) =>
            Results.Ok(await service.DimensionsAsync(report, ct)));

        return sales;
    }

    public sealed record ReportRequest(string Report, DateOnly? From, DateOnly? To, string? GroupBy, string? Granularity)
    {
        public SalesReportQuery ToQuery(IQueryCollection query)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var filters = FilterNames
                .Select(name => (name, value: query[name].ToString()))
                .Where(x => !string.IsNullOrWhiteSpace(x.value))
                .ToDictionary(x => x.name, x => x.value);
            var dims = (GroupBy ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new SalesReportQuery(Report, From ?? new DateOnly(today.Year, today.Month, 1), To ?? today.AddDays(1), dims, Granularity ?? "month", filters);
        }
    }
}
