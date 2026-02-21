using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record CaptureSignatureCommand(
    string Action,
    string ResourceId,
    string SignatureText,
    string Meaning,
    string UserId,
    string IpAddress,
    string? Password);

public interface IElectronicSignatureService
{
    Task<ElectronicSignature> CaptureAsync(CaptureSignatureCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ElectronicSignature>> GetByResourceAsync(string resourceId, CancellationToken cancellationToken = default);

    Task<VerifyHashChainResponse> VerifyHashChainAsync(CancellationToken cancellationToken = default);
}

public sealed record VerifyHashChainResponse(bool Valid, int SignatureCount, string? ErrorMessage = null);

public sealed class ElectronicSignatureService : IElectronicSignatureService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<ElectronicSignatureService> _logger;

    public ElectronicSignatureService(WarehouseDbContext dbContext, ILogger<ElectronicSignatureService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ElectronicSignature> CaptureAsync(CaptureSignatureCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new InvalidOperationException("Password re-entry is required.");
        }

        var previous = await _dbContext.ElectronicSignatures
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var previousHash = previous?.CurrentHash ?? "GENESIS";
        var timestamp = DateTimeOffset.UtcNow;
        var currentHash = ComputeHash(previousHash, command, timestamp);

        var entity = new ElectronicSignature
        {
            UserId = command.UserId,
            Action = command.Action,
            ResourceId = command.ResourceId,
            SignatureText = command.SignatureText,
            Meaning = command.Meaning,
            IpAddress = command.IpAddress,
            Timestamp = timestamp,
            PreviousHash = previousHash,
            CurrentHash = currentHash
        };

        _dbContext.ElectronicSignatures.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task<IReadOnlyList<ElectronicSignature>> GetByResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ElectronicSignatures
            .AsNoTracking()
            .Where(x => x.ResourceId == resourceId)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<VerifyHashChainResponse> VerifyHashChainAsync(CancellationToken cancellationToken = default)
    {
        var signatures = await _dbContext.ElectronicSignatures
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (signatures.Count == 0)
        {
            return new VerifyHashChainResponse(true, 0);
        }

        string expectedPrevious = "GENESIS";
        foreach (var sig in signatures)
        {
            var recomputed = ComputeHash(expectedPrevious, sig);
            if (!string.Equals(sig.PreviousHash, expectedPrevious, StringComparison.Ordinal) ||
                !string.Equals(sig.CurrentHash, recomputed, StringComparison.Ordinal))
            {
                return new VerifyHashChainResponse(false, signatures.Count, $"Mismatch at signature {sig.Id}");
            }

            expectedPrevious = sig.CurrentHash;
        }

        return new VerifyHashChainResponse(true, signatures.Count);
    }

    private static string ComputeHash(string previousHash, CaptureSignatureCommand command, DateTimeOffset timestamp)
    {
        return ComputeHash(previousHash, command.UserId, command.Action, command.ResourceId, command.SignatureText, command.Meaning, timestamp, command.IpAddress);
    }

    private static string ComputeHash(string previousHash, ElectronicSignature sig)
    {
        return ComputeHash(previousHash, sig.UserId, sig.Action, sig.ResourceId, sig.SignatureText, sig.Meaning, sig.Timestamp, sig.IpAddress);
    }

    private static string ComputeHash(
        string previousHash,
        string userId,
        string action,
        string resourceId,
        string signatureText,
        string meaning,
        DateTimeOffset timestamp,
        string ipAddress)
    {
        var payload = $"{previousHash}|{userId}|{action}|{resourceId}|{signatureText}|{meaning}|{timestamp:o}|{ipAddress}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
