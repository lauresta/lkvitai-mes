using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Finance.Api.BankPayments;

public sealed record BankPaymentRow(int Nr, DateOnly Date, string Firm, string Purpose, decimal Sum);
public sealed record BankPaymentPage(IReadOnlyList<BankPaymentRow> Items, int Total, int CountryTotal,
    DateOnly? LastDate, int Page, int PageSize);
public sealed record BankPasteRequest(string Country, string Text);
public sealed record BankPasteResult(IReadOnlyList<BankPasteRow> Rows);
public sealed record BankImportRow(DateOnly Date, string Firm, string Purpose, decimal Sum, bool AllowDuplicate = false);
public sealed record BankImportRequest(string Country, IReadOnlyList<BankImportRow> Rows);
public sealed record BankImportResult(Guid ImportId, int NrFrom, int NrTo, int Count);
internal sealed record BankImportBatch(Guid Id, Guid Actor, IReadOnlyList<BankPaymentRow> Rows, DateTimeOffset Created);
internal sealed class BankImportSessions
{
    private readonly ConcurrentDictionary<Guid, BankImportBatch> _last = new();
    public void Save(BankImportBatch batch) => _last[batch.Actor] = batch;
    public BankImportBatch? Get(Guid actor, Guid id) => _last.TryGetValue(actor, out var batch) && batch.Id == id &&
        batch.Created > DateTimeOffset.UtcNow.AddMinutes(-30) ? batch : null;
    public void Remove(Guid actor, Guid id) { if (Get(actor, id) is not null) _last.TryRemove(actor, out _); }
}
internal sealed class BankOperationException(int status, string message) : Exception(message) { public int Status { get; } = status; }

internal static class BankPaymentsEndpoints
{
    public static void MapBankPayments(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance/bank-payments").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.Request.Method != "GET")
            {
                var origin = http.Request.Headers.Origin.ToString();
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    !string.Equals(uri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase) ||
                    (uri.IsDefaultPort ? 0 : uri.Port) != (http.Request.Host.Port ?? 0))
                    return Results.Problem(statusCode: 403, title: "Origin rejected.");
            }
            try
            {
                var actor = await http.RequestServices.GetRequiredService<FinanceAccess>()
                    .ResolveAsync(http.User, http.RequestAborted);
                if (actor is null) return Results.Problem(statusCode: 403, title: "Finance access required.");
                http.Items[typeof(FinanceActor)] = actor;
                return await next(context);
            }
            catch (BankOperationException ex) { return Results.Problem(statusCode: ex.Status, title: ex.Message); }
            catch (ArgumentException ex) { return Results.Problem(statusCode: 400, title: ex.Message); }
            catch (SqlException ex) when (ex.Number is 229 or 230 or 2627 or 2601)
            { return Results.Problem(statusCode: ex.Number is 229 or 230 ? 403 : 409, title: "Legacy SQL rejected the operation."); }
            catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { return Results.Problem(statusCode: 503, title: "Finance access service is unavailable."); }
        });
        group.MapGet("", (HttpContext http, BankRepository repo, string country, DateOnly? from,
            string? q, string? sort, bool? desc, int? page) => repo.List(Actor(http), country, from, q,
                sort, desc ?? true, page ?? 1, http.RequestAborted));
        group.MapPost("/parse", (HttpContext http, BankRepository repo, BankPasteRequest request,
            IConfiguration configuration) => repo.Parse(Actor(http), request, configuration, http.RequestAborted));
        group.MapPost("", (HttpContext http, BankRepository repo, BankImportRequest request) =>
            repo.Import(Actor(http), request, http.RequestAborted));
        group.MapDelete("/{nr:int}", async (HttpContext http, BankRepository repo, int nr) =>
        { await repo.Delete(Actor(http), nr, http.RequestAborted); return Results.NoContent(); });
        group.MapDelete("/imports/{importId:guid}", async (HttpContext http, BankRepository repo, Guid importId) =>
        { await repo.Undo(Actor(http), importId, http.RequestAborted); return Results.NoContent(); });
    }
    private static FinanceActor Actor(HttpContext http) => (FinanceActor)http.Items[typeof(FinanceActor)]!;
}
