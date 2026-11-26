using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using LKvitai.MES.BuildingBlocks.PortalAuth;

namespace LKvitai.MES.Modules.Finance.Api.BankPayments;

public sealed record BankPaymentRow(int Nr, DateOnly Date, string Firm, string Purpose, decimal Sum);
public sealed record BankPaymentPage(IReadOnlyList<BankPaymentRow> Items, int Total, int CountryTotal,
    DateOnly? LastDate, int Page, int PageSize)
{
    /// <summary>False on a stand where import and deletion are switched off (the test stand), so the page can say so up front.</summary>
    public bool WritesEnabled { get; init; } = true;
}
public sealed record BankPasteRequest(string Country, string Text);
public sealed record BankPasteResult(Guid PreviewId, int LatestNr, IReadOnlyList<BankPasteRow> Rows);
public sealed record BankStructuredRow(string Date, string Firm, string Purpose, string Sum);
public sealed record BankStructuredPreviewRequest(string Country, IReadOnlyList<BankStructuredRow> Rows);
public sealed record BankImportRow(DateOnly Date, string Firm, string Purpose, decimal Sum, bool AllowDuplicate = false);
public sealed record BankImportRequest(string Country, Guid PreviewId, int ExpectedNextNr, IReadOnlyList<int> SelectedLines);
public sealed record BankImportResult(Guid ImportId, int NrFrom, int NrTo, int Count);
internal sealed record BankImportBatch(Guid Id, Guid Actor, IReadOnlyList<BankPaymentRow> Rows, DateTimeOffset Created);
internal sealed record BankPreviewBatch(Guid Id, Guid Actor, string Country, int LatestNr,
    IReadOnlyList<BankPasteRow> Rows, DateTimeOffset Created);
internal sealed class BankImportSessions
{
    private readonly ConcurrentDictionary<Guid, BankImportBatch> _last = new();
    private readonly ConcurrentDictionary<Guid, BankPreviewBatch> _previews = new();
    public void Save(BankImportBatch batch) => _last[batch.Actor] = batch;
    public BankImportBatch? Get(Guid actor, Guid id) => _last.TryGetValue(actor, out var batch) && batch.Id == id &&
        batch.Created > DateTimeOffset.UtcNow.AddMinutes(-30) ? batch : null;
    public void Remove(Guid actor, Guid id) { if (Get(actor, id) is not null) _last.TryRemove(actor, out _); }
    public void SavePreview(BankPreviewBatch batch)
    {
        foreach (var stale in _previews.Where(item => item.Value.Created <= DateTimeOffset.UtcNow.AddMinutes(-15)))
            _previews.TryRemove(stale.Key, out _);
        _previews[batch.Id] = batch;
    }
    public BankPreviewBatch? GetPreview(Guid actor, Guid id) =>
        _previews.TryGetValue(id, out var batch) && batch.Actor == actor &&
        batch.Created > DateTimeOffset.UtcNow.AddMinutes(-15) ? batch : null;
    public void RemovePreview(Guid id) => _previews.TryRemove(id, out _);
}
internal sealed class BankOperationException(int status, string message) : Exception(message) { public int Status { get; } = status; }

public static class BankPaymentWrites
{
    public const string ConfigKey = "Finance:BankPayments:WritesEnabled";
    /// <summary>Problem type of the 409 a stand without writes returns, so the page can tell it from a real conflict.</summary>
    public const string DisabledProblemType = "urn:cikada:finance:bank-payments:writes-disabled";

    public static bool IsWrite(string method, string? path) =>
        HttpMethods.IsDelete(method) ||
        HttpMethods.IsPost(method) && (path ?? "").TrimEnd('/').EndsWith("/bank-payments", StringComparison.OrdinalIgnoreCase);

    public static bool Enabled(IConfiguration configuration) => configuration.GetValue(ConfigKey, false);
}

internal static class BankPaymentsEndpoints
{
    public static void MapBankPayments(this WebApplication app)
    {
        var group = app.MapGroup("/api/finance/bank-payments").RequireAuthorization("FinanceUser");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.Request.Method != "GET" &&
                http.User.Identity?.AuthenticationType != SalesCredentialProtector.SsoScheme)
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
                // Imports and deletions change live legacy payments and cannot be limited to a test branch,
                // so a stand has to opt in (production does; the test stand does not).
                if (BankPaymentWrites.IsWrite(http.Request.Method, http.Request.Path) &&
                    !BankPaymentWrites.Enabled(http.RequestServices.GetRequiredService<IConfiguration>()))
                    return Results.Problem(statusCode: 409, type: BankPaymentWrites.DisabledProblemType,
                        title: "Bank payment import and deletion are switched off on this stand. Parsing and preview still work.");
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
        group.MapGet("", async (HttpContext http, BankRepository repo, IConfiguration configuration, string country,
            DateOnly? from, DateOnly? to, string? q, string? sort, bool? desc, int? page, int? nr) =>
            await repo.List(Actor(http), country, from, to, q, sort, desc ?? true, page ?? 1, http.RequestAborted, nr)
                with { WritesEnabled = BankPaymentWrites.Enabled(configuration) });
        group.MapPost("/parse", (HttpContext http, BankRepository repo, BankPasteRequest request,
            IConfiguration configuration) => repo.Parse(Actor(http), request, configuration, http.RequestAborted));
        group.MapPost("/preview", (HttpContext http, BankRepository repo, BankStructuredPreviewRequest request,
            IConfiguration configuration) => repo.PreviewStructured(Actor(http), request, configuration, http.RequestAborted));
        group.MapPost("", (HttpContext http, BankRepository repo, BankImportRequest request) =>
            repo.Import(Actor(http), request, http.RequestAborted));
        group.MapDelete("/{nr:int}", async (HttpContext http, BankRepository repo, int nr) =>
        { await repo.Delete(Actor(http), nr, http.RequestAborted); return Results.NoContent(); });
        group.MapDelete("/imports/{importId:guid}", async (HttpContext http, BankRepository repo, Guid importId) =>
        { await repo.Undo(Actor(http), importId, http.RequestAborted); return Results.NoContent(); });
    }
    private static FinanceActor Actor(HttpContext http) => (FinanceActor)http.Items[typeof(FinanceActor)]!;
}
