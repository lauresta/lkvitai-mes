using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class TransactionExportIntegrationTests
{
    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportCsv_ShouldWriteExpectedHeaderAndRows()
    {
        await using var db = CreateDbContext();
        var exportDir = Path.Combine(Path.GetTempPath(), "prd1631-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportDir);

        var rows = new List<TransactionEventExportRow>
        {
            new(Guid.NewGuid(), "stock_moved", DateTimeOffset.UtcNow.AddMinutes(-2), "stream-1", "user-1", "{\"value\":1}"),
            new(Guid.NewGuid(), "reservation_created", DateTimeOffset.UtcNow.AddMinutes(-1), "stream-2", "user-2", "{\"value\":2}")
        };

        var service = new TransactionExportService(
            db,
            new StaticReader(rows),
            new NoOpSftpClient(),
            Options.Create(new TransactionExportOptions
            {
                ExportRootPath = exportDir,
                MaxFileSizeBytes = 1024 * 1024,
                DefaultHistoryLimit = 100
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionExportService>.Instance);

        var result = await service.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        result.Succeeded.Should().BeTrue();
        result.RowCount.Should().Be(2);
        result.FilePaths.Should().ContainSingle();

        var csv = await File.ReadAllTextAsync(result.FilePaths[0]);
        csv.Should().Contain("EventId,EventType,Timestamp,AggregateId,UserId,Payload");
        csv.Should().Contain("stock_moved");
        csv.Should().Contain("reservation_created");

        var history = await db.TransactionExports.SingleAsync();
        history.Status.Should().Be(TransactionExportStatus.Completed);
        history.RowCount.Should().Be(2);

        Directory.Delete(exportDir, true);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"transaction-export-integration-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private sealed class StaticReader : ITransactionEventReader
    {
        private readonly IReadOnlyList<TransactionEventExportRow> _rows;

        public StaticReader(IReadOnlyList<TransactionEventExportRow> rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyList<TransactionEventExportRow>> ReadAsync(
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rows);
        }
    }

    private sealed class NoOpSftpClient : ITransactionExportSftpClient
    {
        public Task UploadAsync(
            IReadOnlyList<string> localFilePaths,
            TransactionExportSftpDestination destination,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
