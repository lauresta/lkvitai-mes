using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminAuditLogsControllerTests
{
    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task GetAsync_ShouldReturnOkWithRows()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        service.Setup(x => x.QueryAsync(It.IsAny<SecurityAuditLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SecurityAuditLogDto(1, "u1", "CREATE_ITEM", "ITEM", "1", "ip", "ua", DateTimeOffset.UtcNow, "{}")]);

        var sut = new AdminAuditLogsController(service.Object);
        var result = await sut.GetAsync();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task GetAsync_ShouldPassFiltersToService()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        SecurityAuditLogQuery? captured = null;
        service.Setup(x => x.QueryAsync(It.IsAny<SecurityAuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Callback<SecurityAuditLogQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync([]);

        var sut = new AdminAuditLogsController(service.Object);
        _ = await sut.GetAsync(userId: "u1", action: "CREATE_ITEM", resource: "ITEM", startDate: DateTimeOffset.UtcNow.AddDays(-1), endDate: DateTimeOffset.UtcNow, limit: 50);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be("u1");
        captured.Action.Should().Be("CREATE_ITEM");
        captured.Resource.Should().Be("ITEM");
        captured.Limit.Should().Be(50);
    }
}
