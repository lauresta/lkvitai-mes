using System.Net.Http.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed record OutboxRowDto(
    long Id, string DocumentType, int DocumentId, string DocumentNumber, DateOnly DocumentDate, string Currency, decimal GrossAmount,
    int SupplierId, string? SupplierName, string Operation, int Revision, string Status, int Attempts, string? LastError,
    DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset NextAttemptAt, long? AgnumDocumentId, bool Held);

public sealed record SendingSettingsDto(bool Enabled, DateOnly? StartDate, string? ServiceUrl,
    DateTimeOffset? LastReconciledAt, int LastReconciliationFindings, string? LastReconciliationError,
    int Waiting, int NeedsReview, DateTimeOffset? OldestOpenAt);

public sealed record SendingSettingsRequest(bool Enabled, DateOnly? StartDate);
public sealed record ConnectionResultDto(bool Ok, string? Message, int Warehouses);
public sealed record ReconciliationFindingDto(long Id, DateTimeOffset RunAt, string Kind, string? DocumentType, int? DocumentId, string? DocumentNumber, string Details);
public sealed record UnpostResponseDto(string Outcome);

public sealed record CostDocumentRowDto(
    int Id, int SupplierId, string DocumentNumber, DateOnly DocumentDate, string Currency,
    decimal NetAmount, decimal VatAmount, decimal GrossAmount, decimal? NetEur, decimal? RateUnitsPerEur,
    string VatTreatment, string AccountingRoute, int BasisShipmentId, string CostTypeCode, string PostingStatus,
    string? ManualReason, string? QueueStatus, string? QueueError, string? SupplierName);

public sealed record CostDocumentCreateDto(
    int SupplierId, string DocumentNumber, DateOnly DocumentDate, string? Currency, decimal NetAmount, decimal VatAmount,
    string? VatTreatment, string? AccountingRoute, int BasisShipmentId, string CostTypeCode, string? ManualReason);

public sealed record CostTypeOptionDto(string Code, string Name, bool IncludedInCost, string? ExpenseAccount);
public sealed record LookupItemDto(int Id, string Label);
public sealed record CostLookupsDto(List<LookupItemDto> Suppliers, List<LookupItemDto> Shipments);

/// <summary>Blueprint 4.6 "Sending", 4.7 queue, 4.8 un-post and the screen of cost invoices of other companies (4.4).</summary>
public sealed class AgnumOutboxClient
{
    private const string Outbox = "/api/warehouse/v1/agnum/outbox";
    private const string Costs = "/api/warehouse/v1/additional-costs";
    private readonly IHttpClientFactory _factory;

    public AgnumOutboxClient(IHttpClientFactory factory) => _factory = factory;

    private HttpClient Http => _factory.CreateClient("WarehouseApi");

    public Task<SendingSettingsDto> GetSettingsAsync(CancellationToken ct = default) => GetAsync<SendingSettingsDto>($"{Outbox}/settings", ct);
    public Task SaveSettingsAsync(SendingSettingsRequest r, CancellationToken ct = default) => SendAsync(() => Http.PutAsJsonAsync($"{Outbox}/settings", r, ct));
    public Task<ConnectionResultDto> CheckConnectionAsync(CancellationToken ct = default) => PostAsync<ConnectionResultDto>($"{Outbox}/check-connection", null, ct);
    public Task<List<OutboxRowDto>> GetRowsAsync(string? status, bool openOnly, CancellationToken ct = default)
        => GetAsync<List<OutboxRowDto>>($"{Outbox}?openOnly={openOnly.ToString().ToLowerInvariant()}" + (string.IsNullOrEmpty(status) ? "" : $"&status={status}"), ct);
    public Task<List<OutboxRowDto>> GetForDocumentAsync(string documentType, int id, CancellationToken ct = default)
        => GetAsync<List<OutboxRowDto>>($"{Outbox}/documents/{documentType}/{id}", ct);
    public Task<List<ReconciliationFindingDto>> GetFindingsAsync(CancellationToken ct = default) => GetAsync<List<ReconciliationFindingDto>>($"{Outbox}/findings", ct);
    public Task RetryAsync(long id, CancellationToken ct = default) => SendAsync(() => Http.PostAsync($"{Outbox}/{id}/retry", null, ct));
    public Task MarkManualAsync(long id, string reason, CancellationToken ct = default) => SendAsync(() => Http.PostAsJsonAsync($"{Outbox}/{id}/mark-manual", new { reason }, ct));
    public Task RunNowAsync(CancellationToken ct = default) => SendAsync(() => Http.PostAsync($"{Outbox}/run", null, ct));
    public Task<UnpostResponseDto> UnpostAsync(string documentType, int documentId, CancellationToken ct = default)
        => PostAsync<UnpostResponseDto>($"{Outbox}/unpost", new { documentType, documentId }, ct);

    public Task<List<CostDocumentRowDto>> GetCostDocumentsAsync(CancellationToken ct = default) => GetAsync<List<CostDocumentRowDto>>($"{Costs}/documents", ct);
    public Task<List<CostTypeOptionDto>> GetCostTypesAsync(CancellationToken ct = default) => GetAsync<List<CostTypeOptionDto>>($"{Costs}/types", ct);
    public Task<CostLookupsDto> GetCostLookupsAsync(CancellationToken ct = default) => GetAsync<CostLookupsDto>($"{Costs}/lookups", ct);
    public Task<CostDocumentRowDto> CreateCostDocumentAsync(CostDocumentCreateDto r, CancellationToken ct = default) => PostAsync<CostDocumentRowDto>($"{Costs}/documents", r, ct);
    public Task<CostDocumentRowDto> PostCostDocumentAsync(int id, CancellationToken ct = default) => PostAsync<CostDocumentRowDto>($"{Costs}/documents/{id}/post", null, ct);

    private async Task<T> GetAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await Http.GetAsync(uri, ct);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
    }

    private async Task<T> PostAsync<T>(string uri, object? body, CancellationToken ct)
    {
        using var response = body is null ? await Http.PostAsync(uri, null, ct) : await Http.PostAsJsonAsync(uri, body, ct);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
    }

    private static async Task SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        using var response = await send();
        await EnsureAsync(response);
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
    }
}
