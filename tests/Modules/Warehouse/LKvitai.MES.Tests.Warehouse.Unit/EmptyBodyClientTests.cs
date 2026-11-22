using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>An empty API answer used to crash pages with "The input does not contain any JSON tokens" (HTTP 500).</summary>
public class EmptyBodyClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.NoContent, "")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.OK, "   \r\n")]
    public async Task Agnum_config_with_no_content_means_not_configured(HttpStatusCode status, string body)
    {
        var client = new AgnumClient(new StubFactory(status, body));

        var config = await client.GetConfigAsync();

        config.Should().BeNull();
    }

    [Fact]
    public async Task Agnum_config_with_content_is_parsed()
    {
        var json = """{"id":"3f2c0c5e-2a0e-4a0e-9c53-5a1d0f6f7a11","scope":"BY_CATEGORY","schedule":"0 23 * * *","format":"CSV","isActive":true,"mappings":[]}""";
        var client = new AgnumClient(new StubFactory(HttpStatusCode.OK, json));

        var config = await client.GetConfigAsync();

        config.Should().NotBeNull();
        config!.Scope.Should().Be("BY_CATEGORY");
    }

    [Fact]
    public async Task Agnum_config_server_error_still_surfaces_as_api_exception()
    {
        var client = new AgnumClient(new StubFactory(HttpStatusCode.InternalServerError, ""));

        var act = () => client.GetConfigAsync();

        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task Agnum_list_endpoint_with_no_content_is_an_empty_list()
    {
        var client = new AgnumClient(new StubFactory(HttpStatusCode.NoContent, ""));

        var history = await client.GetHistoryAsync();

        history.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "")]
    [InlineData(HttpStatusCode.OK, "")]
    public async Task Accounting_list_with_no_content_is_an_empty_list(HttpStatusCode status, string body)
    {
        var client = new AgnumAccountingClient(new StubFactory(status, body));

        var profiles = await client.GetProfilesAsync();

        profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Required_object_with_no_content_fails_with_a_clear_message()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };

        var act = () => JsonBody.ReadRequiredAsync<SupplierSyncResultDto>(response, "the sync result");

        (await act.Should().ThrowAsync<JsonException>()).WithMessage("*empty response*sync result*");
    }

    [Fact]
    public async Task Read_or_default_parses_content_and_returns_default_without_it()
    {
        using var filled = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"IdSnd":4}""") };
        using var empty = new HttpResponseMessage(HttpStatusCode.NoContent);

        var parsed = await JsonBody.ReadOrDefaultAsync<AgnumWarehouseDto>(filled);
        var none = await JsonBody.ReadOrDefaultAsync<AgnumWarehouseDto>(empty);

        parsed!.IdSnd.Should().Be(4);
        none.Should().BeNull();
    }

    [Fact]
    public void New_agnum_configuration_form_starts_switched_off()
    {
        var form = new LKvitai.MES.Modules.Warehouse.WebUI.Models.AgnumConfigFormModel();

        form.IsActive.Should().BeFalse();
        form.ConfigId.Should().BeNull();
    }

    private sealed class StubFactory(HttpStatusCode status, string body) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(status, body)) { BaseAddress = new Uri("https://warehouse.test/") };
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);
            if (status != HttpStatusCode.NoContent)
            {
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
