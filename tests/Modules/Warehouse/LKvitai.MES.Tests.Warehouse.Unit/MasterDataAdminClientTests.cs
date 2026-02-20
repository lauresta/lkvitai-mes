using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class MasterDataAdminClientTests
{
    [Fact]
    public async Task CreateItemAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        var response = BuildProblemResponse(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "trace-items-400");
        var sut = CreateSut(CreateHttpClient(_ => response));

        var act = async () => await sut.CreateItemAsync(new CreateOrUpdateItemRequest
        {
            Name = "Bolt",
            CategoryId = 1,
            BaseUoM = "PCS"
        });

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        exception.Which.TraceId.Should().Be("trace-items-400");
    }

    [Fact]
    public async Task CreateSupplierAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        var response = BuildProblemResponse(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "trace-suppliers-400");
        var sut = CreateSut(CreateHttpClient(_ => response));

        var act = async () => await sut.CreateSupplierAsync(
            new CreateOrUpdateSupplierRequest("SUP-1", "Supplier", null));

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        exception.Which.TraceId.Should().Be("trace-suppliers-400");
    }

    [Fact]
    public async Task CreateLocationAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        var response = BuildProblemResponse(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "trace-locations-400");
        var sut = CreateSut(CreateHttpClient(_ => response));

        var act = async () => await sut.CreateLocationAsync(
            new CreateOrUpdateLocationRequest(
                Code: "A-01",
                Barcode: "LOC-A01",
                Type: "Bin",
                ParentLocationId: null,
                IsVirtual: false,
                MaxWeight: 100m,
                MaxVolume: 10m,
                Status: "Active",
                ZoneType: "General",
                CoordinateX: null,
                CoordinateY: null,
                CoordinateZ: null,
                WidthMeters: null,
                LengthMeters: null,
                HeightMeters: null,
                Aisle: null,
                Rack: null,
                Level: null,
                Bin: null,
                CapacityWeight: null,
                CapacityVolume: null));

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        exception.Which.TraceId.Should().Be("trace-locations-400");
    }

    [Fact]
    public async Task ImportAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        var response = BuildProblemResponse(HttpStatusCode.UnprocessableEntity, "IMPORT_VALIDATION_FAILED", "trace-import-422");
        var sut = CreateSut(CreateHttpClient(_ => response));

        var act = async () => await sut.ImportAsync(
            entityType: "items",
            fileName: "items.xlsx",
            fileBytes: [1, 2, 3],
            dryRun: true);

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(422);
        exception.Which.ErrorCode.Should().Be("IMPORT_VALIDATION_FAILED");
        exception.Which.TraceId.Should().Be("trace-import-422");
    }

    private static MasterDataAdminClient CreateSut(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient("WarehouseApi"))
            .Returns(client);

        return new MasterDataAdminClient(factory.Object, new NullLogger<MasterDataAdminClient>());
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
    }

    private static HttpResponseMessage BuildProblemResponse(HttpStatusCode status, string errorCode, string traceId)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(
                $$"""
                {
                  "type": "https://errors/{{errorCode}}",
                  "title": "Validation failed",
                  "status": {{(int)status}},
                  "detail": "Request is invalid.",
                  "traceId": "{{traceId}}",
                  "errorCode": "{{errorCode}}"
                }
                """)
        };

        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
        return response;
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
