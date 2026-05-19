using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumApiClientTests
{
    [Fact]
    public async Task GetProductsAsync_WhenEmptySearchReturnsBadRequest_ShouldRetryWithFallbackQuery()
    {
        var products = new[]
        {
            new AgnumProductDto
            {
                Id = 1,
                Code = "SKU-001",
                Name = "Test Product",
                Pcs = "vnt",
                Enabled = true,
                Balance = 10m
            }
        };

        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(products), Encoding.UTF8, "application/json")
            }
        });

        var handler = new SequentialHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new System.Uri("https://agnum.example/")
        };

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddFilter((_, _) => false));
        var client = new AgnumApiClient(httpClient, new AgnumWarehouseKeyOptions { ApiKey = "secret" }, loggerFactory.CreateLogger<AgnumApiClient>());
        var result = await client.GetProductsAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("SKU-001");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be("/api/products/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id");
    }

    [Fact]
    public async Task GetClientsAsync_ShouldReturnAgnumClients()
    {
        var clients = new[]
        {
            new AgnumClientDto
            {
                Id = 1,
                Code = "BUYER",
                Name = "Buyer",
                PozymNumbers = new List<int> { 2 }
            },
            new AgnumClientDto
            {
                Id = 2,
                Code = "SUP-POZYM",
                Name = "Supplier by marker",
                PozymNumbers = new List<int> { 1 }
            },
            new AgnumClientDto
            {
                Id = 3,
                Code = "SUP-ROLE",
                Name = "Supplier by role",
                ClientRoles = new List<string> { "SUPPLIER" }
            }
        };

        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(clients), Encoding.UTF8, "application/json")
            }
        });

        var handler = new SequentialHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new System.Uri("https://agnum.example/")
        };

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddFilter((_, _) => false));
        var client = new AgnumApiClient(httpClient, new AgnumWarehouseKeyOptions { ApiKey = "secret" }, loggerFactory.CreateLogger<AgnumApiClient>());

        var result = await client.GetClientsAsync(CancellationToken.None);

        result.Select(x => x.Code).Should().Equal("BUYER", "SUP-POZYM", "SUP-ROLE");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/api/clients/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id");
    }

    private sealed class SequentialHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequentialHttpMessageHandler(Queue<HttpResponseMessage> responses)
        {
            _responses = responses;
            Requests = new List<HttpRequestMessage>();
        }

        public List<HttpRequestMessage> Requests { get; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
