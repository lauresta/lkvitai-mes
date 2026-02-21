using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ProjectionsClientTests
{
    [Fact]
    public async Task GetProjectionLagAsync_WhenHealthReturns503WithProjectionPayload_ReturnsRows()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                """
                {
                  "status": "Degraded",
                  "projectionStatus": {
                    "AvailableStockProjection": {
                      "status": "Unhealthy",
                      "lagSeconds": 120.5,
                      "lagEvents": 400,
                      "lastUpdated": "2026-02-12T10:00:00Z"
                    }
                  }
                }
                """)
        };

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        var rows = await sut.GetProjectionLagAsync();

        rows.Should().HaveCount(1);
        rows[0].ProjectionName.Should().Be("AvailableStockProjection");
        rows[0].Status.Should().Be("Unhealthy");
        rows[0].LagSeconds.Should().Be(120.5);
        rows[0].LagEvents.Should().Be(400);
    }

    [Fact]
    public async Task GetProjectionLagAsync_WhenProblemDetailsResponse_ThrowsApiException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                """
                {
                  "type": "https://errors/INTERNAL_ERROR",
                  "title": "Service unavailable",
                  "status": 503,
                  "traceId": "trace-health-503"
                }
                """)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        var act = async () => await sut.GetProjectionLagAsync();

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(503);
        exception.Which.TraceId.Should().Be("trace-health-503");
    }

    [Fact]
    public async Task GetProjectionLagAsync_WhenPayloadUsesPascalCase_ParsesProjectionStatus()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "ProjectionStatus": {
                    "LocationBalanceProjection": {
                      "Status": "Healthy",
                      "LagSeconds": 0.2,
                      "LagEvents": 0,
                      "LastUpdated": "2026-02-12T10:05:00Z"
                    }
                  }
                }
                """)
        };

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        var rows = await sut.GetProjectionLagAsync();

        rows.Should().HaveCount(1);
        rows[0].ProjectionName.Should().Be("LocationBalanceProjection");
        rows[0].Status.Should().Be("Healthy");
        rows[0].LagSeconds.Should().BeGreaterThanOrEqualTo(0);
        rows[0].LagEvents.Should().Be(0);
    }

    private static ProjectionsClient CreateSut(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient("WarehouseApi"))
            .Returns(client);

        return new ProjectionsClient(factory.Object);
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
