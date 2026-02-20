using Hangfire;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public sealed class GdprErasureServiceTests
{
    [Fact]
    public async Task RequestAsync_CreatesPendingRequest()
    {
        await using var db = CreateDbContext();
        var customerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "John Doe",
            Email = "john@example.com",
            BillingAddress = new Address()
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var created = await sut.RequestAsync(new CreateErasureRequest(customerId, "No longer using service"));

        Assert.Equal("PENDING", created.Status);
        Assert.Equal(customerId, created.CustomerId);
    }

    [Fact]
    public async Task ExecuteAnonymizationAsync_MasksCustomerData()
    {
        await using var db = CreateDbContext();
        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "123",
            BillingAddress = new Address
            {
                Street = "Main",
                City = "City",
                State = "ST",
                ZipCode = "12345",
                Country = "LT"
            },
            DefaultShippingAddress = new Address
            {
                Street = "Ship",
                City = "City",
                State = "ST",
                ZipCode = "12345",
                Country = "LT"
            }
        });

        var erasure = new ErasureRequest
        {
            CustomerId = customerId,
            Reason = "Request",
            RequestedBy = "user",
            Status = ErasureRequestStatus.Approved
        };

        db.ErasureRequests.Add(erasure);
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var processed = await sut.ExecuteAnonymizationAsync(erasure.Id);

        var customer = await db.Customers.FirstAsync(x => x.Id == customerId);
        var request = await db.ErasureRequests.FirstAsync(x => x.Id == erasure.Id);

        Assert.Equal(1, processed);
        Assert.StartsWith("Customer-", customer.Name);
        Assert.Equal("***@***.com", customer.Email);
        Assert.Equal(CustomerStatus.Inactive, customer.Status);
        Assert.Equal(ErasureRequestStatus.Completed, request.Status);
        Assert.NotNull(request.CompletedAt);
    }

    private static GdprErasureService CreateService(WarehouseDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Loose);
        var jobs = new Mock<IBackgroundJobClient>(MockBehavior.Loose);

        return new GdprErasureService(db, currentUser.Object, audit.Object, jobs.Object);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"gdpr-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
