using System.Net.Http.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed record StockProfileDto(int Id, string Name, string StockAccount, int AgnumSndId,
    string? PriceDifferenceAccount, string? WriteOffAccount, string? MarkdownAccount, bool IsDefault);

public sealed record PostingRuleDto(int Id, int StockAccountProfileId, string VatTreatment, int SchemeId, string? VatCode);

public sealed record SchemeRowDto(int No, string? Name, string? Debit, string? Credit);
public sealed record SchemeDto(int IdSch, string? Name, List<SchemeRowDto>? Rows);
public sealed record AgnumWarehouseDto(int IdSnd, string? Code, string? Name);
public sealed record AgnumVatCodeDto(string Code);
public sealed record AgnumAccountDto(string Code, string? Name);
public sealed record CostTypeConfigDto(int Id, string Code, string Name, bool IncludedInCost, string? ExpenseAccount, bool IsActive);

public sealed record ConfigAuditDto(long Id, string Entity, string EntityId, string Action, string ChangedBy,
    DateTimeOffset ChangedAt, string? BeforeJson, string? AfterJson);

public sealed record SupplierSyncResultDto(int Created, int Updated, int MarkedMissing, int Restored, int CodeConflicts);

/// <summary>Blueprint 4.6 configuration + 4.11 supplier sync endpoints.</summary>
public sealed class AgnumAccountingClient
{
    private const string Base = "/api/warehouse/v1/agnum/accounting/";
    private readonly IHttpClientFactory _factory;

    public AgnumAccountingClient(IHttpClientFactory factory) => _factory = factory;

    public Task<List<StockProfileDto>> GetProfilesAsync(CancellationToken ct = default) => GetAsync<List<StockProfileDto>>("profiles", ct);
    public Task SaveProfileAsync(StockProfileDto p, CancellationToken ct = default) => SendAsync(c => c.PutAsJsonAsync(Base + "profiles", p, ct));
    public Task DeleteProfileAsync(int id, CancellationToken ct = default) => SendAsync(c => c.DeleteAsync(Base + $"profiles/{id}", ct));
    public Task<List<PostingRuleDto>> GetRulesAsync(CancellationToken ct = default) => GetAsync<List<PostingRuleDto>>("rules", ct);
    public Task SaveRuleAsync(PostingRuleDto r, CancellationToken ct = default) => SendAsync(c => c.PutAsJsonAsync(Base + "rules", r, ct));
    public Task DeleteRuleAsync(int id, CancellationToken ct = default) => SendAsync(c => c.DeleteAsync(Base + $"rules/{id}", ct));
    public Task<List<CostTypeConfigDto>> GetCostTypesAsync(CancellationToken ct = default) => GetAsync<List<CostTypeConfigDto>>("cost-types", ct);
    public Task SaveCostTypeAsync(CostTypeConfigDto t, CancellationToken ct = default) => SendAsync(c => c.PutAsJsonAsync(Base + "cost-types", t, ct));
    public Task<List<AgnumAccountDto>> GetAccountsAsync(CancellationToken ct = default) => GetAsync<List<AgnumAccountDto>>("reference/accounts", ct);
    public Task<List<ConfigAuditDto>> GetAuditAsync(CancellationToken ct = default) => GetAsync<List<ConfigAuditDto>>("audit", ct);
    public Task<List<SchemeDto>> GetSchemesAsync(CancellationToken ct = default) => GetAsync<List<SchemeDto>>("reference/schemes", ct);
    public Task<List<AgnumWarehouseDto>> GetWarehousesAsync(CancellationToken ct = default) => GetAsync<List<AgnumWarehouseDto>>("reference/warehouses", ct);
    public Task<List<AgnumVatCodeDto>> GetVatCodesAsync(CancellationToken ct = default) => GetAsync<List<AgnumVatCodeDto>>("reference/vat-codes", ct);
    public Task<SupplierSyncResultDto> SyncSuppliersAsync(CancellationToken ct = default) => PostAsync<SupplierSyncResultDto>("suppliers/sync", ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await _factory.CreateClient("WarehouseApi").GetAsync(Base + path, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> PostAsync<T>(string path, CancellationToken ct)
    {
        using var response = await _factory.CreateClient("WarehouseApi").PostAsync(Base + path, null, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendAsync(Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        using var response = await send(_factory.CreateClient("WarehouseApi"));
        await EnsureAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureAsync(response);
        return await JsonBody.ReadRequiredAsync<T>(response, typeof(T).Name, ct: ct);
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
    }
}
