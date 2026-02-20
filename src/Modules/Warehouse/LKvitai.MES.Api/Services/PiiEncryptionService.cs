using System.Text.Json;
using System.Linq;
using Hangfire;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface IPiiEncryptionService
{
    Task<KeyRotationResultDto> RotateKeyAsync(CancellationToken cancellationToken = default);
    Task<int> ReencryptAllCustomersAsync(CancellationToken cancellationToken = default);
}

public sealed class PiiEncryptionService : IPiiEncryptionService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityAuditLogService _auditLogService;
    private readonly IBackgroundJobClient _backgroundJobs;

    public PiiEncryptionService(
        WarehouseDbContext dbContext,
        ICurrentUserService currentUserService,
        ISecurityAuditLogService auditLogService,
        IBackgroundJobClient backgroundJobs)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<KeyRotationResultDto> RotateKeyAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var currentKeyId = PiiEncryption.ActiveKeyId;
        var newKeyId = PiiEncryption.RotateToNewRuntimeKey();

        var keys = await _dbContext.PiiEncryptionKeyRecords.ToListAsync(cancellationToken);

        foreach (var key in keys.Where(x => x.Active))
        {
            key.Active = false;
            key.GraceUntil = now.AddDays(30);
        }

        keys.Add(new PiiEncryptionKeyRecord
        {
            KeyId = newKeyId,
            Active = true,
            ActivatedAt = now,
            CreatedAt = now
        });

        _dbContext.PiiEncryptionKeyRecords.AddRange(keys.Where(x => x.Id == 0));
        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobId = _backgroundJobs.Enqueue<PiiReencryptionJob>(x => x.ExecuteAsync(CancellationToken.None));

        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                "PII_KEY_ROTATED",
                "ENCRYPTION",
                newKeyId,
                "system",
                "encryption-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(new
                {
                    PreviousKeyId = currentKeyId,
                    NewKeyId = newKeyId,
                    JobId = jobId
                })),
            cancellationToken);

        return new KeyRotationResultDto(currentKeyId, newKeyId, jobId, now.AddDays(30));
    }

    public async Task<int> ReencryptAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        const int batchSize = 500;
        var updated = 0;
        var offset = 0;

        while (true)
        {
            var rows = await _dbContext.Customers
                .OrderBy(x => x.Id)
                .Skip(offset)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                break;
            }

            foreach (var customer in rows)
            {
                // Force value converter write-path for encrypted properties.
                var entry = _dbContext.Entry(customer);
                entry.Property(x => x.Name).IsModified = true;
                entry.Property(x => x.Email).IsModified = true;

                var billing = entry.Reference(x => x.BillingAddress).TargetEntry;
                if (billing is not null)
                {
                    billing.Property(nameof(Address.Street)).IsModified = true;
                    billing.Property(nameof(Address.City)).IsModified = true;
                    billing.Property(nameof(Address.State)).IsModified = true;
                    billing.Property(nameof(Address.ZipCode)).IsModified = true;
                    billing.Property(nameof(Address.Country)).IsModified = true;
                }

                var shipping = entry.Reference(x => x.DefaultShippingAddress).TargetEntry;
                if (shipping is not null)
                {
                    shipping.Property(nameof(Address.Street)).IsModified = true;
                    shipping.Property(nameof(Address.City)).IsModified = true;
                    shipping.Property(nameof(Address.State)).IsModified = true;
                    shipping.Property(nameof(Address.ZipCode)).IsModified = true;
                    shipping.Property(nameof(Address.Country)).IsModified = true;
                }
            }

            updated += rows.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);
            offset += rows.Count;
        }

        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                "PII_REENCRYPTION_COMPLETED",
                "ENCRYPTION",
                PiiEncryption.ActiveKeyId,
                "system",
                "encryption-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(new { UpdatedCustomers = updated })),
            cancellationToken);

        return updated;
    }
}

public sealed class PiiReencryptionJob
{
    private readonly IPiiEncryptionService _service;

    public PiiReencryptionJob(IPiiEncryptionService service)
    {
        _service = service;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _service.ReencryptAllCustomersAsync(cancellationToken);
    }
}

public sealed record KeyRotationResultDto(
    string PreviousKeyId,
    string NewKeyId,
    string JobId,
    DateTimeOffset GraceUntil);
