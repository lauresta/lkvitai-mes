using System.Text.Json;

namespace LKvitai.MES.WebUI.Infrastructure;

public static class ProblemDetailsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ProblemDetailsModel?> ParseAsync(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<ProblemDetailsModel>(body, JsonOptions);
            if (model is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(model.TraceId) &&
                model.Extensions is not null &&
                model.Extensions.TryGetValue("traceId", out var traceIdElement) &&
                traceIdElement.ValueKind == JsonValueKind.String)
            {
                var extensionTraceId = traceIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(extensionTraceId))
                {
                    return model with { TraceId = extensionTraceId };
                }
            }

            if (!string.IsNullOrWhiteSpace(model.TraceId))
            {
                return model;
            }

            var traceparent = response.Headers.TryGetValues("traceparent", out var values)
                ? values.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(traceparent))
            {
                return model;
            }

            var parts = traceparent.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var traceId = parts.Length >= 2 ? parts[1] : traceparent;

            return model with { TraceId = traceId };
        }
        catch
        {
            return null;
        }
    }
}
