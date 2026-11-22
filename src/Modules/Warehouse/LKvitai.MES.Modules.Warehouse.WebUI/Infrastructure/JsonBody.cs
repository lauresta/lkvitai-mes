using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>
/// Reads JSON response bodies that may legitimately be empty (204, or 200 with no content when nothing
/// is configured yet). <c>ReadFromJsonAsync</c> and <c>JsonSerializer.Deserialize</c> throw
/// "The input does not contain any JSON tokens" on those, which turned an empty answer into an HTTP 500 page.
/// </summary>
public static class JsonBody
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public static bool IsEmpty(HttpStatusCode status, string? body)
        => status == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(body);

    /// <summary>Deserializes the body, or returns <c>default</c> when the response has no content.</summary>
    public static T? ParseOrDefault<T>(HttpStatusCode status, string? body, JsonSerializerOptions? options = null)
        => IsEmpty(status, body) ? default : JsonSerializer.Deserialize<T>(body!, options ?? WebOptions);

    public static async Task<T?> ReadOrDefaultAsync<T>(
        HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken ct = default)
    {
        if (response.StatusCode == HttpStatusCode.NoContent) return default;
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseOrDefault<T>(response.StatusCode, body, options);
    }

    /// <summary>
    /// For endpoints that must return data: an empty body gives an empty list for list-shaped types
    /// and a clear <see cref="JsonException"/> for anything else.
    /// </summary>
    public static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response, string what, JsonSerializerOptions? options = null, CancellationToken ct = default)
    {
        var value = await ReadOrDefaultAsync<T>(response, options, ct);
        return value ?? EmptyValue<T>() ?? throw new JsonException($"The server returned an empty response for {what}.");
    }

    private static readonly HashSet<Type> ListShapes =
    [
        typeof(List<>), typeof(IList<>), typeof(IReadOnlyList<>), typeof(ICollection<>),
        typeof(IReadOnlyCollection<>), typeof(IEnumerable<>),
    ];

    /// <summary>An empty list for list-shaped types, a plain object for <c>object</c>, otherwise <c>default</c>.</summary>
    public static T? EmptyValue<T>()
    {
        var type = typeof(T);
        if (type == typeof(object)) return (T)new object();
        if (!type.IsGenericType || !ListShapes.Contains(type.GetGenericTypeDefinition())) return default;
        return (T)Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]))!;
    }
}
