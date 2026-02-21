using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net.Sockets;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class TcpLabelPrinterClientTests
{
    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenTransportSucceeds_ShouldCallOnce()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport);

        await sut.SendAsync("^XA^XZ");

        transport.CallCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenFirstAttemptFailsSecondSucceeds_ShouldRetryOnce()
    {
        var transport = new RecordingTransport(new SocketException(), null);
        var sut = CreateSut(transport, retryDelayMs: 0);

        await sut.SendAsync("^XA^XZ");

        transport.CallCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenFirstTwoAttemptsFailThirdSucceeds_ShouldRetryTwice()
    {
        var transport = new RecordingTransport(new TimeoutException(), new TimeoutException(), null);
        var sut = CreateSut(transport, retryDelayMs: 0);

        await sut.SendAsync("^XA^XZ");

        transport.CallCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenAllAttemptsFail_ShouldThrowUnavailableException()
    {
        var transport = new RecordingTransport(new SocketException(), new SocketException(), new SocketException());
        var sut = CreateSut(transport, retryDelayMs: 0);

        var action = async () => await sut.SendAsync("^XA^XZ");

        var ex = await action.Should().ThrowAsync<LabelPrinterUnavailableException>();
        ex.Which.Attempts.Should().Be(3);
        transport.CallCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenRetryCountIsOne_ShouldNotRetry()
    {
        var transport = new RecordingTransport(new SocketException());
        var sut = CreateSut(transport, retryCount: 1, retryDelayMs: 0);

        var action = async () => await sut.SendAsync("^XA^XZ");

        await action.Should().ThrowAsync<LabelPrinterUnavailableException>();
        transport.CallCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenRetryCountZero_ShouldDefaultToOneAttempt()
    {
        var transport = new RecordingTransport(new SocketException());
        var sut = CreateSut(transport, retryCount: 0, retryDelayMs: 0);

        var action = async () => await sut.SendAsync("^XA^XZ");

        await action.Should().ThrowAsync<LabelPrinterUnavailableException>();
        transport.CallCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenRetryCountNegative_ShouldDefaultToOneAttempt()
    {
        var transport = new RecordingTransport(new SocketException());
        var sut = CreateSut(transport, retryCount: -5, retryDelayMs: 0);

        var action = async () => await sut.SendAsync("^XA^XZ");

        await action.Should().ThrowAsync<LabelPrinterUnavailableException>();
        transport.CallCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_ShouldPassConfiguredHostAndPortToTransport()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport, printerIp: "192.168.1.100", printerPort: 9200);

        await sut.SendAsync("^XA^XZ");

        transport.LastHost.Should().Be("192.168.1.100");
        transport.LastPort.Should().Be(9200);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenPrinterIpMissing_ShouldUseLoopbackDefault()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport, printerIp: "  ");

        await sut.SendAsync("^XA^XZ");

        transport.LastHost.Should().Be("127.0.0.1");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenPortInvalid_ShouldUse9100()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport, printerPort: 0);

        await sut.SendAsync("^XA^XZ");

        transport.LastPort.Should().Be(9100);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_ShouldPassConfiguredTimeout()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport, socketTimeoutMs: 2500);

        await sut.SendAsync("^XA^XZ");

        transport.LastTimeout.Should().Be(TimeSpan.FromMilliseconds(2500));
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenTimeoutInvalid_ShouldUseFiveSeconds()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport, socketTimeoutMs: 0);

        await sut.SendAsync("^XA^XZ");

        transport.LastTimeout.Should().Be(TimeSpan.FromMilliseconds(5000));
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_ShouldPassZplPayloadToTransport()
    {
        var transport = new RecordingTransport();
        var sut = CreateSut(transport);
        const string zpl = "^XA^FO50,50^FDHELLO^FS^XZ";

        await sut.SendAsync(zpl);

        transport.LastPayload.Should().Be(zpl);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenCancellationRequested_ShouldThrowOperationCanceled()
    {
        var transport = new RecordingTransport(new OperationCanceledException());
        var sut = CreateSut(transport, retryCount: 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await sut.SendAsync("^XA^XZ", cts.Token);

        await action.Should().ThrowAsync<LabelPrinterUnavailableException>();
        transport.CallCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task SendAsync_WhenRetryDelayNegative_ShouldStillRetry()
    {
        var transport = new RecordingTransport(new SocketException(), null);
        var sut = CreateSut(transport, retryDelayMs: -100);

        await sut.SendAsync("^XA^XZ");

        transport.CallCount.Should().Be(2);
    }

    private static TcpLabelPrinterClient CreateSut(
        RecordingTransport transport,
        string printerIp = "127.0.0.1",
        int printerPort = 9100,
        int retryCount = 3,
        int retryDelayMs = 0,
        int socketTimeoutMs = 5000)
    {
        var options = Options.Create(new LabelPrintingConfig
        {
            PrinterIP = printerIp,
            PrinterPort = printerPort,
            RetryCount = retryCount,
            RetryDelayMs = retryDelayMs,
            SocketTimeoutMs = socketTimeoutMs
        });

        return new TcpLabelPrinterClient(
            options,
            transport,
            Mock.Of<ILogger<TcpLabelPrinterClient>>());
    }

    private sealed class RecordingTransport : ILabelPrinterTransport
    {
        private readonly Queue<Exception?> _responses;

        public RecordingTransport(params Exception?[] responses)
        {
            _responses = new Queue<Exception?>(responses);
        }

        public int CallCount { get; private set; }
        public string LastHost { get; private set; } = string.Empty;
        public int LastPort { get; private set; }
        public TimeSpan LastTimeout { get; private set; }
        public string LastPayload { get; private set; } = string.Empty;

        public Task SendAsync(
            string host,
            int port,
            string zplPayload,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastHost = host;
            LastPort = port;
            LastTimeout = timeout;
            LastPayload = zplPayload;

            if (_responses.TryDequeue(out var response) && response is not null)
            {
                throw response;
            }

            return Task.CompletedTask;
        }
    }
}
