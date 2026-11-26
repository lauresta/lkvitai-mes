using System.Security.Claims;
using LKvitai.MES.Modules.Finance.Api.BankPayments;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>
/// GET /api/finance/incomes/{access|summary|rows|export.xlsx} — read-only. Only the Cikada users on
/// <see cref="IncomesAccessList"/> get past the filter; everybody else gets 403.
/// </summary>
internal static class IncomesEndpoints
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public static void MapIncomes(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance/incomes").RequireAuthorization("FinanceUser");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers[CorrelationHeader] = http.TraceIdentifier;
            http.Response.Headers.CacheControl = "no-store";
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Finance.Incomes");
            try
            {
                var actor = await http.RequestServices.GetRequiredService<FinanceAccess>()
                    .ResolveIncomesAsync(http.User, http.RequestAborted);
                if (actor is null) return Problem(http, 403, "Incomes report access required.");
                http.Items[typeof(FinanceActor)] = actor;
                return await next(context);
            }
            catch (IncomesQueryException ex) { return Problem(http, 400, ex.Message); }
            catch (SqlException ex) when (ex.Number is 229 or 230)
            { return Problem(http, 403, "Legacy SQL rejected the read."); }
            catch (SqlException ex)
            {
                logger.LogError(ex, "Incomes query failed ({CorrelationId})", http.TraceIdentifier);
                return Problem(http, 503, "Legacy SQL is unavailable.");
            }
            catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "Incomes access check failed ({CorrelationId})", http.TraceIdentifier);
                return Problem(http, 503, "Finance access service is unavailable.");
            }
        });
        group.MapGet("/access", () => Results.Ok(new { allowed = true }));
        group.MapGet("/summary", async (HttpContext http, FinanceAccess access, IMemoryCache cache, TimeProvider clock) =>
        {
            var query = Query(http, clock);
            await using var source = Source(http, access, cache);
            return Results.Ok(await SummaryAsync(query, source, Today(clock), http.RequestAborted));
        });
        group.MapGet("/rows", async (HttpContext http, FinanceAccess access, IMemoryCache cache, TimeProvider clock) =>
        {
            var query = Query(http, clock);
            await using var source = Source(http, access, cache);
            var table = await TableAsync(query, source, http.RequestAborted);
            return Results.Ok(table.Page(query.Skip, query.Take));
        });
        group.MapGet("/export.xlsx", async (HttpContext http, FinanceAccess access, IMemoryCache cache, TimeProvider clock) =>
        {
            var query = Query(http, clock);
            await using var source = Source(http, access, cache);
            var summary = await SummaryAsync(query, source, Today(clock), http.RequestAborted);
            var table = await TableAsync(query, source, http.RequestAborted);
            var branches = await source.BranchesAsync(http.RequestAborted);
            var user = http.User.FindFirstValue("display_name") ?? http.User.FindFirstValue(ClaimTypes.Name) ?? "";
            var bytes = IncomesExcel.Build(query, summary, table, branches, BaseUrl(http.Request), user,
                clock.GetLocalNow().DateTime);
            return Results.File(bytes, IncomesExcel.ContentType, IncomesExcel.FileName(query));
        });
    }

    private static IncomesDataSource Source(HttpContext http, FinanceAccess access, IMemoryCache cache) =>
        new(access, cache, (FinanceActor)http.Items[typeof(FinanceActor)]!);

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private static IncomesQuery Query(HttpContext http, TimeProvider clock) =>
        IncomesQueryParser.Parse(http.Request.Query.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Today(clock));

    internal static async Task<IncomesSummary> SummaryAsync(IncomesQuery query, IncomesDataSource source, DateOnly today,
        CancellationToken ct)
    {
        var branches = await source.BranchesAsync(ct);
        var (previousFrom, previousTo) = IncomesPeriod.Previous(query.From, query.To);
        var (lastYearFrom, lastYearTo) = IncomesPeriod.LastYear(query.From, query.To);
        var payments = await source.RecordsAsync(IncomesDataset.Money, query.From, query.To, ct);
        var orders = await source.RecordsAsync(IncomesDataset.Orders, query.From, query.To, ct);
        var previous = await source.RecordsAsync(query.Dataset, previousFrom, previousTo, ct);
        var lastYear = await source.RecordsAsync(query.Dataset, lastYearFrom, lastYearTo, ct);
        var sources = new IncomesSources(branches, query.IsMoney ? payments : orders, previous, lastYear, payments, orders);
        return IncomesCalculator.Summary(query, sources, today);
    }

    private static async Task<IncomesTable> TableAsync(IncomesQuery query, IncomesDataSource source, CancellationToken ct)
    {
        var branches = await source.BranchesAsync(ct);
        var records = await source.RecordsAsync(query.Dataset, query.From, query.To, ct);
        return IncomesTable.Build(query, branches, records);
    }

    /// <summary>Public address for the workbook links (Traefik terminates TLS and forwards the scheme).</summary>
    private static string BaseUrl(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-Proto"].ToString().Split(',')[0].Trim();
        var scheme = forwarded is "http" or "https" ? forwarded : request.Scheme;
        return $"{scheme}://{request.Host}";
    }

    private static IResult Problem(HttpContext http, int status, string title) =>
        Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["correlationId"] = http.TraceIdentifier });
}
