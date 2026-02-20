using System.Security.Cryptography;
using System.Text.Json;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;

namespace LKvitai.MES.Api.Services;

public interface IMfaService
{
    Task<Result<MfaEnrollmentDto>> EnrollAsync(Guid userId, string userLabel, CancellationToken cancellationToken = default);
    Task<Result> VerifyEnrollmentAsync(Guid userId, string code, CancellationToken cancellationToken = default);
    Task<Result<MfaVerifyResultDto>> VerifyChallengeAsync(MfaVerifyRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<string>>> RegenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ResetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MfaUserStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    bool IsMfaRequired(IReadOnlyList<string> roles);
    int GetChallengeTimeoutMinutes();
    int GetSessionTimeoutHours();
}

public sealed class MfaService : IMfaService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IAdminUserStore _adminUserStore;
    private readonly IDataProtector _secretProtector;
    private readonly IDataProtector _backupProtector;
    private readonly IMfaSessionTokenService _sessionTokenService;
    private readonly IOptionsMonitor<MfaOptions> _mfaOptionsMonitor;
    private readonly ISecurityAuditLogService? _auditLogService;
    private readonly ILogger<MfaService> _logger;

    public MfaService(
        WarehouseDbContext dbContext,
        IAdminUserStore adminUserStore,
        IDataProtectionProvider dataProtectionProvider,
        IMfaSessionTokenService sessionTokenService,
        IOptionsMonitor<MfaOptions> mfaOptionsMonitor,
        ILogger<MfaService> logger,
        ISecurityAuditLogService? auditLogService = null)
    {
        _dbContext = dbContext;
        _adminUserStore = adminUserStore;
        _secretProtector = dataProtectionProvider.CreateProtector("mfa:totp-secret:v1");
        _backupProtector = dataProtectionProvider.CreateProtector("mfa:backup-codes:v1");
        _sessionTokenService = sessionTokenService;
        _mfaOptionsMonitor = mfaOptionsMonitor;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<Result<MfaEnrollmentDto>> EnrollAsync(Guid userId, string userLabel, CancellationToken cancellationToken = default)
    {
        if (!_adminUserStore.GetAll().Any(x => x.Id == userId))
        {
            return Result<MfaEnrollmentDto>.Fail(DomainErrorCodes.NotFound, "User does not exist.");
        }

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretBase32 = Base32Encoding.ToString(secretBytes);
        var backupCodes = GenerateBackupCodes(10);
        var backupHashes = backupCodes.Select(HashCode).ToList();

        var entity = await _dbContext.UserMfas.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
                     ?? new UserMfa
                     {
                         UserId = userId,
                         CreatedAt = DateTimeOffset.UtcNow
                     };

        entity.TotpSecret = _secretProtector.Protect(secretBase32);
        entity.BackupCodes = _backupProtector.Protect(JsonSerializer.Serialize(backupHashes));
        entity.MfaEnabled = false;
        entity.MfaEnrolledAt = null;
        entity.FailedAttempts = 0;
        entity.LockedUntil = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (_dbContext.Entry(entity).State == EntityState.Detached)
        {
            _dbContext.UserMfas.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var keyUri = BuildOtpAuthUri(secretBase32, userLabel, "LKvitai.MES");
        var qrCodeDataUri = GenerateQrCodeDataUri(keyUri);

        _logger.LogInformation("MFA enrollment initiated for UserId={UserId}", userId);
        await WriteAuditAsync(
            userId.ToString(),
            "MFA_ENROLL_START",
            "USER",
            userId.ToString(),
            cancellationToken,
            "{\"mfaEnabled\":false}");

        return Result<MfaEnrollmentDto>.Ok(new MfaEnrollmentDto(secretBase32, qrCodeDataUri, backupCodes, false));
    }

    public async Task<Result> VerifyEnrollmentAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Code is required.");
        }

        var entity = await _dbContext.UserMfas.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "MFA enrollment not found for user.");
        }

        var secret = UnprotectSecret(entity.TotpSecret);
        var isValid = ValidateTotp(secret, code);
        if (!isValid)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Invalid MFA code.");
        }

        entity.MfaEnabled = true;
        entity.MfaEnrolledAt = DateTimeOffset.UtcNow;
        entity.FailedAttempts = 0;
        entity.LockedUntil = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("MFA enrollment verified for UserId={UserId}", userId);
        await WriteAuditAsync(
            userId.ToString(),
            "MFA_ENROLL_VERIFY",
            "USER",
            userId.ToString(),
            cancellationToken,
            "{\"mfaEnabled\":true}");
        return Result.Ok();
    }

    public async Task<Result<MfaVerifyResultDto>> VerifyChallengeAsync(MfaVerifyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_sessionTokenService.TryParseToken(request.ChallengeToken, out var challenge))
        {
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.Unauthorized, "Invalid MFA challenge token.");
        }

        if (DateTimeOffset.UtcNow > challenge.ExpiresAt)
        {
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.Unauthorized, "MFA challenge expired.");
        }

        if (!Guid.TryParse(challenge.UserId, out var userId))
        {
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.ValidationError, "Invalid challenge user identifier.");
        }

        var entity = await _dbContext.UserMfas.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity is null || !entity.MfaEnabled)
        {
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.ValidationError, "MFA is not enabled for this user.");
        }

        if (entity.LockedUntil.HasValue && entity.LockedUntil.Value > DateTimeOffset.UtcNow)
        {
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.ValidationError, "MFA verification is temporarily locked due to failed attempts.");
        }

        var success = false;
        var backupHashes = DeserializeBackupHashes(entity.BackupCodes).ToList();

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            success = ValidateTotp(UnprotectSecret(entity.TotpSecret), request.Code);
        }
        else if (!string.IsNullOrWhiteSpace(request.BackupCode))
        {
            var hash = HashCode(request.BackupCode);
            if (backupHashes.Remove(hash))
            {
                success = true;
                entity.BackupCodes = ProtectBackupHashes(backupHashes);
            }
        }

        if (!success)
        {
            var options = _mfaOptionsMonitor.CurrentValue;
            entity.FailedAttempts += 1;
            if (entity.FailedAttempts >= Math.Max(1, options.MaxFailedAttempts))
            {
                entity.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, options.LockoutMinutes));
                entity.FailedAttempts = 0;
            }

            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result<MfaVerifyResultDto>.Fail(DomainErrorCodes.ValidationError, "Invalid MFA code.");
        }

        entity.FailedAttempts = 0;
        entity.LockedUntil = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var sessionHours = Math.Max(1, _mfaOptionsMonitor.CurrentValue.SessionTimeoutHours);
        var accessToken = _sessionTokenService.IssueAccessToken(challenge.UserId, challenge.Roles, sessionHours);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(sessionHours);

        _logger.LogInformation("MFA challenge verified for UserId={UserId}", userId);
        await WriteAuditAsync(
            userId.ToString(),
            "MFA_VERIFY",
            "USER",
            userId.ToString(),
            cancellationToken,
            $"{{\"remainingBackupCodes\":{backupHashes.Count}}}");

        return Result<MfaVerifyResultDto>.Ok(new MfaVerifyResultDto(
            accessToken,
            expiresAt,
            true,
            backupHashes.Count));
    }

    public async Task<Result<IReadOnlyList<string>>> RegenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.UserMfas.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity is null || !entity.MfaEnabled)
        {
            return Result<IReadOnlyList<string>>.Fail(DomainErrorCodes.ValidationError, "MFA is not enabled for this user.");
        }

        var backupCodes = GenerateBackupCodes(10);
        var hashes = backupCodes.Select(HashCode).ToList();

        entity.BackupCodes = ProtectBackupHashes(hashes);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("MFA backup codes regenerated for UserId={UserId}", userId);
        return Result<IReadOnlyList<string>>.Ok(backupCodes);
    }

    public async Task<Result> ResetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.UserMfas.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "MFA configuration not found for user.");
        }

        entity.TotpSecret = string.Empty;
        entity.BackupCodes = string.Empty;
        entity.MfaEnabled = false;
        entity.MfaEnrolledAt = null;
        entity.FailedAttempts = 0;
        entity.LockedUntil = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("MFA reset for UserId={UserId}", userId);
        await WriteAuditAsync(
            userId.ToString(),
            "MFA_RESET",
            "USER",
            userId.ToString(),
            cancellationToken,
            "{\"mfaEnabled\":false}");
        return Result.Ok();
    }

    public async Task<MfaUserStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.UserMfas.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return new MfaUserStatusDto(false, false, null, null);
        }

        var backupHashes = DeserializeBackupHashes(entity.BackupCodes);
        return new MfaUserStatusDto(
            true,
            entity.MfaEnabled,
            entity.MfaEnrolledAt,
            backupHashes.Count());
    }

    public bool IsMfaRequired(IReadOnlyList<string> roles)
    {
        var options = _mfaOptionsMonitor.CurrentValue;
        var required = options.RequiredRoles ?? [];

        return roles.Any(role => required.Any(requiredRole => string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)));
    }

    public int GetChallengeTimeoutMinutes()
        => Math.Max(1, _mfaOptionsMonitor.CurrentValue.ChallengeTimeoutMinutes);

    public int GetSessionTimeoutHours()
        => Math.Max(1, _mfaOptionsMonitor.CurrentValue.SessionTimeoutHours);

    private static IReadOnlyList<string> GenerateBackupCodes(int count)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var result = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(12);
            var chars = bytes.Select(x => alphabet[x % alphabet.Length]).ToArray();
            result.Add(new string(chars));
        }

        return result;
    }

    private static string HashCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private bool ValidateTotp(string secretBase32, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        return totp.VerifyTotp(code.Trim(), out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    private static string GenerateQrCodeDataUri(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(qrData);
        var bytes = pngQr.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static string BuildOtpAuthUri(string secretBase32, string userLabel, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{userLabel}");
        var issuerPart = Uri.EscapeDataString(issuer);
        var secretPart = Uri.EscapeDataString(secretBase32);
        return $"otpauth://totp/{label}?secret={secretPart}&issuer={issuerPart}&algorithm=SHA1&digits=6&period=30";
    }

    private string UnprotectSecret(string protectedSecret)
    {
        return _secretProtector.Unprotect(protectedSecret);
    }

    private string ProtectBackupHashes(IReadOnlyList<string> hashes)
    {
        return _backupProtector.Protect(JsonSerializer.Serialize(hashes));
    }

    private IEnumerable<string> DeserializeBackupHashes(string protectedBackupCodes)
    {
        if (string.IsNullOrWhiteSpace(protectedBackupCodes))
        {
            return [];
        }

        var json = _backupProtector.Unprotect(protectedBackupCodes);
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private async Task WriteAuditAsync(
        string? userId,
        string action,
        string resource,
        string? resourceId,
        CancellationToken cancellationToken,
        string detailsJson)
    {
        if (_auditLogService is null)
        {
            return;
        }

        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                userId,
                action,
                resource,
                resourceId,
                "system",
                "mfa-service",
                DateTimeOffset.UtcNow,
                detailsJson),
            cancellationToken);
    }
}

public sealed record MfaEnrollmentDto(
    string ManualSecret,
    string QrCodeDataUri,
    IReadOnlyList<string> BackupCodes,
    bool MfaEnabled);

public sealed record MfaVerifyRequest(
    string ChallengeToken,
    string? Code,
    string? BackupCode);

public sealed record MfaVerifyResultDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    bool MfaVerified,
    int RemainingBackupCodes);

public sealed record MfaUserStatusDto(
    bool HasEnrollment,
    bool MfaEnabled,
    DateTimeOffset? MfaEnrolledAt,
    int? BackupCodeCount);
