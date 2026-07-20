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
[Route("api/warehouse/v1/price-groups")]
public sealed class PriceGroupsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public PriceGroupsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.PriceGroups
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new PriceGroupDto(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertPriceGroupRequest request,
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

        var code = request.Code.Trim();
        var duplicate = await _dbContext.PriceGroups
            .AsNoTracking()
            .AnyAsync(x => x.Code == code, cancellationToken);
        if (duplicate)
        {
            return ValidationFailure($"Price group '{code}' already exists.");
        }

        var entity = new PriceGroup
        {
            Code = code,
            Name = request.Name.Trim(),
            IsActive = request.IsActive
        };

        _dbContext.PriceGroups.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/warehouse/v1/price-groups/{entity.Id}", new PriceGroupDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.IsActive));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertPriceGroupRequest request,
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

        var entity = await _dbContext.PriceGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Price group '{id}' does not exist."));
        }

        var code = request.Code.Trim();
        var duplicate = await _dbContext.PriceGroups
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == code, cancellationToken);
        if (duplicate)
        {
            return ValidationFailure($"Price group '{code}' already exists.");
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PriceGroupDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PriceGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Price group '{id}' does not exist."));
        }

        var hasOverrides = await _dbContext.ItemPriceOverrides
            .AsNoTracking()
            .AnyAsync(x => x.PriceGroupId == id, cancellationToken);
        if (hasOverrides)
        {
            return UnprocessableFailure("Price group has item price overrides and cannot be deleted.");
        }

        var hasCustomers = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(x => x.PriceGroupId == id, cancellationToken);
        if (hasCustomers)
        {
            return UnprocessableFailure("Price group is assigned to customers and cannot be deleted.");
        }

        _dbContext.PriceGroups.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/customers")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetCustomersAsync(int id, CancellationToken cancellationToken = default)
    {
        var groupExists = await _dbContext.PriceGroups.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!groupExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Price group '{id}' does not exist."));
        }

        var rows = await _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.PriceGroupId == id)
            .OrderBy(x => x.Name)
            .Select(x => new PriceGroupCustomerDto(x.Id, x.CustomerCode, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(rows);
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

    public sealed record UpsertPriceGroupRequest(string Code, string Name, bool IsActive);
    public sealed record PriceGroupDto(int Id, string Code, string Name, bool IsActive);
    public sealed record PriceGroupCustomerDto(Guid Id, string CustomerCode, string Name);
}
