using System.Text.Json;
using System.Linq;
using System.Linq.Expressions;
using Hangfire;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface IGdprErasureService
{
    Task<ErasureRequestDto> RequestAsync(CreateErasureRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErasureRequestDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<ErasureRequestDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ErasureRequestDto?> RejectAsync(Guid id, string rejectionReason, CancellationToken cancellationToken = default);
    Task<int> ExecuteAnonymizationAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class GdprErasureService : IGdprErasureService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityAuditLogService _auditLogService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public GdprErasureService(
        WarehouseDbContext dbContext,
        ICurrentUserService currentUserService,
        ISecurityAuditLogService auditLogService,
        IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<ErasureRequestDto> RequestAsync(CreateErasureRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
        {
            throw new KeyNotFoundException($"Customer '{request.CustomerId}' was not found.");
        }

        var entity = new ErasureRequest
        {
            CustomerId = request.CustomerId,
            Reason = request.Reason.Trim(),
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = _currentUserService.GetCurrentUserId(),
            Status = ErasureRequestStatus.Pending
        };

        _dbContext.ErasureRequests.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("GDPR_ERASURE_REQUESTED", entity.Id.ToString(), new { entity.CustomerId, entity.Reason }, cancellationToken);

        return ToDto(entity);
    }

    public async Task<IReadOnlyList<ErasureRequestDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ErasureRequests
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAt)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<ErasureRequestDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ErasureRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != ErasureRequestStatus.Pending)
        {
            throw new InvalidOperationException($"Erasure request '{id}' must be PENDING to approve.");
        }

        request.Status = ErasureRequestStatus.Approved;
        request.ApprovedAt = DateTimeOffset.UtcNow;
        request.ApprovedBy = _currentUserService.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobId = _backgroundJobClient.Enqueue<GdprErasureJob>(x => x.ExecuteAsync(id, CancellationToken.None));

        await WriteAuditAsync("GDPR_ERASURE_APPROVED", request.Id.ToString(), new { request.CustomerId, JobId = jobId }, cancellationToken);

        return ToDto(request);
    }

    public async Task<ErasureRequestDto?> RejectAsync(Guid id, string rejectionReason, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ErasureRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != ErasureRequestStatus.Pending)
        {
            throw new InvalidOperationException($"Erasure request '{id}' must be PENDING to reject.");
        }

        request.Status = ErasureRequestStatus.Rejected;
        request.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "Rejected by administrator." : rejectionReason.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("GDPR_ERASURE_REJECTED", request.Id.ToString(), new { request.CustomerId, request.RejectionReason }, cancellationToken);

        return ToDto(request);
    }

    public async Task<int> ExecuteAnonymizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ErasureRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null || request.Status != ErasureRequestStatus.Approved)
        {
            return 0;
        }

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return 0;
        }

        var originalEmail = customer.Email;
        var originalName = customer.Name;
        var anonymizedName = $"Customer-{customer.Id:N}";

        customer.Name = anonymizedName;
        customer.Email = "***@***.com";
        customer.Phone = null;
        customer.Status = CustomerStatus.Inactive;
        customer.BillingAddress = MaskAddress(customer.BillingAddress);

        if (customer.DefaultShippingAddress is not null)
        {
            customer.DefaultShippingAddress = MaskAddress(customer.DefaultShippingAddress);
        }

        var salesOrders = await _dbContext.SalesOrders
            .Where(x => x.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);

        foreach (var salesOrder in salesOrders)
        {
            if (salesOrder.ShippingAddress is not null)
            {
                salesOrder.ShippingAddress = MaskAddress(salesOrder.ShippingAddress);
            }
        }

        var outboundSummaries = await _dbContext.OutboundOrderSummaries
            .Where(x => x.CustomerName == originalName)
            .ToListAsync(cancellationToken);
        foreach (var summary in outboundSummaries)
        {
            summary.CustomerName = anonymizedName;
        }

        var shipmentSummaries = await _dbContext.ShipmentSummaries
            .Where(x => x.CustomerName == originalName)
            .ToListAsync(cancellationToken);
        foreach (var summary in shipmentSummaries)
        {
            summary.CustomerName = anonymizedName;
        }

        request.Status = ErasureRequestStatus.Completed;
        request.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("GDPR_ERASURE_COMPLETED", request.Id.ToString(), new
        {
            request.CustomerId,
            OriginalEmail = originalEmail,
            SalesOrders = salesOrders.Count,
            OutboundSummaries = outboundSummaries.Count,
            ShipmentSummaries = shipmentSummaries.Count
        }, cancellationToken);

        await WriteAuditAsync("GDPR_ERASURE_CONFIRMATION_SENT", request.Id.ToString(), new
        {
            Email = originalEmail,
            Message = "Erasure completed"
        }, cancellationToken);

        return 1;
    }

    private static readonly Expression<Func<ErasureRequest, ErasureRequestDto>> ToDtoExpression = x => new ErasureRequestDto(
        x.Id,
        x.CustomerId,
        x.Reason,
        x.Status.ToString().ToUpperInvariant(),
        x.RequestedAt,
        x.RequestedBy,
        x.ApprovedAt,
        x.ApprovedBy,
        x.CompletedAt,
        x.RejectionReason);

    private static ErasureRequestDto ToDto(ErasureRequest x)
    {
        return new ErasureRequestDto(
            x.Id,
            x.CustomerId,
            x.Reason,
            x.Status.ToString().ToUpperInvariant(),
            x.RequestedAt,
            x.RequestedBy,
            x.ApprovedAt,
            x.ApprovedBy,
            x.CompletedAt,
            x.RejectionReason);
    }

    private static Address MaskAddress(Address source)
    {
        return new Address
        {
            Street = "REDACTED",
            City = "REDACTED",
            State = "REDACTED",
            ZipCode = "REDACTED",
            Country = "REDACTED"
        };
    }

    private async Task WriteAuditAsync(string action, string resourceId, object payload, CancellationToken cancellationToken)
    {
        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                action,
                "GDPR_ERASURE",
                resourceId,
                "system",
                "gdpr-erasure-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(payload)),
            cancellationToken);
    }
}

public sealed class GdprErasureJob
{
    private readonly IGdprErasureService _service;

    public GdprErasureJob(IGdprErasureService service)
    {
        _service = service;
    }

    public Task ExecuteAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return _service.ExecuteAnonymizationAsync(requestId, cancellationToken);
    }
}

public sealed record CreateErasureRequest(Guid CustomerId, string Reason);

public sealed record ErasureRequestDto(
    Guid Id,
    Guid CustomerId,
    string Reason,
    string Status,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? CompletedAt,
    string? RejectionReason);
