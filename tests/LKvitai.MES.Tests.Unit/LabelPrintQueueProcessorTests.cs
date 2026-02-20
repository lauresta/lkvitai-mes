using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class LabelPrintQueueProcessorTests
{
    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void Enqueue_ShouldCreatePendingQueueItem()
    {
        var store = new InMemoryLabelPrintQueueStore();

        var item = store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });

        item.Status.Should().Be(PrintQueueStatus.Pending);
        item.RetryCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void Enqueue_ShouldPersistDataJson()
    {
        var store = new InMemoryLabelPrintQueueStore();

        var item = store.Enqueue("ITEM", new Dictionary<string, string> { ["ItemSKU"] = "RM-0001" });

        item.DataJson.Should().Contain("RM-0001");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_WhenPrintSucceeds_ShouldMarkCompleted()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });

        await context.Processor.ProcessPendingAsync();

        var saved = context.Store.Get(item.Id)!;
        saved.Status.Should().Be(PrintQueueStatus.Completed);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_WhenPrintFails_ShouldIncrementRetryAndRemainPending()
    {
        var context = CreateContext(new FailingPrinterClient(new InvalidOperationException("offline")));
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });

        await context.Processor.ProcessPendingAsync();

        var saved = context.Store.Get(item.Id)!;
        saved.Status.Should().Be(PrintQueueStatus.Pending);
        saved.RetryCount.Should().Be(1);
        saved.ErrorMessage.Should().Contain("offline");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_WhenRetryReachesTen_ShouldMarkFailed()
    {
        var context = CreateContext(new FailingPrinterClient(new InvalidOperationException("offline")));
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });
        item.RetryCount = 9;
        context.Store.Save(item);

        await context.Processor.ProcessPendingAsync();

        var saved = context.Store.Get(item.Id)!;
        saved.Status.Should().Be(PrintQueueStatus.Failed);
        saved.RetryCount.Should().Be(10);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryNowAsync_WhenMissing_ShouldReturnNotFoundResult()
    {
        var context = CreateContext(new SuccessfulPrinterClient());

        var result = await context.Processor.RetryNowAsync(Guid.NewGuid());

        result.Found.Should().BeFalse();
        result.Item.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryNowAsync_WhenSuccess_ShouldMarkCompleted()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });

        var result = await context.Processor.RetryNowAsync(item.Id);

        result.Found.Should().BeTrue();
        result.Item!.Status.Should().Be(PrintQueueStatus.Completed);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryNowAsync_WhenFails_ShouldIncrementRetry()
    {
        var context = CreateContext(new FailingPrinterClient(new InvalidOperationException("still offline")));
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });

        var result = await context.Processor.RetryNowAsync(item.Id);

        result.Found.Should().BeTrue();
        result.Item!.RetryCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryNowAsync_WhenFailedAndFailsAgain_ShouldStayFailed()
    {
        var context = CreateContext(new FailingPrinterClient(new InvalidOperationException("offline")));
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "R1-C1-L1" });
        item.Status = PrintQueueStatus.Failed;
        item.RetryCount = 10;
        context.Store.Save(item);

        var result = await context.Processor.RetryNowAsync(item.Id);

        result.Item!.Status.Should().Be(PrintQueueStatus.Failed);
        result.Item.RetryCount.Should().Be(11);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void GetPendingAndFailed_ShouldExcludeCompletedItems()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var completed = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" });
        completed.Status = PrintQueueStatus.Completed;
        context.Store.Save(completed);

        var pending = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "B" });
        pending.Status = PrintQueueStatus.Pending;
        context.Store.Save(pending);

        var failed = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "C" });
        failed.Status = PrintQueueStatus.Failed;
        context.Store.Save(failed);

        var items = context.Processor.GetPendingAndFailed();

        items.Should().HaveCount(2);
        items.Should().OnlyContain(x => x.Status == PrintQueueStatus.Pending || x.Status == PrintQueueStatus.Failed);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_ShouldReturnProcessedCount()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" });
        context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "B" });

        var processed = await context.Processor.ProcessPendingAsync();

        processed.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_ShouldSetLastAttemptAt()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" });

        await context.Processor.ProcessPendingAsync();

        var saved = context.Store.Get(item.Id)!;
        saved.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_WhenSuccess_ShouldClearErrorMessage()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var item = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" }, "old error");

        await context.Processor.ProcessPendingAsync();

        var saved = context.Store.Get(item.Id)!;
        saved.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessPendingAsync_ShouldIgnoreNonPendingStatuses()
    {
        var context = CreateContext(new SuccessfulPrinterClient());
        var failed = context.Store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" });
        failed.Status = PrintQueueStatus.Failed;
        context.Store.Save(failed);

        var processed = await context.Processor.ProcessPendingAsync();

        processed.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryNowAsync_ShouldUseTemplateEngineRendering()
    {
        var printer = new CapturingPrinterClient();
        var context = CreateContext(printer);
        var item = context.Store.Enqueue("ITEM", new Dictionary<string, string>
        {
            ["ItemSKU"] = "RM-0001",
            ["Description"] = "Bolt"
        });

        await context.Processor.RetryNowAsync(item.Id);

        printer.LastPayload.Should().Contain("RM-0001");
        printer.LastPayload.Should().Contain("^BC");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void StoreList_ShouldReturnItemsOrderedByCreatedAt()
    {
        var store = new InMemoryLabelPrintQueueStore();
        var first = store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "A" });
        Thread.Sleep(5);
        var second = store.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "B" });

        var list = store.List(_ => true);

        list.First().Id.Should().Be(first.Id);
        list.Last().Id.Should().Be(second.Id);
    }

    private static TestContext CreateContext(ILabelPrinterClient printerClient)
    {
        var store = new InMemoryLabelPrintQueueStore();
        var engine = new LabelTemplateEngine(new ConfigurationBuilder().AddInMemoryCollection().Build());
        var processor = new LabelPrintQueueProcessor(
            store,
            printerClient,
            engine,
            Mock.Of<ILogger<LabelPrintQueueProcessor>>());

        return new TestContext(store, processor);
    }

    private sealed record TestContext(
        InMemoryLabelPrintQueueStore Store,
        LabelPrintQueueProcessor Processor);

    private sealed class SuccessfulPrinterClient : ILabelPrinterClient
    {
        public Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingPrinterClient : ILabelPrinterClient
    {
        public string LastPayload { get; private set; } = string.Empty;

        public Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
        {
            LastPayload = zplPayload;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPrinterClient : ILabelPrinterClient
    {
        private readonly Exception _exception;

        public FailingPrinterClient(Exception exception)
        {
            _exception = exception;
        }

        public Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
