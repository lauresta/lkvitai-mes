using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

// Wire shapes of agnum-api-deploy docs/PURCHASE_POSTING_SPEC.md (POST body, GET answers).

public sealed class AgnumPurchasePostingRequest
{
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("revision")] public int Revision { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "invoice";
    [JsonPropertyName("doc_no")] public string DocNo { get; set; } = string.Empty;
    [JsonPropertyName("doc_date")] public string DocDate { get; set; } = string.Empty;
    [JsonPropertyName("client_id")] public long ClientId { get; set; }
    [JsonPropertyName("snd_id")] public long SndId { get; set; }
    [JsonPropertyName("scheme_id")] public long SchemeId { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "EUR";
    [JsonPropertyName("rate_eur_per_unit")] public decimal RateEurPerUnit { get; set; }
    [JsonPropertyName("net")] public decimal Net { get; set; }
    [JsonPropertyName("vat")] public decimal Vat { get; set; }
    [JsonPropertyName("gross")] public decimal Gross { get; set; }
    [JsonPropertyName("vat_code")] public string VatCode { get; set; } = string.Empty;
    [JsonPropertyName("postings")] public List<AgnumPostingLine> Postings { get; set; } = new();
    [JsonPropertyName("ref_doc_no"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RefDocNo { get; set; }
    [JsonPropertyName("ref_doc_date"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RefDocDate { get; set; }
    [JsonPropertyName("payment_term_days")] public int PaymentTermDays { get; set; }
    [JsonPropertyName("intrastat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public AgnumIntrastat? Intrastat { get; set; }
    [JsonPropertyName("payload_hash"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PayloadHash { get; set; }
}

public sealed class AgnumPostingLine
{
    [JsonPropertyName("debit_account")] public string DebitAccount { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("amount_eur")] public decimal AmountEur { get; set; }
}

public sealed record AgnumResultPosting(
    [property: JsonPropertyName("debit")] string? Debit, [property: JsonPropertyName("credit")] string? Credit,
    [property: JsonPropertyName("amount")] decimal Amount, [property: JsonPropertyName("amount_eur")] decimal AmountEur);

/// <summary>A document as the service reports it (GET by key or by period).</summary>
public sealed class AgnumPostedDocument
{
    [JsonPropertyName("from_mes")] public bool FromMes { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("revision")] public int? Revision { get; set; }
    [JsonPropertyName("hash")] public string? Hash { get; set; }
    [JsonPropertyName("id_dok")] public long? IdDok { get; set; }
    [JsonPropertyName("id_op")] public long? IdOp { get; set; }
    [JsonPropertyName("doc_no")] public string? DocNo { get; set; }
    [JsonPropertyName("doc_date")] public string? DocDate { get; set; }
    [JsonPropertyName("client_id")] public long? ClientId { get; set; }
    [JsonPropertyName("net")] public decimal? Net { get; set; }
    [JsonPropertyName("vat")] public decimal? Vat { get; set; }
    [JsonPropertyName("gross")] public decimal? Gross { get; set; }
    [JsonPropertyName("has_payments")] public bool HasPayments { get; set; }
    [JsonPropertyName("postings")] public List<AgnumResultPosting>? Postings { get; set; }
}

public sealed record AgnumKeyLookup(bool Found, AgnumPostedDocument? Document);

public sealed record AgnumPeriod(IReadOnlyList<AgnumPostedDocument> Postings, IReadOnlyList<AgnumPostedDocument> ForeignDocuments);

/// <summary>Answer of POST/DELETE: status code plus whatever the service said. Network failures are exceptions, not results.</summary>
public sealed record AgnumWriteResult(int Status, long? IdDok, long? IdOp, string? PostingsJson, string? Message)
{
    public bool Ok => Status is 200 or 201;
}

/// <summary>Purchase posting routes of the Agnum service (blueprint 5.1 / 5.2).</summary>
public interface IAgnumPostingClient
{
    Task<AgnumKeyLookup> GetByKeyAsync(Guid key, CancellationToken ct);
    Task<AgnumWriteResult> PostAsync(string payloadJson, CancellationToken ct);
    Task<AgnumWriteResult> DeleteAsync(Guid key, int revision, CancellationToken ct);
    Task<AgnumPeriod> GetPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed class AgnumPostingClient : IAgnumPostingClient
{
    public const string HttpClientName = "AgnumPosting";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public AgnumPostingClient(HttpClient http) => _http = http;

    public async Task<AgnumKeyLookup> GetByKeyAsync(Guid key, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"api/purchase-postings/{key:D}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new AgnumPostingHttpException((int)response.StatusCode, $"GET purchase-postings/{key:D} returned HTTP {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("found", out var found) && found.ValueKind == JsonValueKind.False)
            return new AgnumKeyLookup(false, null);
        return new AgnumKeyLookup(true, doc.RootElement.Deserialize<AgnumPostedDocument>(JsonOptions));
    }

    public async Task<AgnumWriteResult> PostAsync(string payloadJson, CancellationToken ct)
    {
        using var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("api/purchase-postings", content, ct);
        return await ReadWriteAsync(response, ct);
    }

    public async Task<AgnumWriteResult> DeleteAsync(Guid key, int revision, CancellationToken ct)
    {
        using var response = await _http.DeleteAsync($"api/purchase-postings/{key:D}?revision={revision.ToString(CultureInfo.InvariantCulture)}", ct);
        return await ReadWriteAsync(response, ct);
    }

    public async Task<AgnumPeriod> GetPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var uri = $"api/purchase-postings?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        using var response = await _http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
            throw new AgnumPostingHttpException((int)response.StatusCode, $"GET purchase-postings by period returned HTTP {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return new AgnumPeriod(
            Read(doc.RootElement, "postings"),
            Read(doc.RootElement, "foreign_documents"));
    }

    private static List<AgnumPostedDocument> Read(JsonElement root, string name)
        => root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.Deserialize<List<AgnumPostedDocument>>(JsonOptions) ?? new()
            : new();

    private static async Task<AgnumWriteResult> ReadWriteAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(ct);
        long? idDok = null, idOp = null;
        string? postings = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("id_dok", out var d) && d.ValueKind == JsonValueKind.Number) idDok = d.GetInt64();
                if (root.TryGetProperty("id_op", out var o) && o.ValueKind == JsonValueKind.Number) idOp = o.GetInt64();
                if (root.TryGetProperty("postings", out var p)) postings = p.GetRawText();
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) message = m.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON (a proxy page): keep the status and a short excerpt.
            message = body.Length > 300 ? body[..300] : body;
        }

        if (message is null && status >= 400) message = $"HTTP {status} {(HttpStatusCode)status}";
        return new AgnumWriteResult(status, idDok, idOp, postings, message);
    }
}

public sealed class AgnumPostingHttpException : Exception
{
    public int Status { get; }
    public AgnumPostingHttpException(int status, string message) : base(message) => Status = status;
}

/// <summary>Intrastat header of an EU acquisition (service field names nature, delivery_terms, transport, country).</summary>
public sealed class AgnumIntrastat
{
    [JsonPropertyName("nature")] public string? Nature { get; set; }
    [JsonPropertyName("delivery_terms")] public string? DeliveryTerms { get; set; }
    [JsonPropertyName("transport")] public string? Transport { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}
