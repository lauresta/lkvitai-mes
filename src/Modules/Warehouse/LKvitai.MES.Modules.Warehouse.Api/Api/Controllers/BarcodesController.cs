using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/barcodes")]
public sealed class BarcodesController : ControllerBase
{
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

        if (barcode is null || barcode.Item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Barcode '{normalized}' does not exist in the system."));
        }

        return Ok(new ItemBarcodeLookupDto(
            barcode.Barcode,
            barcode.ItemId,
            barcode.Item.InternalSKU,
            barcode.Item.Name,
            barcode.BarcodeType,
            barcode.IsPrimary));
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
        bool IsPrimary);
}
