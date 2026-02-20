using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class TransactionExportServiceTests
{
    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenCsvRequested_ShouldCreateFileAndPersistHistory()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var reader = new FakeEventReader(SeedRows(3));
        var sftp = new FakeSftpClient();

        var sut = CreateService(db, reader, sftp, tempDir);
        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        result.Succeeded.Should().BeTrue();
        result.RowCount.Should().Be(3);
        result.FilePaths.Should().ContainSingle();
        File.Exists(result.FilePaths[0]).Should().BeTrue();

        var history = await db.TransactionExports.SingleAsync();
        history.Status.Should().Be(TransactionExportStatus.Completed);
        history.RowCount.Should().Be(3);

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenJsonRequested_ShouldCreateJsonFile()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var sut = CreateService(db, new FakeEventReader(SeedRows(2)), new FakeSftpClient(), tempDir);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Json,
            "auditor",
            null));

        result.Succeeded.Should().BeTrue();
        result.FilePaths[0].Should().EndWith(".json");
        var content = await File.ReadAllTextAsync(result.FilePaths[0]);
        content.Should().Contain("eventId");

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenStartDateAfterEndDate_ShouldFail()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db, new FakeEventReader(SeedRows(1)), new FakeSftpClient(), CreateTempDirectory());

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(-1),
            TransactionExportFormat.Csv,
            "admin",
            null));

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(TransactionExportStatus.Failed);
        result.ErrorMessage.Should().Contain("StartDate");
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenCsvExceedsSizeLimit_ShouldSplitFiles()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var rows = SeedRows(8, payloadSize: 180);
        var sut = CreateService(db, new FakeEventReader(rows), new FakeSftpClient(), tempDir, maxFileSizeBytes: 512);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        result.Succeeded.Should().BeTrue();
        result.FilePaths.Count.Should().BeGreaterThan(1);
        result.FilePaths.Should().OnlyContain(x => x.Contains("-part"));

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenJsonExceedsSizeLimit_ShouldSplitFiles()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var rows = SeedRows(8, payloadSize: 180);
        var sut = CreateService(db, new FakeEventReader(rows), new FakeSftpClient(), tempDir, maxFileSizeBytes: 512);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Json,
            "admin",
            null));

        result.Succeeded.Should().BeTrue();
        result.FilePaths.Count.Should().BeGreaterThan(1);
        result.FilePaths.Should().OnlyContain(x => x.EndsWith(".json"));

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenSftpUploadRequested_ShouldUploadAndDeleteLocalFiles()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var sftp = new FakeSftpClient();
        var sut = CreateService(db, new FakeEventReader(SeedRows(2)), sftp, tempDir);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            new TransactionExportSftpDestination("host", "user", "pass", "/exports", true)));

        result.Succeeded.Should().BeTrue();
        sftp.UploadCalls.Should().Be(1);
        result.FilePaths.Should().OnlyContain(path => !File.Exists(path));

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenSftpDeleteDisabled_ShouldKeepLocalFiles()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var sftp = new FakeSftpClient();
        var sut = CreateService(db, new FakeEventReader(SeedRows(2)), sftp, tempDir);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            new TransactionExportSftpDestination("host", "user", "pass", "/exports", false)));

        result.Succeeded.Should().BeTrue();
        sftp.UploadCalls.Should().Be(1);
        result.FilePaths.Should().OnlyContain(path => File.Exists(path));

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_WhenReaderThrows_ShouldFailAndPersistError()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var reader = new FakeEventReader([]) { ExceptionToThrow = new InvalidOperationException("reader failed") };
        var sut = CreateService(db, reader, new FakeSftpClient(), tempDir);

        var result = await sut.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(TransactionExportStatus.Failed);
        result.ErrorMessage.Should().Contain("reader failed");

        var history = await db.TransactionExports.SingleAsync();
        history.Status.Should().Be(TransactionExportStatus.Failed);
        history.ErrorMessage.Should().Contain("reader failed");

        DeleteDirectory(tempDir);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task GetHistoryAsync_ShouldReturnNewestFirst()
    {
        await using var db = CreateDbContext();
        db.TransactionExports.Add(new TransactionExport
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-2),
            EndDate = DateTimeOffset.UtcNow.AddDays(-1),
            Format = TransactionExportFormat.Csv,
            RowCount = 1,
            FilePath = "a.csv",
            Status = TransactionExportStatus.Completed,
            ExportedBy = "old",
            ExportedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        db.TransactionExports.Add(new TransactionExport
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow,
            Format = TransactionExportFormat.Json,
            RowCount = 2,
            FilePath = "b.json",
            Status = TransactionExportStatus.Completed,
            ExportedBy = "new",
            ExportedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db, new FakeEventReader([]), new FakeSftpClient(), CreateTempDirectory());
        var history = await sut.GetHistoryAsync(10);

        history.Should().HaveCount(2);
        history[0].ExportedBy.Should().Be("new");
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task GetHistoryAsync_WhenLimitMissing_ShouldUseConfiguredDefault()
    {
        await using var db = CreateDbContext();
        for (var i = 0; i < 5; i++)
        {
            db.TransactionExports.Add(new TransactionExport
            {
                StartDate = DateTimeOffset.UtcNow.AddDays(-1),
                EndDate = DateTimeOffset.UtcNow,
                Format = TransactionExportFormat.Csv,
                RowCount = i,
                FilePath = $"{i}.csv",
                Status = TransactionExportStatus.Completed,
                ExportedBy = "admin",
                ExportedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }

        await db.SaveChangesAsync();
        var sut = CreateService(db, new FakeEventReader([]), new FakeSftpClient(), CreateTempDirectory(), defaultHistoryLimit: 3);

        var history = await sut.GetHistoryAsync(null);

        history.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportAsync_ShouldWriteAuditLogsOnSuccessAndFailure()
    {
        await using var db = CreateDbContext();
        var tempDir = CreateTempDirectory();
        var auditMock = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        auditMock.Setup(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var successService = CreateService(db, new FakeEventReader(SeedRows(1)), new FakeSftpClient(), tempDir, auditLogService: auditMock.Object);
        _ = await successService.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        var failedReader = new FakeEventReader([]) { ExceptionToThrow = new InvalidOperationException("boom") };
        var failedService = CreateService(db, failedReader, new FakeSftpClient(), tempDir, auditLogService: auditMock.Object);
        _ = await failedService.ExportAsync(new TransactionExportExecuteCommand(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            TransactionExportFormat.Csv,
            "admin",
            null));

        auditMock.Verify(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        DeleteDirectory(tempDir);
    }

    private static TransactionExportService CreateService(
        WarehouseDbContext db,
        ITransactionEventReader reader,
        ITransactionExportSftpClient sftpClient,
        string exportRoot,
        long maxFileSizeBytes = 1024 * 1024,
        int defaultHistoryLimit = 100,
        ISecurityAuditLogService? auditLogService = null)
    {
        var options = Options.Create(new TransactionExportOptions
        {
            ExportRootPath = exportRoot,
            MaxFileSizeBytes = maxFileSizeBytes,
            DefaultHistoryLimit = defaultHistoryLimit
        });

        return new TransactionExportService(
            db,
            reader,
            sftpClient,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionExportService>.Instance,
            auditLogService);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"transaction-export-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "prd1631", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static IReadOnlyList<TransactionEventExportRow> SeedRows(int count, int payloadSize = 20)
    {
        return Enumerable.Range(1, count)
            .Select(i => new TransactionEventExportRow(
                Guid.NewGuid(),
                "stock_moved",
                DateTimeOffset.UtcNow.AddMinutes(-i),
                $"stock-ledger-{i}",
                "user-1",
                $"{{\"delta\":{i},\"note\":\"{new string('X', payloadSize)}\"}}"))
            .ToList();
    }

    private sealed class FakeEventReader : ITransactionEventReader
    {
        private readonly IReadOnlyList<TransactionEventExportRow> _rows;

        public FakeEventReader(IReadOnlyList<TransactionEventExportRow> rows)
        {
            _rows = rows;
        }

        public Exception? ExceptionToThrow { get; init; }

        public Task<IReadOnlyList<TransactionEventExportRow>> ReadAsync(
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(_rows);
        }
    }

    private sealed class FakeSftpClient : ITransactionExportSftpClient
    {
        public int UploadCalls { get; private set; }

        public Task UploadAsync(
            IReadOnlyList<string> localFilePaths,
            TransactionExportSftpDestination destination,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            localFilePaths.Should().NotBeEmpty();
            destination.Host.Should().NotBeNullOrWhiteSpace();
            return Task.CompletedTask;
        }
    }
}
