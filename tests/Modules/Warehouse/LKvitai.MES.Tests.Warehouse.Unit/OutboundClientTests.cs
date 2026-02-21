using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class OutboundClientTests
{
    [Fact]
    public async Task GetOutboundOrderSummariesAsync_ShouldSendExpectedQueryString()
    {
        Uri? capturedUri = null;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  {
                    "id": "3ad8de0d-1741-41e8-b8ed-51c5b85c0a18",
                    "orderNumber": "OUT-0001",
                    "status": "PACKED",
                    "type": "SALES",
                    "itemCount": 2,
                    "orderDate": "2026-02-10T10:00:00Z"
                  }
                ]
                """)
        };

        var client = CreateHttpClient(request =>
        {
            capturedUri = request.RequestUri;
            return response;
        });

        var sut = CreateSut(client);

        var rows = await sut.GetOutboundOrderSummariesAsync(
            "PACKED",
            "Acme",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero));

        rows.Should().HaveCount(1);
        capturedUri.Should().NotBeNull();
        capturedUri!.Query.Should().Contain("status=PACKED");
        capturedUri.Query.Should().Contain("customer=Acme");
        capturedUri.Query.Should().Contain("dateFrom=");
        capturedUri.Query.Should().Contain("dateTo=");
    }

    [Fact]
    public async Task PackOrderAsync_WhenApiReturnsProblemDetails_ShouldThrowApiException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                  "type": "https://errors/VALIDATION_ERROR",
                  "title": "Validation error",
                  "status": 400,
                  "detail": "Missing items",
                  "traceId": "trace-pack-400",
                  "errorCode": "VALIDATION_ERROR"
                }
                """)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        var act = async () => await sut.PackOrderAsync(Guid.NewGuid(), new PackOrderRequestDto());

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.TraceId.Should().Be("trace-pack-400");
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task DispatchShipmentAsync_ShouldDeserializeResponse()
    {
        var shipmentId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "shipmentId": "{{shipmentId}}",
                  "shipmentNumber": "SHIP-0007",
                  "carrier": "FEDEX",
                  "trackingNumber": "TRK-777",
                  "dispatchedAt": "2026-02-11T08:00:00Z",
                  "dispatchedBy": "warehouse.user"
                }
                """)
        };

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        var result = await sut.DispatchShipmentAsync(shipmentId, new DispatchShipmentRequestDto());

        result.ShipmentId.Should().Be(shipmentId);
        result.ShipmentNumber.Should().Be("SHIP-0007");
        result.Carrier.Should().Be("FEDEX");
    }

    private static OutboundClient CreateSut(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient("WarehouseApi"))
            .Returns(client);

        return new OutboundClient(factory.Object);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
