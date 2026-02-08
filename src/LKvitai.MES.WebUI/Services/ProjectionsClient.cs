using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class ProjectionsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;

    public ProjectionsClient(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public Task<RebuildResultDto> RebuildAsync(string projectionName, bool verify)
    {
        return PostAsync<RebuildResultDto>("/api/projections/rebuild", new { projectionName, verify });
    }

    public Task<VerifyResultDto> VerifyAsync(string projectionName)
    {
        return PostAsync<VerifyResultDto>("/api/projections/verify", new { projectionName });
    }

    private async Task<T> PostAsync<T>(string relativeUrl, object payload)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }
}
