using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Stage 2: live, read-only Access production queue. The fixture UI remains
/// separate until production commands are connected; no queue row can be issued here.</summary>
public static class ProductionQueueEndpoints
{
    public static RouteGroupBuilder MapProductionQueueEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-queue", async (int? page, int? pageSize, long? seriesId,
            long? typeId, long? priorityId, ProductionQueueReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 100000 || pageSize is < 1 or > 100 || seriesId is <= 0
                || typeId is <= 0 || priorityId is <= 0)
                return Results.BadRequest(new { code = "INVALID_QUEUE_FILTER" });
            try
            {
                return Results.Ok(await reader.ReadAsync(page ?? 1, pageSize ?? 50,
                    seriesId, typeId, priorityId, ct));
            }
            catch (ProductionQueueAccessException ex)
            {
                return Results.Problem(ex.Message, statusCode: ex.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
            }
            catch (SqlException ex) when (ex.Number is 229 or 230)
            {
                return Results.Problem("Your legacy SQL account does not permit this operation.",
                    statusCode: 403, extensions: new Dictionary<string, object?>
                    { ["code"] = "LEGACY_SQL_PERMISSION_DENIED" });
            }
        });
        return group;
    }
}

public sealed record ProductionQueueItem(long ProductId, long OrderId, string OrderNumber,
    DateTime OrderDate, DateTime? DueDate, long SeriesId, long DepartmentId,
    long CategoryId, string Category, long TypeId, string ProductType,
    string? Control, string? Colour, decimal? WidthMm, decimal? HeightMm,
    decimal Quantity, string? Notes, long? ProductionTaskId, long StateId,
    long PriorityId, string Priority);
public sealed record ProductionQueuePage(int Page, int PageSize, bool HasMore,
    IReadOnlyList<ProductionQueueItem> Items);
public sealed class ProductionQueueAccessException(string code, string message, int status)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
