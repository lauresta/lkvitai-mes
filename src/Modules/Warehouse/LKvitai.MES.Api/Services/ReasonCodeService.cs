using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface IReasonCodeService
{
    Task<Result<IReadOnlyList<ReasonCodeDto>>> GetAsync(
        string? category,
        bool? active,
        CancellationToken cancellationToken = default);

    Task<Result<ReasonCodeDto>> CreateAsync(
        CreateReasonCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ReasonCodeDto>> UpdateAsync(
        int id,
        UpdateReasonCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<Result> IncrementUsageAsync(
        string code,
        ReasonCategory expectedCategory,
        CancellationToken cancellationToken = default);

    Task IncrementUsageIfCodeMatchesAsync(
        string? candidateCode,
        ReasonCategory expectedCategory,
        CancellationToken cancellationToken = default);
}

public sealed class ReasonCodeService : IReasonCodeService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<ReasonCodeService> _logger;

    public ReasonCodeService(
        WarehouseDbContext dbContext,
        ILogger<ReasonCodeService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ReasonCodeDto>>> GetAsync(
        string? category,
        bool? active,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AdjustmentReasonCodes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<ReasonCategory>(category.Trim(), true, out var parsedCategory) ||
                !Enum.IsDefined(parsedCategory))
            {
                return Result<IReadOnlyList<ReasonCodeDto>>.Fail(
                    DomainErrorCodes.ValidationError,
                    "Category must be ADJUSTMENT, REVALUATION, WRITEDOWN, or RETURN.");
            }

            query = query.Where(x => x.Category == parsedCategory);
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.Active == active.Value);
        }

        var rows = await query
            .OrderBy(x => x.Code)
            .Select(x => new ReasonCodeDto(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.ParentId,
                x.Category.ToString(),
                x.Active,
                x.UsageCount,
                x.CreatedBy,
                x.CreatedAt,
                x.UpdatedBy,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ReasonCodeDto>>.Ok(rows);
    }

    public async Task<Result<ReasonCodeDto>> CreateAsync(
        CreateReasonCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateUpsertAsync(null, request.Code, request.Name, request.ParentId, request.Category, cancellationToken);
        if (!validationResult.IsSuccess)
        {
            return Result<ReasonCodeDto>.Fail(validationResult.ErrorCode ?? DomainErrorCodes.ValidationError, validationResult.ErrorDetail ?? validationResult.Error);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var parsedCategory = Enum.Parse<ReasonCategory>(request.Category.Trim(), true);

        var entity = new AdjustmentReasonCode
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ParentId = request.ParentId,
            Category = parsedCategory,
            Active = request.Active
        };

        _dbContext.AdjustmentReasonCodes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reason code created: Id={ReasonCodeId}, Code={Code}, Category={Category}",
            entity.Id,
            entity.Code,
            entity.Category);

        return Result<ReasonCodeDto>.Ok(Map(entity));
    }

    public async Task<Result<ReasonCodeDto>> UpdateAsync(
        int id,
        UpdateReasonCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdjustmentReasonCodes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return Result<ReasonCodeDto>.Fail(DomainErrorCodes.NotFound, $"Reason code '{id}' does not exist.");
        }

        var validationResult = await ValidateUpsertAsync(id, request.Code, request.Name, request.ParentId, request.Category, cancellationToken);
        if (!validationResult.IsSuccess)
        {
            return Result<ReasonCodeDto>.Fail(validationResult.ErrorCode ?? DomainErrorCodes.ValidationError, validationResult.ErrorDetail ?? validationResult.Error);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var parsedCategory = Enum.Parse<ReasonCategory>(request.Category.Trim(), true);

        entity.Code = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.ParentId = request.ParentId;
        entity.Category = parsedCategory;
        entity.Active = request.Active;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reason code updated: Id={ReasonCodeId}, Code={Code}, Active={Active}, UsageCount={UsageCount}",
            entity.Id,
            entity.Code,
            entity.Active,
            entity.UsageCount);

        return Result<ReasonCodeDto>.Ok(Map(entity));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdjustmentReasonCodes
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Reason code '{id}' does not exist.");
        }

        if (entity.UsageCount > 0)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                "Cannot delete reason code with usage history. Mark inactive instead.");
        }

        if (entity.Children.Count > 0)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                "Cannot delete reason code with child reason codes.");
        }

        _dbContext.AdjustmentReasonCodes.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reason code deleted: Id={ReasonCodeId}, Code={Code}", entity.Id, entity.Code);
        return Result.Ok();
    }

    public async Task<Result> IncrementUsageAsync(
        string code,
        ReasonCategory expectedCategory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Reason code is required.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var entity = await _dbContext.AdjustmentReasonCodes
            .FirstOrDefaultAsync(
                x => x.Code == normalizedCode &&
                     x.Active &&
                     x.Category == expectedCategory,
                cancellationToken);

        if (entity is null)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"ReasonCode '{normalizedCode}' does not exist, is inactive, or has wrong category.");
        }

        entity.UsageCount++;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task IncrementUsageIfCodeMatchesAsync(
        string? candidateCode,
        ReasonCategory expectedCategory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidateCode))
        {
            return;
        }

        var normalizedCode = candidateCode.Trim().ToUpperInvariant();
        var entity = await _dbContext.AdjustmentReasonCodes
            .FirstOrDefaultAsync(
                x => x.Code == normalizedCode &&
                     x.Active &&
                     x.Category == expectedCategory,
                cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.UsageCount++;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result> ValidateUpsertAsync(
        int? id,
        string code,
        string name,
        int? parentId,
        string category,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Code is required.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 3)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Name must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(category) ||
            !Enum.TryParse<ReasonCategory>(category.Trim(), true, out var parsedCategory) ||
            !Enum.IsDefined(parsedCategory))
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                "Category must be ADJUSTMENT, REVALUATION, WRITEDOWN, or RETURN.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicate = await _dbContext.AdjustmentReasonCodes
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == normalizedCode, cancellationToken);

        if (duplicate)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, $"Reason code '{normalizedCode}' already exists.");
        }

        var hierarchyValidation = await ValidateHierarchyAsync(id, parentId, cancellationToken);
        if (!hierarchyValidation.IsSuccess)
        {
            return hierarchyValidation;
        }

        return Result.Ok();
    }

    private async Task<Result> ValidateHierarchyAsync(int? id, int? parentId, CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return Result.Ok();
        }

        if (id.HasValue && parentId.Value == id.Value)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Circular reference is not allowed.");
        }

        var parent = await _dbContext.AdjustmentReasonCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parentId.Value, cancellationToken);

        if (parent is null)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, $"Parent reason code '{parentId.Value}' does not exist.");
        }

        if (parent.ParentId.HasValue)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Hierarchy supports max 2 levels (parent -> child).");
        }

        if (id.HasValue)
        {
            var hasChildren = await _dbContext.AdjustmentReasonCodes
                .AsNoTracking()
                .AnyAsync(x => x.ParentId == id.Value, cancellationToken);

            if (hasChildren)
            {
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    "Cannot assign parent when reason code already has children (max 2 levels).");
            }
        }

        return Result.Ok();
    }

    private static ReasonCodeDto Map(AdjustmentReasonCode entity)
    {
        return new ReasonCodeDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.ParentId,
            entity.Category.ToString(),
            entity.Active,
            entity.UsageCount,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt);
    }
}

public sealed record ReasonCodeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ParentId,
    string Category,
    bool Active,
    int UsageCount,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

public sealed record CreateReasonCodeRequest(
    string Code,
    string Name,
    string? Description,
    int? ParentId,
    string Category,
    bool Active);

public sealed record UpdateReasonCodeRequest(
    string Code,
    string Name,
    string? Description,
    int? ParentId,
    string Category,
    bool Active);
