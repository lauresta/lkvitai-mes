using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LKvitai.MES.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class FedExApiContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task GenerateTrackingNumberAsync_ShouldMatchCarrierContract()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"trackingNumber\":\"FDX-12345\"}")
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fedex.test")
        };

        var factory = new StubHttpClientFactory(client);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CarrierApi:Enabled"] = "true",
                ["CarrierApi:FedEx:BaseUrl"] = "https://fedex.test",
                ["CarrierApi:FedEx:TrackingPath"] = "/api/v1/tracking-numbers",
                ["CarrierApi:FedEx:ApiKey"] = "secret-key"
            })
            .Build();

        var sut = new FedExApiService(config, factory, Mock.Of<ILogger<FedExApiService>>());

        var shipmentId = Guid.NewGuid();
        var result = await sut.GenerateTrackingNumberAsync(shipmentId, "FEDEX");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("FDX-12345");

        handler.Requests.Should().HaveCount(1);
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be("/api/v1/tracking-numbers");
        request.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue("Bearer", "secret-key"));

        var body = handler.Bodies.Single();
        body.Should().Contain("\"carrier\":\"FEDEX\"");
        body.Should().Contain("\"shipmentId\":");
        body.Should().Contain("\"requestedAtUtc\":");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return _responder(request);
        }
    }
}
