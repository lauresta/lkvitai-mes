using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class StockClientTests
{
    [Fact]
    public async Task SearchAvailableStockAsync_BuildsExpectedQueryString()
    {
        // Arrange
        string? capturedPathAndQuery = null;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "items": [],
                  "totalCount": 0,
                  "pageNumber": 2,
                  "pageSize": 25
                }
                """)
        };

        var client = CreateHttpClient(request =>
        {
            capturedPathAndQuery = request.RequestUri?.PathAndQuery;
            return response;
        });

        var sut = CreateSut(client);

        // Act
        await sut.SearchAvailableStockAsync(
            warehouse: "WH-1",
            location: "A*",
            sku: "SKU*",
            includeVirtual: false,
            page: 2,
            pageSize: 25);

        // Assert
        capturedPathAndQuery.Should().NotBeNull();
        capturedPathAndQuery.Should().Contain("/api/warehouse/v1/stock/available?");
        capturedPathAndQuery.Should().Contain("includeVirtualLocations=false");
        capturedPathAndQuery.Should().Contain("pageNumber=2");
        capturedPathAndQuery.Should().Contain("pageSize=25");
        capturedPathAndQuery.Should().Contain("warehouse=WH-1");
        capturedPathAndQuery.Should().Contain("location=A%2A");
        capturedPathAndQuery.Should().Contain("sku=SKU%2A");
    }

    [Fact]
    public async Task SearchAvailableStockAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                  "type": "https://errors/VALIDATION_ERROR",
                  "title": "Validation failed",
                  "status": 400,
                  "traceId": "trace-stock-400",
                  "errorCode": "VALIDATION_ERROR",
                  "errors": {
                    "sku": [ "SKU is invalid." ]
                  }
                }
                """)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");

        var client = CreateHttpClient(_ => response);
        var sut = CreateSut(client);

        // Act
        var act = async () => await sut.SearchAvailableStockAsync(null, null, null);

        // Assert
        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.TraceId.Should().Be("trace-stock-400");
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetWarehousesAsync_CallsApiAndReturnsItems()
    {
        // Arrange
        string? capturedPathAndQuery = null;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "items": [
                    {
                      "id": "7d6b9d0f-7d4f-4ca8-a3a8-ff064ef17f47",
                      "code": "WH1",
                      "name": "Main Warehouse",
                      "description": "Primary",
                      "isVirtual": false,
                      "status": "Active"
                    }
                  ],
                  "totalCount": 1,
                  "pageNumber": 1,
                  "pageSize": 200
                }
                """)
        };

        var client = CreateHttpClient(request =>
        {
            capturedPathAndQuery = request.RequestUri?.PathAndQuery;
            return response;
        });
        var sut = CreateSut(client);

        // Act
        var result = await sut.GetWarehousesAsync();

        // Assert
        capturedPathAndQuery.Should().Be("/api/warehouse/v1/warehouses?status=Active&includeVirtual=false&pageNumber=1&pageSize=200");
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("7d6b9d0f-7d4f-4ca8-a3a8-ff064ef17f47");
        result[0].Code.Should().Be("WH1");
        result[0].Name.Should().Be("Main Warehouse");
    }

    private static StockClient CreateSut(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient("WarehouseApi"))
            .Returns(client);

        return new StockClient(factory.Object);
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
