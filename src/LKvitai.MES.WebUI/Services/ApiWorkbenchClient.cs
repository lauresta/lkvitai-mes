using System.Net.Http.Json;
using LKvitai.MES.WebUI.Infrastructure;

namespace LKvitai.MES.WebUI.Services;

public sealed class ApiWorkbenchClient
{
    private readonly IHttpClientFactory _factory;

    public ApiWorkbenchClient(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<WorkbenchResult> SendAsync(
        string method,
        string route,
        string? jsonBody,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        if ((method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
             method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
             method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(jsonBody))
        {
            request.Content = JsonContent.Create(System.Text.Json.JsonSerializer.Deserialize<object>(jsonBody));
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var body = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                   contentType.Contains("text/", StringComparison.OrdinalIgnoreCase)
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : $"Binary payload: {response.Content.Headers.ContentLength ?? 0} bytes";

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            body = problem is null ? body : $"{body}\n\nProblemDetails: {problem.Title} | {problem.Detail} | traceId={problem.TraceId}";
        }

        return new WorkbenchResult((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, body);
    }

    public sealed record WorkbenchResult(int StatusCode, string ReasonPhrase, string Body);
}

