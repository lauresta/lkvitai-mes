using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
    public async Task CreateItemAsync_SerializesFullItemPayload()
    {
        string? body = null;
        var sut = CreateSut(CreateHttpClient(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await sut.CreateItemAsync(new CreateOrUpdateItemRequest
        {
            InternalSKU = "RAW-001",
            Name = "Fabric",
            Description = "Dense cotton",
            CategoryId = 7,
            BaseUoM = "M",
            Weight = 1.25m,
            Volume = 0.4m,
            RequiresLotTracking = true,
            RequiresQC = true,
            Status = "Active",
            PrimaryBarcode = "BAR-001",
            ProductConfigId = "CFG-9"
        });

        body.Should().NotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(body!);
        var root = json.RootElement;
        root.GetProperty("internalSKU").GetString().Should().Be("RAW-001");
        root.GetProperty("description").GetString().Should().Be("Dense cotton");
        root.GetProperty("weight").GetDecimal().Should().Be(1.25m);
        root.GetProperty("volume").GetDecimal().Should().Be(0.4m);
        root.GetProperty("productConfigId").GetString().Should().Be("CFG-9");
    }

    [Fact]
    public async Task GetItemsAsync_DeserializesExtendedListFields()
    {
        var sut = CreateSut(CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "items": [
                    {
                      "id": 42,
                      "internalSKU": "RAW-042",
                      "name": "Fabric",
                      "categoryId": 7,
                      "categoryName": "Raw",
                      "baseUoM": "M",
                      "status": "Active",
                      "requiresLotTracking": true,
                      "requiresQC": false,
                      "primaryBarcode": "BAR-042",
                      "weight": 1.25,
                      "volume": 0.4,
                      "productConfigId": "CFG-42",
                      "createdAt": "2026-06-10T08:00:00Z",
                      "updatedAt": "2026-06-11T08:00:00Z",
                      "primaryThumbnailUrl": "/api/warehouse/v1/items/42/photos/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?size=thumb",
                      "primaryPhotoId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
                    }
                  ],
                  "totalCount": 1,
                  "pageNumber": 1,
                  "pageSize": 50
                }
                """)
        }));

        var result = await sut.GetItemsAsync("RAW", 7, "Active", 1, 50);

        result.Items.Should().ContainSingle();
        var row = result.Items[0];
        row.Weight.Should().Be(1.25m);
        row.Volume.Should().Be(0.4m);
        row.ProductConfigId.Should().Be("CFG-42");
        row.UpdatedAt.Should().NotBeNull();
        row.PrimaryThumbnailUrl.Should().Contain("size=thumb");
    }

    [Fact]
    public async Task CreateSupplierAsync_WhenProblemDetails_ThrowsApiExceptionWithTraceId()
    {
        var response = BuildProblemResponse(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "trace-suppliers-400");
        var sut = CreateSut(CreateHttpClient(_ => response));

        var act = async () => await sut.CreateSupplierAsync(
            new CreateOrUpdateSupplierRequest { Code = "SUP-1", Name = "Supplier" });

        var exception = await act.Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be(400);
        exception.Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        exception.Which.TraceId.Should().Be("trace-suppliers-400");
    }

    [Fact]
    public async Task GetSuppliersAsync_WhenStructuredPayload_DeserializesAllFields()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateSut(CreateHttpClient(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "items": [
                        {
                          "id": 11,
                          "agnumClientId": 57,
                          "code": "SUP-1",
                          "name": "ABC Fasteners",
                          "shortName": "ABC",
                          "companyCode": "302345678",
                          "vatCode": "LT100001234567",
                          "registeredAddress": "Gamyklos g. 1",
                          "pickupAddress": "Sandelio g. 5",
                          "city": "Vilnius",
                          "country": "Lithuania",
                          "contactName": "Jonas",
                          "phone": "+37060000000",
                          "email": "orders@abc.example",
                          "website": "https://abc.example",
                          "additionalInfo": "Preferred",
                          "contactInfo": "legacy",
                          "lastAgnumSyncedAt": "2026-06-10T10:00:00+00:00",
                          "createdAt": "2026-06-01T08:00:00+00:00",
                          "updatedAt": "2026-06-09T09:00:00+00:00"
                        }
                      ],
                      "totalCount": 1,
                      "pageNumber": 1,
                      "pageSize": 50
                    }
                    """)
            };
        }));

        var result = await sut.GetSuppliersAsync("ABC", 1, 50, "Lithuania");

        captured!.RequestUri!.Query.Should().Contain("search=ABC");
        captured.RequestUri!.Query.Should().Contain("country=Lithuania");

        var row = result.Items.Should().ContainSingle().Subject;
        row.AgnumClientId.Should().Be(57);
        row.IsAgnumLinked.Should().BeTrue();
        row.ShortName.Should().Be("ABC");
        row.CompanyCode.Should().Be("302345678");
        row.VatCode.Should().Be("LT100001234567");
        row.RegisteredAddress.Should().Be("Gamyklos g. 1");
        row.PickupAddress.Should().Be("Sandelio g. 5");
        row.City.Should().Be("Vilnius");
        row.Country.Should().Be("Lithuania");
        row.ContactName.Should().Be("Jonas");
        row.Phone.Should().Be("+37060000000");
        row.Email.Should().Be("orders@abc.example");
        row.Website.Should().Be("https://abc.example");
        row.AdditionalInfo.Should().Be("Preferred");
        row.ContactInfo.Should().Be("legacy");
        row.LastAgnumSyncedAt.Should().NotBeNull();
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
