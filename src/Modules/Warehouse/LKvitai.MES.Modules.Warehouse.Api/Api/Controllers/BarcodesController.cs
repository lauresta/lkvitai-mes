using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/barcodes")]
public sealed class BarcodesController : ControllerBase
{
    public const string SupplierCodeAmbiguousErrorCode = "SUPPLIER_CODE_AMBIGUOUS";

    private readonly WarehouseDbContext _dbContext;

    public BarcodesController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> LookupAsync(
        [FromQuery] string code,
        [FromQuery] string? type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ValidationFailure("Query parameter 'code' is required.");
        }

        var normalized = code.Trim();

        if (string.Equals(type, "location", StringComparison.OrdinalIgnoreCase))
        {
            var location = await _dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Barcode == normalized, cancellationToken);

            if (location is null)
            {
                return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location barcode '{normalized}' does not exist."));
            }

            if (!string.Equals(location.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableFailure($"Location '{location.Code}' is in status '{location.Status}'.");
            }

            return Ok(new LocationLookupDto(
                location.Id,
                location.Code,
                location.Type,
                location.MaxWeight,
                location.MaxVolume,
                location.Status));
        }

        var barcode = await _dbContext.ItemBarcodes
            .AsNoTracking()
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Barcode == normalized, cancellationToken);

        if (barcode is not null && barcode.Item is not null)
        {
            return Ok(new ItemBarcodeLookupDto(
                barcode.Barcode,
                barcode.ItemId,
                barcode.Item.InternalSKU,
                barcode.Item.Name,
                barcode.BarcodeType,
                barcode.IsPrimary));
        }

        // No item barcode matches: fall back to the supplier's own item code.
        var lowered = normalized.ToLower();
        var supplierMatches = await _dbContext.SupplierItemMappings
            .AsNoTracking()
            .Where(x => x.SupplierSKU.ToLower() == lowered && x.Item != null)
            .Select(x => new SupplierCodeCandidateDto(
                x.ItemId,
                x.Item!.InternalSKU,
                x.Item.Name,
                x.SupplierId,
                x.Supplier != null ? x.Supplier.Name : string.Empty,
                x.SupplierSKU))
            .ToListAsync(cancellationToken);

        var distinctItems = supplierMatches.Select(x => x.ItemId).Distinct().Count();
        if (distinctItems == 1)
        {
            var match = supplierMatches[0];
            return Ok(new ItemBarcodeLookupDto(
                match.SupplierSKU,
                match.ItemId,
                match.InternalSKU,
                match.ItemName,
                "SupplierCode",
                false,
                MatchedBySupplierCode: true));
        }

        if (distinctItems > 1)
        {
            var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
                DomainErrorCodes.ValidationError,
                $"Several items have supplier code '{normalized}'. Scan the item barcode or choose the item manually.",
                HttpContext);
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Ambiguous supplier code";
            problemDetails.Extensions["errorCode"] = SupplierCodeAmbiguousErrorCode;
            problemDetails.Extensions["candidates"] = supplierMatches
                .OrderBy(x => x.InternalSKU, StringComparer.Ordinal)
                .ThenBy(x => x.SupplierName, StringComparer.Ordinal)
                .ToList();

            return new ObjectResult(problemDetails)
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }

        return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Barcode '{normalized}' does not exist in the system."));
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult UnprocessableFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record LocationLookupDto(
        int Id,
        string Code,
        string Type,
        decimal? MaxWeight,
        decimal? MaxVolume,
        string Status);

    public sealed record ItemBarcodeLookupDto(
        string Barcode,
        int ItemId,
        string InternalSKU,
        string ItemName,
        string BarcodeType,
        bool IsPrimary,
        bool MatchedBySupplierCode = false);

    public sealed record SupplierCodeCandidateDto(
        int ItemId,
        string InternalSKU,
        string ItemName,
        int SupplierId,
        string SupplierName,
        string SupplierSKU);
}
