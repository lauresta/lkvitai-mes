using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Api.Services;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyViewDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyRequest request, string createdBy, CancellationToken cancellationToken = default);
    Task<Result<ApiKeyCreatedDto>> RotateAsync(int id, string rotatedBy, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiKeyValidationResult> ValidateAsync(string plainKey, CancellationToken cancellationToken = default);
}

public sealed class ApiKeyService : IApiKeyService
{
    private static long _cacheGeneration;

    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "read:items",
        "write:orders",
        "read:stock"
    };

    private readonly WarehouseDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        WarehouseDbContext dbContext,
        IMemoryCache memoryCache,
        ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApiKeyViewDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeyViewDto(
                x.Id,
                x.Name,
                x.Scopes,
                x.ExpiresAt,
                x.Active,
                x.RateLimitPerMinute,
                x.LastUsedAt,
                x.CreatedBy,
                x.CreatedAt,
                x.PreviousKeyGraceUntil,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ApiKeyCreatedDto>> CreateAsync(
        CreateApiKeyRequest request,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (!validation.IsSuccess)
        {
            return Result<ApiKeyCreatedDto>.Fail(validation.ErrorCode!, validation.ErrorDetail!);
        }

        var normalizedName = request.Name.Trim();
        var nameExists = await _dbContext.ApiKeys.AnyAsync(
            x => x.Name.ToLower() == normalizedName.ToLower(),
            cancellationToken);

        if (nameExists)
        {
            return Result<ApiKeyCreatedDto>.Fail(DomainErrorCodes.ValidationError, "API key name already exists.");
        }

        var plainKey = GeneratePlainKey();
        var keyHash = ComputeHash(plainKey);

        var entity = new ApiKey
        {
            Name = normalizedName,
            KeyHash = keyHash,
            Scopes = NormalizeScopes(request.Scopes),
            ExpiresAt = request.ExpiresAt,
            Active = true,
            RateLimitPerMinute = request.RateLimitPerMinute ?? 100,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation(
            "API key created. ApiKeyId={ApiKeyId}, Name={Name}, Scopes={Scopes}, CreatedBy={CreatedBy}",
            entity.Id,
            entity.Name,
            string.Join(',', entity.Scopes),
            entity.CreatedBy);

        return Result<ApiKeyCreatedDto>.Ok(new ApiKeyCreatedDto(
            entity.Id,
            entity.Name,
            plainKey,
            entity.Scopes,
            entity.ExpiresAt,
            entity.Active,
            entity.RateLimitPerMinute,
            entity.CreatedAt,
            null));
    }

    public async Task<Result<ApiKeyCreatedDto>> RotateAsync(
        int id,
        string rotatedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<ApiKeyCreatedDto>.Fail(DomainErrorCodes.NotFound, "API key not found.");
        }

        var plainKey = GeneratePlainKey();
        var now = DateTimeOffset.UtcNow;

        entity.PreviousKeyHash = entity.KeyHash;
        entity.PreviousKeyGraceUntil = now.AddDays(7);
        entity.KeyHash = ComputeHash(plainKey);
        entity.Active = true;
        entity.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation(
            "API key rotated. ApiKeyId={ApiKeyId}, RotatedBy={RotatedBy}, PreviousKeyGraceUntil={GraceUntil}",
            entity.Id,
            string.IsNullOrWhiteSpace(rotatedBy) ? "system" : rotatedBy,
            entity.PreviousKeyGraceUntil);

        return Result<ApiKeyCreatedDto>.Ok(new ApiKeyCreatedDto(
            entity.Id,
            entity.Name,
            plainKey,
            entity.Scopes,
            entity.ExpiresAt,
            entity.Active,
            entity.RateLimitPerMinute,
            entity.CreatedAt,
            entity.PreviousKeyGraceUntil));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "API key not found.");
        }

        entity.Active = false;
        entity.PreviousKeyHash = null;
        entity.PreviousKeyGraceUntil = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        _logger.LogInformation("API key deactivated. ApiKeyId={ApiKeyId}", entity.Id);
        return Result.Ok();
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string plainKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey))
        {
            return ApiKeyValidationResult.Fail("API key is required.");
        }

        var hash = ComputeHash(plainKey.Trim());
        var cacheKey = $"api-key:validation:{Interlocked.Read(ref _cacheGeneration)}:{hash}";

        if (!_memoryCache.TryGetValue(cacheKey, out ApiKeyValidationCacheEntry? cached) || cached is null)
        {
            var now = DateTimeOffset.UtcNow;
            var entity = await _dbContext.ApiKeys
                .FirstOrDefaultAsync(x =>
                    x.Active &&
                    (x.KeyHash == hash || (x.PreviousKeyHash == hash && x.PreviousKeyGraceUntil.HasValue && x.PreviousKeyGraceUntil > now)),
                    cancellationToken);

            if (entity is null)
            {
                return ApiKeyValidationResult.Fail("Invalid API key.");
            }

            cached = new ApiKeyValidationCacheEntry(
                entity.Id,
                entity.Name,
                entity.Scopes,
                entity.ExpiresAt,
                entity.Active,
                entity.RateLimitPerMinute,
                entity.KeyHash,
                entity.PreviousKeyHash,
                entity.PreviousKeyGraceUntil);

            _memoryCache.Set(cacheKey, cached, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });
        }

        if (!cached.Active)
        {
            return ApiKeyValidationResult.Fail("API key is inactive.");
        }

        var utcNow = DateTimeOffset.UtcNow;
        if (cached.ExpiresAt.HasValue && cached.ExpiresAt.Value <= utcNow)
        {
            return ApiKeyValidationResult.Fail("API key expired.");
        }

        var isPrimaryKey = string.Equals(cached.KeyHash, hash, StringComparison.Ordinal);
        if (!isPrimaryKey)
        {
            if (!string.Equals(cached.PreviousKeyHash, hash, StringComparison.Ordinal) ||
                !cached.PreviousKeyGraceUntil.HasValue ||
                cached.PreviousKeyGraceUntil.Value <= utcNow)
            {
                return ApiKeyValidationResult.Fail("Invalid API key.");
            }
        }

        var trackedEntity = await _dbContext.ApiKeys.FirstOrDefaultAsync(x => x.Id == cached.Id, cancellationToken);
        if (trackedEntity is not null)
        {
            trackedEntity.LastUsedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "API key authenticated. ApiKeyId={ApiKeyId}, Name={Name}, Scopes={Scopes}",
            cached.Id,
            cached.Name,
            string.Join(',', cached.Scopes));

        return ApiKeyValidationResult.Success(cached.Id, cached.Name, cached.Scopes, cached.RateLimitPerMinute);
    }

    private static Result ValidateCreateRequest(CreateApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "API key name is required.");
        }

        var scopes = NormalizeScopes(request.Scopes);
        if (scopes.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least one scope is required.");
        }

        if (request.RateLimitPerMinute is <= 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "RateLimitPerMinute must be greater than zero.");
        }

        var invalidScopes = scopes.Where(scope => !AllowedScopes.Contains(scope)).ToList();
        if (invalidScopes.Count > 0)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Unsupported scopes: {string.Join(',', invalidScopes)}");
        }

        return Result.Ok();
    }

    private static List<string> NormalizeScopes(IReadOnlyList<string> scopes)
    {
        return scopes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GeneratePlainKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"wh_{token}";
    }

    private static string ComputeHash(string plainKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(hash);
    }

    private static void InvalidateCache()
    {
        Interlocked.Increment(ref _cacheGeneration);
    }

    private sealed record ApiKeyValidationCacheEntry(
        int Id,
        string Name,
        IReadOnlyList<string> Scopes,
        DateTimeOffset? ExpiresAt,
        bool Active,
        int RateLimitPerMinute,
        string KeyHash,
        string? PreviousKeyHash,
        DateTimeOffset? PreviousKeyGraceUntil);
}

public sealed record CreateApiKeyRequest(
    string Name,
    IReadOnlyList<string> Scopes,
    int? RateLimitPerMinute,
    DateTimeOffset? ExpiresAt);

public sealed record ApiKeyCreatedDto(
    int Id,
    string Name,
    string PlainKey,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt,
    bool Active,
    int RateLimitPerMinute,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PreviousKeyGraceUntil);

public sealed record ApiKeyViewDto(
    int Id,
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt,
    bool Active,
    int RateLimitPerMinute,
    DateTimeOffset? LastUsedAt,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PreviousKeyGraceUntil,
    DateTimeOffset? UpdatedAt);

public sealed record ApiKeyValidationResult(
    bool IsSuccess,
    int ApiKeyId,
    string Name,
    IReadOnlyList<string> Scopes,
    int RateLimitPerMinute,
    string? ErrorMessage)
{
    public static ApiKeyValidationResult Success(int id, string name, IReadOnlyList<string> scopes, int rateLimitPerMinute)
        => new(true, id, name, scopes, rateLimitPerMinute, null);

    public static ApiKeyValidationResult Fail(string errorMessage)
        => new(false, 0, string.Empty, [], 100, errorMessage);
}
