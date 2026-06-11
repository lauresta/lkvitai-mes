using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public SuppliersController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] string? country,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(normalized) ||
                x.Name.ToLower().Contains(normalized) ||
                (x.ShortName != null && x.ShortName.ToLower().Contains(normalized)) ||
                (x.CompanyCode != null && x.CompanyCode.ToLower().Contains(normalized)) ||
                (x.VatCode != null && x.VatCode.ToLower().Contains(normalized)) ||
                (x.Email != null && x.Email.ToLower().Contains(normalized)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(normalized)) ||
                (x.Country != null && x.Country.ToLower().Contains(normalized)) ||
                (x.City != null && x.City.ToLower().Contains(normalized)));
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            var normalizedCountry = country.Trim().ToLowerInvariant();
            query = query.Where(x => x.Country != null && x.Country.ToLower() == normalizedCountry);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(x => x.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var rows = entities.Select(ToDto).ToList();
        return Ok(new PagedResponse<SupplierListItemDto>(rows, totalCount, pageNumber, pageSize));
    }

    [HttpGet("countries")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var countries = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.Country != null && x.Country != "")
            .Select(x => x.Country!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        var normalizedCode = request.Code.Trim();
        var exists = await _dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
        if (exists)
        {
            return ValidationFailure($"Supplier with code '{normalizedCode}' already exists.");
        }

        var entity = new Supplier
        {
            Code = normalizedCode,
            Name = request.Name.Trim()
        };
        ApplyStructuredFields(entity, request);

        _dbContext.Suppliers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/warehouse/v1/suppliers/{entity.Id}",
            ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        var entity = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Supplier '{id}' does not exist."));
        }

        var normalizedCode = request.Code.Trim();
        var duplicateCode = await _dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == normalizedCode, cancellationToken);
        if (duplicateCode)
        {
            return ValidationFailure($"Supplier with code '{normalizedCode}' already exists.");
        }

        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        // AgnumClientId is owned by the Agnum sync and is never mutated through this endpoint.
        ApplyStructuredFields(entity, request);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(entity));
    }

    private static void ApplyStructuredFields(Supplier entity, UpsertSupplierRequest request)
    {
        entity.ShortName = Normalize(request.ShortName);
        entity.CompanyCode = Normalize(request.CompanyCode);
        entity.VatCode = Normalize(request.VatCode);
        entity.RegisteredAddress = Normalize(request.RegisteredAddress);
        entity.PickupAddress = Normalize(request.PickupAddress);
        entity.City = Normalize(request.City);
        entity.Country = Normalize(request.Country);
        entity.ContactName = Normalize(request.ContactName);
        entity.Phone = Normalize(request.Phone);
        entity.Email = Normalize(request.Email);
        entity.Website = Normalize(request.Website);
        entity.AdditionalInfo = Normalize(request.AdditionalInfo);
        entity.ContactInfo = Normalize(request.ContactInfo);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SupplierListItemDto ToDto(Supplier x) => new(
        x.Id,
        x.AgnumClientId,
        x.Code,
        x.Name,
        x.ShortName,
        x.CompanyCode,
        x.VatCode,
        x.RegisteredAddress,
        x.PickupAddress,
        x.City,
        x.Country,
        x.ContactName,
        x.Phone,
        x.Email,
        x.Website,
        x.AdditionalInfo,
        x.ContactInfo,
        x.LastAgnumSyncedAt,
        x.CreatedAt,
        x.UpdatedAt);

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

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record UpsertSupplierRequest(
        string Code,
        string Name,
        string? ShortName = null,
        string? CompanyCode = null,
        string? VatCode = null,
        string? RegisteredAddress = null,
        string? PickupAddress = null,
        string? City = null,
        string? Country = null,
        string? ContactName = null,
        string? Phone = null,
        string? Email = null,
        string? Website = null,
        string? AdditionalInfo = null,
        string? ContactInfo = null);

    public sealed record SupplierListItemDto(
        int Id,
        int? AgnumClientId,
        string Code,
        string Name,
        string? ShortName,
        string? CompanyCode,
        string? VatCode,
        string? RegisteredAddress,
        string? PickupAddress,
        string? City,
        string? Country,
        string? ContactName,
        string? Phone,
        string? Email,
        string? Website,
        string? AdditionalInfo,
        string? ContactInfo,
        DateTimeOffset? LastAgnumSyncedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
