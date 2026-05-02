using System.Diagnostics;
using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Frontline.Application.Ports;
using LKvitai.MES.Modules.Frontline.Contracts.Common;
using LKvitai.MES.Modules.Frontline.Contracts.Fabric;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Frontline.Api.Endpoints;

/// <summary>
/// Maps the F-1 read-only fabric-availability endpoints under
/// <c>/api/frontline/fabric</c>. Backed by <see cref="IFabricQueryService"/>
/// (in-memory stub in F-1, SQL Server adapter in F-2). Stays anonymous until
/// the Frontline auth model is finalised — the parent <c>/api/frontline</c>
/// group in <c>Program.cs</c> intentionally does not call
/// <c>RequireAuthorization()</c>.
/// </summary>
public static class FabricAvailabilityEndpoints
{
    /// <summary>
    /// Same regex the legacy <c>FabricAvailabilityController.Mobile</c> action
    /// uses. Mirrored here so the API rejects malformed input with a 400
    /// before it reaches the query service / SQL adapter.
    /// </summary>
    private static readonly Regex CodeShape =
        new("^[A-Z0-9\\-_./]{2,}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    public static RouteGroupBuilder MapFabricAvailabilityEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var fabric = group.MapGroup("/fabric");

        fabric.MapGet("/{code}", GetMobileCardAsync)
            .WithName("GetFrontlineFabricMobileCard")
            .Produces<FabricCardDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        fabric.MapGet("/low-stock", GetLowStockListAsync)
            .WithName("GetFrontlineFabricLowStock")
            .Produces<PagedResult<FabricLowStockDto>>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> GetMobileCardAsync(
        string code,
        int? width,
        int? lowThreshold,
        int? enoughThreshold,
        IFabricQueryService fabric,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { error = "Code is required." });
        }

        var normalised = code.Trim().ToUpperInvariant();
        if (!CodeShape.IsMatch(normalised))
        {
            return Results.BadRequest(new { error = $"Wrong format: '{normalised}'." });
        }

        var query = new FabricLookupParams(
            Code:            normalised,
            Width:           width,
            LowThreshold:    lowThreshold    ?? 10,
            EnoughThreshold: enoughThreshold ?? 25);

        var card = await fabric.GetMobileCardAsync(query, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        loggerFactory.CreateLogger("LKvitai.MES.Modules.Frontline.Api.Endpoints.Fabric").LogInformation(
            "[FrontlinePerf] api GET /api/frontline/fabric/{{code}} code={Code} width={Width} found={Found} elapsedMs={ElapsedMs}",
            normalised, width, card is not null, sw.ElapsedMilliseconds);

        return card is null ? Results.NotFound() : Results.Ok(card);
    }

    private static async Task<IResult> GetLowStockListAsync(
        [AsParameters] LowStockListRequest request,
        IFabricQueryService fabric,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var query = request.ToQueryParams();
        var page = await fabric.GetLowStockListAsync(query, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        loggerFactory.CreateLogger("LKvitai.MES.Modules.Frontline.Api.Endpoints.Fabric").LogInformation(
            "[FrontlinePerf] api GET /api/frontline/fabric/low-stock page={Page} pageSize={PageSize} returned={Returned} total={Total} " +
            "search={HasSearch} threshold={Threshold} status={HasStatus} width={HasWidth} supplier={HasSupplier} elapsedMs={ElapsedMs}",
            page.Page, page.PageSize, page.Items.Count, page.Total,
            !string.IsNullOrWhiteSpace(query.Search), query.ThresholdMeters,
            !string.IsNullOrWhiteSpace(query.Status), query.WidthMm.HasValue,
            !string.IsNullOrWhiteSpace(query.Supplier), sw.ElapsedMilliseconds);

        return Results.Ok(page);
    }

    /// <summary>
    /// API-side binding shape for <c>GET /api/frontline/fabric/low-stock</c>.
    /// All members are nullable so missing query-string values bind to
    /// <c>null</c> instead of triggering a <c>BadHttpRequestException</c> in
    /// minimal-API binding. Mapped into the layer-neutral
    /// <see cref="FabricLowStockQueryParams"/> with the Page / PageSize
    /// guards applied here so the Application contract stays HTTP-free.
    /// </summary>
    private sealed record LowStockListRequest(
        string? Search,
        int?    Threshold,
        string? Status,
        int?    Width,
        string? Supplier,
        int?    Page,
        int?    PageSize)
    {
        public FabricLowStockQueryParams ToQueryParams()
        {
            var page = Page is null or < 1 ? 1 : Page.Value;
            var size = PageSize is null or < 1 or > 500 ? 50 : PageSize.Value;
            return new FabricLowStockQueryParams
            {
                Search          = Search,
                ThresholdMeters = Threshold,
                Status          = Status,
                WidthMm         = Width,
                Supplier        = Supplier,
                Page            = page,
                PageSize        = size,
            };
        }
    }
}
