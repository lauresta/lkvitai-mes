using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;
using LKvitai.MES.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class DashboardClientTests
{
    [Fact]
    public async Task GetStockSummaryAsync_WhenApiReturnsProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                """
                {
                  "type": "https://errors/INTERNAL_ERROR",
                  "title": "Server error",
                  "status": 500,
                  "detail": "Query failed",
                  "traceId": "trace-dashboard-500",
                  "errorCode": "INTERNAL_ERROR"
                }
                """)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        // Act
        var act = async () => await sut.GetStockSummaryAsync();

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(500);
        exception.Which.TraceId.Should().Be("trace-dashboard-500");
        exception.Which.ErrorCode.Should().Be("INTERNAL_ERROR");
        exception.Which.UserMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetProjectionHealthAsync_WhenResponseIsArray_MapsKnownProjections()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  { "projectionName": "LocationBalanceProjection", "lagSeconds": 4.2, "lastUpdated": "2026-02-12T21:40:01Z" },
                  { "projectionName": "AvailableStockProjection", "lagSeconds": 11.6, "lastUpdated": "2026-02-12T21:40:02Z" }
                ]
                """)
        };

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        // Act
        var result = await sut.GetProjectionHealthAsync();

        // Assert
        result.LocationBalanceLag.Should().Be(4.2);
        result.AvailableStockLag.Should().Be(11.6);
        result.LastRebuildLB.Should().Be(new DateTime(2026, 2, 12, 21, 40, 1, DateTimeKind.Utc));
        result.LastRebuildAS.Should().Be(new DateTime(2026, 2, 12, 21, 40, 2, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetRecentActivityAsync_WhenWrappedPayload_ReturnsMovements()
    {
        // Arrange
        var movementId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "movements": [
                    {
                      "movementId": "{{movementId}}",
                      "sku": "SKU-01",
                      "quantity": 12.5,
                      "fromLocation": "A01",
                      "toLocation": "B01",
                      "timestamp": "2026-02-09T10:15:00Z"
                    }
                  ]
                }
                """)
        };

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        // Act
        var result = await sut.GetRecentActivityAsync(10);

        // Assert
        result.Should().HaveCount(1);
        result[0].MovementId.Should().Be(movementId);
        result[0].SKU.Should().Be("SKU-01");
        result[0].Quantity.Should().Be(12.5m);
    }

    private static DashboardClient CreateSut(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient("WarehouseApi"))
            .Returns(client);

        return new DashboardClient(factory.Object);
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
