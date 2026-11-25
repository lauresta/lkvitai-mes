using LKvitai.MES.Modules.Warehouse.Api.Middleware;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class PhotoUrlAndRateLimitTests
{
    [Fact]
    public void Photo_urls_are_relative_so_the_web_ui_proxy_serves_them()
    {
        var url = ItemPhotoService.BuildProxyUrl(42, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "thumb");

        Assert.Equal("api/warehouse/v1/items/42/photos/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?size=thumb", url);
        // Resolved against the Warehouse base href it must hit the WebUI proxy, not the API root.
        Assert.Equal("/warehouse/api/warehouse/v1/items/42/photos/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            new Uri(new Uri("https://cikada.example/warehouse/"), url).AbsolutePath);
    }

    [Fact]
    public void Rate_limit_defaults_to_a_budget_that_fits_normal_browsing()
    {
        var middleware = new ApiRateLimitingMiddleware(_ => Task.CompletedTask);

        Assert.Equal(ApiRateLimitingMiddleware.DefaultLimitPerMinute, middleware.LimitPerMinute);
        Assert.True(middleware.LimitPerMinute >= 1000);
    }

    [Theory]
    [InlineData("250", 250)]
    [InlineData("0", ApiRateLimitingMiddleware.DefaultLimitPerMinute)]
    [InlineData("-5", ApiRateLimitingMiddleware.DefaultLimitPerMinute)]
    public void Rate_limit_can_be_configured(string value, int expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ApiRateLimitingMiddleware.LimitConfigKey] = value })
            .Build();

        Assert.Equal(expected, new ApiRateLimitingMiddleware(_ => Task.CompletedTask, configuration).LimitPerMinute);
    }

    [Fact]
    public async Task Requests_over_the_configured_limit_get_429()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ApiRateLimitingMiddleware.LimitConfigKey] = "2" })
            .Build();
        var middleware = new ApiRateLimitingMiddleware(_ => Task.CompletedTask, configuration);
        var statuses = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/warehouse/v1/items";
            context.Request.Headers["X-User-Id"] = "limit-test-user";
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
            statuses.Add(context.Response.StatusCode);
        }

        Assert.Equal([200, 200, 429], statuses);
    }
}
