using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public CategoriesController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ItemCategories
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new CategoryDto(x.Id, x.Code, x.Name, x.ParentCategoryId))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertCategoryRequest request,
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
        var duplicate = await _dbContext.ItemCategories
            .AsNoTracking()
            .AnyAsync(x => x.Code == code, cancellationToken);
        if (duplicate)
        {
            return ValidationFailure($"Category '{code}' already exists.");
        }

        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _dbContext.ItemCategories
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentCategoryId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent category '{request.ParentCategoryId.Value}' does not exist.");
            }
        }

        var entity = new ItemCategory
        {
            Code = code,
            Name = request.Name.Trim(),
            ParentCategoryId = request.ParentCategoryId
        };

        _dbContext.ItemCategories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/warehouse/v1/categories/{entity.Id}", new CategoryDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.ParentCategoryId));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertCategoryRequest request,
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

        var entity = await _dbContext.ItemCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Category '{id}' does not exist."));
        }

        if (request.ParentCategoryId == id)
        {
            return ValidationFailure("Category cannot reference itself as parent.");
        }

        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _dbContext.ItemCategories
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentCategoryId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent category '{request.ParentCategoryId.Value}' does not exist.");
            }
        }

        var code = request.Code.Trim();
        var duplicate = await _dbContext.ItemCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == code, cancellationToken);
        if (duplicate)
        {
            return ValidationFailure($"Category '{code}' already exists.");
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.ParentCategoryId = request.ParentCategoryId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new CategoryDto(entity.Id, entity.Code, entity.Name, entity.ParentCategoryId));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ItemCategories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Category '{id}' does not exist."));
        }

        var hasChildren = await _dbContext.ItemCategories
            .AsNoTracking()
            .AnyAsync(x => x.ParentCategoryId == id, cancellationToken);
        if (hasChildren)
        {
            return UnprocessableFailure("Category has child categories and cannot be deleted.");
        }

        var hasItems = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.CategoryId == id, cancellationToken);
        if (hasItems)
        {
            return UnprocessableFailure("Category is referenced by items and cannot be deleted.");
        }

        _dbContext.ItemCategories.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    public sealed record UpsertCategoryRequest(string Code, string Name, int? ParentCategoryId);
    public sealed record CategoryDto(int Id, string Code, string Name, int? ParentCategoryId);
}
