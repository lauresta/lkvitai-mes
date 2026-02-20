using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Api.Services;

public interface IApprovalRuleService
{
    Task<IReadOnlyList<ApprovalRuleDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<ApprovalRuleDto>> CreateAsync(
        CreateApprovalRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ApprovalRuleDto>> UpdateAsync(
        int id,
        UpdateApprovalRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<ApprovalRuleEvaluationDto>> EvaluateAsync(
        EvaluateApprovalRuleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ApprovalRuleService : IApprovalRuleService
{
    private const string CacheKey = "approval-rules:active";

    private static readonly HashSet<string> AllowedApproverRoles =
    [
        WarehouseRoles.WarehouseAdmin,
        WarehouseRoles.WarehouseManager,
        WarehouseRoles.Operator,
        WarehouseRoles.QCInspector,
        WarehouseRoles.SalesAdmin,
        WarehouseRoles.PackingOperator,
        WarehouseRoles.DispatchClerk,
        WarehouseRoles.InventoryAccountant,
        WarehouseRoles.CFO,
        "Admin",
        "Manager",
        "CFO"
    ];

    private readonly WarehouseDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ApprovalRuleService> _logger;

    public ApprovalRuleService(
        WarehouseDbContext dbContext,
        IMemoryCache memoryCache,
        ILogger<ApprovalRuleService> logger)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApprovalRuleDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ApprovalRules
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.RuleType)
            .ThenByDescending(x => x.ThresholdValue)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<Result<ApprovalRuleDto>> CreateAsync(
        CreateApprovalRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request.RuleType, request.ThresholdType, request.ThresholdValue, request.ApproverRole, request.Priority);
        if (!validation.IsSuccess)
        {
            return Result<ApprovalRuleDto>.Fail(validation.ErrorCode ?? DomainErrorCodes.ValidationError, validation.ErrorDetail ?? validation.Error);
        }

        var entity = new ApprovalRule
        {
            RuleType = Enum.Parse<ApprovalRuleType>(request.RuleType.Trim(), true),
            ThresholdType = Enum.Parse<ApprovalThresholdType>(request.ThresholdType.Trim(), true),
            ThresholdValue = request.ThresholdValue,
            ApproverRole = request.ApproverRole.Trim(),
            Active = request.Active,
            Priority = request.Priority
        };

        _dbContext.ApprovalRules.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation(
            "Approval rule created: Id={RuleId}, RuleType={RuleType}, Threshold={Threshold}, ApproverRole={ApproverRole}, Priority={Priority}",
            entity.Id,
            entity.RuleType,
            entity.ThresholdValue,
            entity.ApproverRole,
            entity.Priority);

        return Result<ApprovalRuleDto>.Ok(Map(entity));
    }

    public async Task<Result<ApprovalRuleDto>> UpdateAsync(
        int id,
        UpdateApprovalRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request.RuleType, request.ThresholdType, request.ThresholdValue, request.ApproverRole, request.Priority);
        if (!validation.IsSuccess)
        {
            return Result<ApprovalRuleDto>.Fail(validation.ErrorCode ?? DomainErrorCodes.ValidationError, validation.ErrorDetail ?? validation.Error);
        }

        var entity = await _dbContext.ApprovalRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<ApprovalRuleDto>.Fail(DomainErrorCodes.NotFound, $"Approval rule '{id}' does not exist.");
        }

        entity.RuleType = Enum.Parse<ApprovalRuleType>(request.RuleType.Trim(), true);
        entity.ThresholdType = Enum.Parse<ApprovalThresholdType>(request.ThresholdType.Trim(), true);
        entity.ThresholdValue = request.ThresholdValue;
        entity.ApproverRole = request.ApproverRole.Trim();
        entity.Active = request.Active;
        entity.Priority = request.Priority;

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation(
            "Approval rule updated: Id={RuleId}, RuleType={RuleType}, Threshold={Threshold}, ApproverRole={ApproverRole}, Priority={Priority}, Active={Active}",
            entity.Id,
            entity.RuleType,
            entity.ThresholdValue,
            entity.ApproverRole,
            entity.Priority,
            entity.Active);

        return Result<ApprovalRuleDto>.Ok(Map(entity));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ApprovalRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Approval rule '{id}' does not exist.");
        }

        _dbContext.ApprovalRules.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation("Approval rule deleted: Id={RuleId}", id);
        return Result.Ok();
    }

    public async Task<Result<ApprovalRuleEvaluationDto>> EvaluateAsync(
        EvaluateApprovalRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RuleType) ||
            !Enum.TryParse<ApprovalRuleType>(request.RuleType.Trim(), true, out var ruleType) ||
            !Enum.IsDefined(ruleType))
        {
            return Result<ApprovalRuleEvaluationDto>.Fail(
                DomainErrorCodes.ValidationError,
                "RuleType must be COST_ADJUSTMENT, WRITEDOWN, or TRANSFER.");
        }

        if (request.Value < 0m)
        {
            return Result<ApprovalRuleEvaluationDto>.Fail(
                DomainErrorCodes.ValidationError,
                "Value must be >= 0.");
        }

        var activeRules = await GetCachedActiveRulesAsync(cancellationToken);
        var matchedRule = activeRules
            .Where(x => x.RuleType == ruleType)
            .Where(x => request.Value > x.ThresholdValue)
            .OrderBy(x => x.Priority)
            .ThenByDescending(x => x.ThresholdValue)
            .FirstOrDefault();

        if (matchedRule is null)
        {
            return Result<ApprovalRuleEvaluationDto>.Ok(new ApprovalRuleEvaluationDto(false, null, null));
        }

        return Result<ApprovalRuleEvaluationDto>.Ok(
            new ApprovalRuleEvaluationDto(
                true,
                matchedRule.ApproverRole,
                matchedRule.Id));
    }

    private Result ValidateRequest(
        string ruleType,
        string thresholdType,
        decimal thresholdValue,
        string approverRole,
        int priority)
    {
        if (string.IsNullOrWhiteSpace(ruleType) ||
            !Enum.TryParse<ApprovalRuleType>(ruleType.Trim(), true, out var parsedRuleType) ||
            !Enum.IsDefined(parsedRuleType))
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                "RuleType must be COST_ADJUSTMENT, WRITEDOWN, or TRANSFER.");
        }

        if (string.IsNullOrWhiteSpace(thresholdType) ||
            !Enum.TryParse<ApprovalThresholdType>(thresholdType.Trim(), true, out var parsedThresholdType) ||
            !Enum.IsDefined(parsedThresholdType))
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                "ThresholdType must be AMOUNT or PERCENTAGE.");
        }

        if (thresholdValue < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ThresholdValue must be >= 0.");
        }

        if (priority <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Priority must be > 0.");
        }

        if (string.IsNullOrWhiteSpace(approverRole) || !AllowedApproverRoles.Contains(approverRole.Trim()))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ApproverRole must reference an existing role.");
        }

        return Result.Ok();
    }

    private async Task<IReadOnlyList<ApprovalRule>> GetCachedActiveRulesAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<ApprovalRule>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _dbContext.ApprovalRules
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.Priority)
            .ThenByDescending(x => x.ThresholdValue)
            .ToListAsync(cancellationToken);

        _memoryCache.Set(
            CacheKey,
            rows,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

        return rows;
    }

    private void InvalidateCache()
    {
        _memoryCache.Remove(CacheKey);
    }

    private static ApprovalRuleDto Map(ApprovalRule entity)
    {
        return new ApprovalRuleDto(
            entity.Id,
            entity.RuleType.ToString(),
            entity.ThresholdType.ToString(),
            entity.ThresholdValue,
            entity.ApproverRole,
            entity.Active,
            entity.Priority,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt);
    }
}

public sealed record ApprovalRuleDto(
    int Id,
    string RuleType,
    string ThresholdType,
    decimal ThresholdValue,
    string ApproverRole,
    bool Active,
    int Priority,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

public sealed record CreateApprovalRuleRequest(
    string RuleType,
    string ThresholdType,
    decimal ThresholdValue,
    string ApproverRole,
    bool Active,
    int Priority);

public sealed record UpdateApprovalRuleRequest(
    string RuleType,
    string ThresholdType,
    decimal ThresholdValue,
    string ApproverRole,
    bool Active,
    int Priority);

public sealed record EvaluateApprovalRuleRequest(string RuleType, decimal Value);

public sealed record ApprovalRuleEvaluationDto(
    bool RequiresApproval,
    string? ApproverRole,
    int? MatchedRuleId);
