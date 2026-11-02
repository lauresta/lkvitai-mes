using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>Circuit-scoped transport. Unknown mutation outcomes retain the original command for exact replay.</summary>
public sealed partial class SalesOrderEntryClient(IHttpClientFactory factory, SalesSessionCredentials credentials, IJSRuntime js, IStringLocalizer<SalesResources>? localizer = null)
{
    private readonly IStringLocalizer<SalesResources> L = localizer ?? SalesResourceText.Localizer;
    public Task<LiveOrderEntryCatalogDto> CatalogAsync() => GetAsync<LiveOrderEntryCatalogDto>("/api/sales/order-entry/catalog");
    public Task<IReadOnlyList<OrderAddressReferenceDto>> AddressesAsync(string? search = null, int? parentId = null, int? id = null) => GetAsync<IReadOnlyList<OrderAddressReferenceDto>>("/api/sales/order-entry/addresses?" + string.Join("&",new[] { search is null ? null : "search="+Uri.EscapeDataString(search), parentId is null ? null : "parentId="+parentId, id is null ? null : "id="+id }.Where(x=>x is not null)));
    public Task<LegacyProductPreviewDto> PreviewAsync(LegacyProductPreviewRequest request) => SendAsync<LegacyProductPreviewDto>(HttpMethod.Post,"/api/sales/order-entry/product-preview",request,false);
    public Task<OrderEditorSnapshot> ReadAsync(long id) => GetAsync<OrderEditorSnapshot>($"/api/sales/order-entry/orders/{id}");
    public Task<CommandOutcome> OutcomeAsync(Guid id) => GetAsync<CommandOutcome>($"/api/sales/order-entry/commands/{id}");
    public Task<OrderPaymentReferences> PaymentReferencesAsync(long id,int? methodId=null,int? bankTransferId=null,int? registerId=null) => GetAsync<OrderPaymentReferences>($"/api/sales/order-entry/orders/{id}/payment-references?" + string.Join("&",new[]{methodId is null ? null : "methodId="+methodId,bankTransferId is null ? null : "bankTransferId="+bankTransferId,registerId is null ? null : "registerId="+registerId}.Where(x=>x is not null)));
    public Task<OrderPaymentBankTransferPage> PaymentBankTransfersAsync(long id,string? search=null,int page=1,int pageSize=25,int? selectedId=null) => GetAsync<OrderPaymentBankTransferPage>($"/api/sales/order-entry/orders/{id}/payment-bank-transfers?page={page}&pageSize={pageSize}"+(string.IsNullOrWhiteSpace(search)?"":"&search="+Uri.EscapeDataString(search))+(selectedId is null?"":"&selectedId="+selectedId));
    public Task<SalesUnitPriceResult> PriceAsync(SalesUnitPriceQuery query) => GetAsync<SalesUnitPriceResult>(
        $"/api/sales/order-entry/unit-price?typeId={query.TypeId}&componentId={query.ComponentId}&widthMm={query.WidthMm}&heightMm={query.HeightMm}&quantity={query.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)}&orderDate={query.OrderDate:yyyy-MM-dd}");

    public Task<OrderEntryCommandResult> CreateAsync(CreateOrderCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, "/api/sales/order-entry/orders", command);
    public Task<OrderEntryCommandResult> SaveAsync(long id, UpdateOrderCommand command) => CommandAsync(command.CommandId, HttpMethod.Put, $"/api/sales/order-entry/orders/{id}", command);
    public Task<OrderEntryCommandResult> CreateProductAsync(long id, CreateProductCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/products", command);
    public Task<OrderEntryCommandResult> DuplicateProductAsync(long id, long productId, DuplicateProductCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/products/{productId}/duplicate", command);
    public Task<OrderEntryCommandResult> ConfigureAsync(long id, long productId, ConfigureProductCommand command) => CommandAsync(command.CommandId, HttpMethod.Put, $"/api/sales/order-entry/orders/{id}/products/{productId}", command);
    public Task<OrderEntryCommandResult> AccessoryAsync(long id, long productId, SaveAccessoryCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/products/{productId}/accessories", command);
    public Task<OrderEntryCommandResult> DeleteAccessoryAsync(long id, long productId, int accessoryId, DeleteAccessoryCommand command) => CommandAsync(command.CommandId, HttpMethod.Delete, $"/api/sales/order-entry/orders/{id}/products/{productId}/accessories/{accessoryId}", command);
    public Task<OrderEntryCommandResult> DeleteProductAsync(long id, long productId, DeleteProductCommand command) => CommandAsync(command.CommandId, HttpMethod.Delete, $"/api/sales/order-entry/orders/{id}/products/{productId}", command);
    public Task<OrderEntryCommandResult> SetFinalPriceAsync(long id, SetOrderFinalPriceCommand command) => CommandAsync(command.CommandId, HttpMethod.Put, $"/api/sales/order-entry/orders/{id}/final-price", command);
    public Task<OrderEntryCommandResult> CancelOrderAsync(long id, CancelOrderCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/cancel", command);
    public Task<OrderEntryCommandResult> DeleteDutyAsync(long id, int dutyId, int employeeId, DeleteOrderDutyCommand command) => CommandAsync(command.CommandId, HttpMethod.Delete, $"/api/sales/order-entry/orders/{id}/duties/{dutyId}/{employeeId}", command);
    public Task<OrderEntryCommandResult> SaveAddressAsync(long id, SaveOrderAddressCommand command) => CommandAsync(command.CommandId, HttpMethod.Put, $"/api/sales/order-entry/orders/{id}/address", command);
    public Task<OrderEntryCommandResult> SaveDutyAsync(long id, SaveOrderDutyCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/duties", command);
    public Task<OrderEntryCommandResult> PostPaymentAsync(long id, PostOrderPaymentCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/payments", command);
    public Task<OrderEntryCommandResult> ConfirmProductsAsync(long id, ConfirmOrderProductsCommand command) => CommandAsync(command.CommandId, HttpMethod.Post, $"/api/sales/order-entry/orders/{id}/confirm", command);

    private Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path, null);
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool? isMutation = null)
    {
        var mutation = isMutation ?? method != HttpMethod.Get;
        var credential = await credentials.CreateApiCredentialAsync();
        if (credential is null) throw new SalesOrderEntryFailure(L["Errors.Client.SessionRenew"].Value, false, 401);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        try
        {
            using var response = await factory.CreateClient("SalesApi").SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var message = L["Errors.Client.ServiceFailure"].Value;
                var code = "";
                try
                {
                    var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (problem.TryGetProperty("detail", out var detail)) message = detail.GetString() ?? message;
                    else if (problem.TryGetProperty("title", out var title)) message = title.GetString() ?? message;
                    if (problem.TryGetProperty("code", out var errorCode)) code = errorCode.GetString() ?? "";
                }
                catch (JsonException) { }
                throw new SalesOrderEntryFailure(SalesApiErrors.Translate(L, code, message), mutation && (int)response.StatusCode >= 500, (int)response.StatusCode, code);
            }
            return await response.Content.ReadFromJsonAsync<T>() ?? throw new SalesOrderEntryFailure(L["Errors.Client.EmptyResponse"].Value, mutation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { throw new SalesOrderEntryFailure(L["Errors.Client.ConnectionInterrupted"].Value, mutation); }
    }
}

public sealed class SalesOrderEntryFailure(string message, bool outcomeUnknown = false, int status = 0, string code = "") : Exception(message)
{
    public bool OutcomeUnknown { get; } = outcomeUnknown;
    public int Status { get; } = status;
    public string Code { get; } = code;
}
