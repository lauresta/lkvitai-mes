using Hangfire;
using System.Linq;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public sealed class PiiEncryptionServiceTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrip_Works()
    {
        var encrypted = PiiEncryption.Encrypt("john@example.com");
        var decrypted = PiiEncryption.Decrypt(encrypted);

        Assert.StartsWith("enc:", encrypted);
        Assert.Equal("john@example.com", decrypted);
    }

    [Fact]
    public async Task RotateKeyAsync_PersistsMetadata_AndEnqueuesJob()
    {
        await using var db = CreateDbContext();

        var user = new Mock<ICurrentUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetCurrentUserId()).Returns("admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Loose);

        var jobs = new Mock<IBackgroundJobClient>(MockBehavior.Loose);

        var sut = new PiiEncryptionService(db, user.Object, audit.Object, jobs.Object);

        var result = await sut.RotateKeyAsync();

        Assert.NotEqual(result.PreviousKeyId, result.NewKeyId);
        Assert.Single(db.PiiEncryptionKeyRecords.Where(x => x.Active));
    }

    [Fact]
    public async Task ReencryptAllCustomersAsync_CompletesForExistingCustomers()
    {
        await using var db = CreateDbContext();

        db.Customers.Add(new Customer
        {
            Name = "John Doe",
            Email = "john@example.com",
            BillingAddress = new Address
            {
                Street = "Main",
                City = "Kaunas",
                State = "KA",
                ZipCode = "12345",
                Country = "LT"
            }
        });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetCurrentUserId()).Returns("admin");

        var audit = new Mock<ISecurityAuditLogService>(MockBehavior.Loose);
        var jobs = new Mock<IBackgroundJobClient>(MockBehavior.Loose);

        var sut = new PiiEncryptionService(db, user.Object, audit.Object, jobs.Object);

        var updated = await sut.ReencryptAllCustomersAsync();

        Assert.Equal(1, updated);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"pii-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
