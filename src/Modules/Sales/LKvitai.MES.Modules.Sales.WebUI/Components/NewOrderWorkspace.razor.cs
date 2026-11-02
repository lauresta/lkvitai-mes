using System.Globalization;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Sales.WebUI.Components;

public partial class NewOrderWorkspace
{
    [Inject] public SalesOrderEntryClient Entry { get; set; } = default!;
    [Inject] public AuthenticationStateProvider Authentication { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    private bool IsLive = true;
    private bool _loading = true;
    private bool _busy;
    private string? _loadError;
    private string _liveError
    {
        get => _noticeText;
        set { if (string.IsNullOrWhiteSpace(value)) DismissNotice(); else ShowNotice(value); }
    }
    private int? _customerId, _storeId, _managerId, _consultantId;
    private string _headerSaved = "";
    private string _productSaved = "";
    private string _addressSaved = "";
    private bool AddressDirty => _order?.LegacyId is not null && JsonSerializer.Serialize(AddressInput()) != _addressSaved;
    private OrderAddressInput? AddressInput() => _order?.StreetId is { } streetId ? new OrderAddressInput(streetId,_order.Building,_order.Flat,_order.Phone,_order.AddressNote) : null;
    private string _priceMessage = "";
    private bool _pendingRestored;
    private CommandOutcome? _legacySaveOutcome;
    private IReadOnlyList<LiveNamedReferenceDto> _duties = [];
    private IReadOnlyList<LiveNamedReferenceDto> _priorities = [];
    private DateTimeOffset _catalogLoadedAt;
    private int? _employeeId, _employeeDutyId;
    private bool HeaderDirty => _order?.LegacyId is not null && JsonSerializer.Serialize(HeaderInput()) != _headerSaved;
    private DateOnly SavedOrderDate => string.IsNullOrEmpty(_headerSaved) ? _order!.Date : JsonSerializer.Deserialize<OrderHeaderInput>(_headerSaved)!.Date;
    private bool ProductDirty => _configuringLine is not null && ProductState(_configuringLine) != _productSaved;
    private bool CanEnterManualPrice => ConfiguredType?.CanEnterManualPrice == true;

    protected override Task OnInitializedAsync() => ReloadLiveAsync();
    private async Task ReloadLiveAsync()
    {
        _loading = true; _loadError = null;
        try
        {
            var state = await Authentication.GetAuthenticationStateAsync();
            IsLive = !state.User.HasClaim("sales_review", "true");
            if (!IsLive) Catalog = DemoCatalog.Load();
            else
            {
                Entry.SetOrderScope(LegacyOrderId);
                if(!await RefreshLiveCatalogAsync(true))throw new SalesOrderEntryFailure(_liveError);
                if (LegacyOrderId is { } id) ApplySnapshot(await Entry.ReadAsync(id));
            }
        }
        catch (Exception ex) when (ex is SalesOrderEntryFailure or HttpRequestException)
        { _loadError = ex.Message; }
        finally { _loading = false; }
    }

    private async Task<bool> RefreshLiveCatalogAsync(bool force = false)
    {
        if (!IsLive || !force && DateTimeOffset.UtcNow - _catalogLoadedAt < TimeSpan.FromSeconds(60)) return true;
        try
        {
            var live = await Entry.CatalogAsync();
            Catalog = DemoCatalog.FromLive(live); _duties = live.Duties; _priorities = live.Priorities;
            _catalogLoadedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (SalesOrderEntryFailure ex) { _liveError = ex.Message; return false; }
    }

    private async Task CheckLiveParametersAsync()
    {
        if (!IsLive || _configuringLine is null || !await RefreshLiveCatalogAsync(true)) return;
        await UpdateLivePreviewAsync(false);
        if (string.IsNullOrEmpty(_previewError)) _priceMessage = "Parameter and stock preview refreshed. Saving checks the current legacy rules.";
    }

    private async Task RefreshLiveProductAsync()
    {
        if (_order?.LegacyId is not { } id || _configuringLine?.LegacyId is not { } productId) return;
        if ((HeaderDirty || ProductDirty || AddressDirty) && !await JS.InvokeAsync<bool>("confirm", "Reload saved values? Unsaved header, address and product edits will be discarded.")) return;
        _busy = true;
        try
        {
            _previewGeneration++;
            if (!await RefreshLiveCatalogAsync(true)) return;
            ApplySnapshot(await Entry.ReadAsync(id), productId, false, false);
            await UpdateLivePreviewAsync(false);
        }
        catch (SalesOrderEntryFailure ex) { _liveError = ex.Message; }
        finally { _busy = false; }
    }

    private async Task DuplicateLiveProductAsync()
    {
        if (_order?.LegacyId is not { } id || _configuringLine?.LegacyId is not { } productId || !await SaveLiveProductAsync()) return;
        _busy = true;
        try
        {
            var result = await Entry.DuplicateProductAsync(id, productId, new DuplicateProductCommand(Guid.NewGuid(), _order.Revision));
            ApplySnapshot(result.Order, result.ProductId);
            await UpdateLivePreviewAsync(false);
            _priceMessage = "Product duplicated with current legacy checks.";
        }
        catch (SalesOrderEntryFailure ex) { _liveError = ex.Message; }
        finally { _busy = false; }
    }

    private static string ReferenceName(IEnumerable<LiveNamedReferenceDto> items, int? id) => items.FirstOrDefault(x => x.Id == id)?.Name ?? "";
    private RenderFragment ReferenceSelect(string id, IEnumerable<LiveNamedReferenceDto> items, int? selected, Action<int?> update, bool disabled = false, string? existingLabel = null) => builder =>
    {
        builder.OpenComponent<ReferenceAutocomplete>(0);
        builder.AddAttribute(1,nameof(ReferenceAutocomplete.Id),id);
        builder.AddAttribute(2,nameof(ReferenceAutocomplete.Items),items);
        builder.AddAttribute(3,nameof(ReferenceAutocomplete.Value),selected);
        builder.AddAttribute(4,nameof(ReferenceAutocomplete.ValueChanged),EventCallback.Factory.Create<int?>(this,update));
        builder.AddAttribute(5,nameof(ReferenceAutocomplete.ExistingLabel),existingLabel);
        builder.AddAttribute(6,nameof(ReferenceAutocomplete.Disabled),disabled);
        builder.CloseComponent();
    };

    private OrderHeaderInput HeaderInput()
    {
        var order = _order!;
        return new OrderHeaderInput(order.SeriesId, order.Number, order.Date, order.Due,
            order.CustomerId ?? 0, order.StoreId ?? 0, order.ManagerId, order.ConsultantId,
            order.PriorityId, order.MountingDate, order.Note, order.DiscountPercent);
    }
    private ProductConfigurationInput ProductInput(DemoOrderLine line) => new(line.TypeId,
        line.WidthMm, line.HeightMm, line.Quantity, line.ColourId ?? 0, string.IsNullOrEmpty(line.ControlType) ? null : line.ControlType,
        line.Price, line.UseListPrice, line.Note, line.ParameterValues.Select(x => (string?)x).ToArray(), line.RecalculateParameters, Enumerable.Range(0,12).Where(i=>line.ParameterManual[i]).ToArray(), line.CalculationRevision);
    private string ProductState(DemoOrderLine line) => JsonSerializer.Serialize(ProductInput(line) with { CalculationRevision = null });
    private string CalculationContext(DemoOrderLine line) => $"{_order?.LegacyId}:{line.LegacyId}:{line.TypeId}:{line.ColourId ?? 0}";
    private bool BusinessConfigurationDirty(DemoOrderLine line)
    {
        var previous = JsonSerializer.Deserialize<ProductConfigurationInput>(_productSaved);
        return previous is null || JsonSerializer.Serialize(previous with { Note = null, CalculationRevision = null, RecalculateParameters = false }) !=
            JsonSerializer.Serialize(ProductInput(line) with { Note = null, CalculationRevision = null, RecalculateParameters = false });
    }

    private void ApplySnapshot(OrderEditorSnapshot snapshot, long? selectedProductId = null, bool preserveHeader = true, bool preserveAddress = true)
    {
        var headerBuffer=preserveHeader && _order?.LegacyId==snapshot.Header.Id && HeaderDirty ? HeaderInput() : null;
        var addressBuffer=preserveAddress && _order?.LegacyId==snapshot.Header.Id && AddressDirty ? AddressInput() : null;
        var addressOptionsBuffer=addressBuffer is not null ? _addressOptions : null;
        var h = snapshot.Header;
        var order = new DemoOrderDraft
        {
            LegacyId=h.Id, Revision=snapshot.Revision, Number=h.Number, SeriesId=h.SeriesId,
            Series=Catalog.Series.FirstOrDefault(x=>x.Id==h.SeriesId)?.Code ?? "",
            CustomerId=h.CustomerId, Customer=h.CustomerName ?? "", StoreId=h.StoreId, Store=h.StoreName ?? "",
            ManagerId=h.ManagerId, Manager=h.ManagerName ?? "", ConsultantId=h.ConsultantId, Consultant=h.ConsultantName ?? "",
            Date=h.Date ?? Today, Due=h.Due ?? Today, MountingDate=h.MountingDate, PriorityId=h.PriorityId ?? 2,
            Note=h.Note ?? "", NoteHistory=h.NoteHistory ?? [], DiscountPercent=h.DiscountPercent ?? 0, CanEdit=h.CanEdit && !h.Cancelled, StateId=h.StateId, Cancelled=h.Cancelled,
            WritesAllowed=snapshot.WritesAllowed, CancellationUnavailableReason=snapshot.CancellationUnavailableReason, PaymentsEnabled=snapshot.PaymentsEnabled, ConfirmationEnabled=snapshot.ConfirmationEnabled, AllowedPaymentMethods=snapshot.AllowedPaymentMethods ?? [], ServerTotal=h.CalculatedTotal, StateName=h.StateName ?? "",
            ServerCalculated=(h.DiscountableTotal ?? 0)+(h.NonDiscountableTotal ?? 0), ServerPaid=h.Paid,
            ServerDiscountable=h.DiscountableTotal, ServerDiscountAmount=(h.DiscountableTotal ?? 0)+(h.NonDiscountableTotal ?? 0)-h.CalculatedTotal,
            StreetId=snapshot.Address?.StreetId, Building=snapshot.Address?.Building ?? "",
            Flat=snapshot.Address?.Room ?? "", Phone=snapshot.Address?.Phone ?? "", AddressNote=snapshot.Address?.Note ?? ""
        };
        foreach (var item in snapshot.Products)
        {
            var category=Catalog.Categories.FirstOrDefault(x=>x.Id==item.CategoryId);
            var line = new DemoOrderLine
            {
                LegacyId=item.Id, TypeId=item.TypeId ?? 0, CategoryId=item.CategoryId ?? 0,
                Category=item.CategoryName ?? "", Name=item.TypeName ?? $"#{item.TypeId}",
                Kind=category?.Name is "Priedai" or "Be gaminių" ? "Priedai" : category?.Name=="Paslaugos" ? "Service" : "Product",
                WidthMm=item.WidthMm ?? 0, HeightMm=item.HeightMm ?? 0, Quantity=item.Quantity ?? 0,
                ColourId=item.ColourId, ColourCode=item.ColourCode ?? "", ControlType=item.Control ?? "",
                Price=item.UnitPrice ?? 0, Note=item.Note ?? "", CanEdit=item.CanEdit && !h.Cancelled, CanDelete=item.CanDelete && !h.Cancelled,
                StateName=item.StateName ?? "", StateId=item.StateId, IsManufactured=item.IsManufactured, HasStoredPrice=item.UnitPrice is not null, HasStoredDimensions=item.WidthMm is not null && item.HeightMm is not null, ServerAmount=item.Amount, ServerAccessoryAmount=item.AccessoryAmount, Discountable=category?.DiscountAllowed == true,
                UseListPrice=false, RecalculateParameters=false
            };
            for(var i=0;i<12;i++) line.ParameterValues[i]=item.Parameters.ElementAtOrDefault(i) ?? "";
            foreach(var accessory in item.Accessories) line.Accessories.Add(new DemoOrderLine
            { AccessoryId=accessory.AccessoryId, Name=accessory.Name ?? $"#{accessory.AccessoryId}", Kind="Priedai",
                Unit=accessory.Unit ?? "", Quantity=accessory.Quantity ?? 0, Price=accessory.UnitPrice ?? 0 });
            order.Lines.Add(line);
        }
        foreach(var duty in snapshot.Duties) order.Employees.Add(new DemoResponsibleEmployee
        { DutyId=duty.DutyId, EmployeeId=duty.EmployeeId, Duty=duty.DutyName ?? $"#{duty.DutyId}", Name=duty.EmployeeName ?? $"#{duty.EmployeeId}", Date=DateOnly.FromDateTime(duty.Date) });
        foreach(var payment in snapshot.Payments ?? []) order.Payments.Add(new DemoPayment
        { LegacyId=payment.Id, Date=payment.Date ?? default, HasDate=payment.Date.HasValue, Amount=payment.Amount,
            Method=payment.MethodName ?? $"Payment #{payment.MethodId}",
            Reference=payment.PpkReceipt ?? payment.BankTransferId?.ToString(CultureInfo.InvariantCulture) ?? "",
            ReceiverOrRegister=payment.RegisterName ?? payment.EmployeeName ?? payment.RegisterId?.ToString(CultureInfo.InvariantCulture) ?? ReferenceName(Catalog.Employees,payment.EmployeeId),
            Note=payment.Note ?? "", CurrencyId=payment.CurrencyId, CurrencyCode=payment.CurrencyCode ?? $"currency #{payment.CurrencyId}" });
        _order=order;
        _editorDate=order.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        _editorDue=order.Due.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        _editorMountingDate=order.MountingDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) ?? "";
        _headerSaved=JsonSerializer.Serialize(HeaderInput());
        if(order.StreetId is not null) _addressOptions=[new OrderAddressReferenceDto(order.StreetId.Value,$"Address #{order.StreetId}",null,null,false)];
        _configuringLine=selectedProductId is null ? null : order.Lines.FirstOrDefault(x=>x.LegacyId==selectedProductId);
        _productSaved=_configuringLine is null ? "" : ProductState(_configuringLine);
        _liveError=string.Join(" ",snapshot.Warnings);
        _addressSaved=JsonSerializer.Serialize(AddressInput());
        RestoreHeaderBuffer(headerBuffer);
        if(addressBuffer is not null){order.StreetId=addressBuffer.StreetId;order.Building=addressBuffer.Building ?? "";order.Flat=addressBuffer.Room ?? "";order.Phone=addressBuffer.Phone ?? "";order.AddressNote=addressBuffer.Note ?? "";_addressOptions=addressOptionsBuffer ?? _addressOptions;}
    }
    private async Task CreateLiveOrderAsync()
    {
        _formError="";
        if(!DemoOrderNumber.TryNormalize(_rawNumber,Today.Year,Catalog,out var number,out var series,out var error)) { _formError=error;return; }
        var seriesRow=Catalog.Series.First(x=>x.Code==series);
        if(_customerId is null || _storeId is null) { _formError="Choose a customer and store from the live references.";return; }
        if(!DateOnly.TryParse(_date,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||!DateOnly.TryParse(_due,CultureInfo.InvariantCulture,DateTimeStyles.None,out var due)) { _formError="Enter valid dates.";return; }
        _busy=true;
        try
        {
            var header=new OrderHeaderInput(seriesRow.Id,number,date,due,_customerId.Value,_storeId.Value,_managerId,_consultantId);
            var result=await Entry.CreateAsync(new CreateOrderCommand(Guid.NewGuid(),header));
            _leaving=true;
            Navigation.NavigateTo($"entry/{result.Order.Header.Id}",replace:true);
        }
        catch(SalesOrderEntryFailure ex) { _formError=ex.Message;_liveError=ex.Message; }
        finally { if(!_leaving)_busy=false; }
    }
    private async Task SaveLiveOrderAsync()
    {
        if(_order?.LegacyId is not { } id)return;
        if(!await SaveLiveProductAsync())return;
        _busy=true;_saveMessage="";
        try
        {
            var result=await Entry.SaveAsync(id,new UpdateOrderCommand(Guid.NewGuid(),_order.Revision,HeaderInput()));
            ApplySnapshot(result.Order,preserveHeader:false);_saveIsError=false;_saveMessage="Order saved.";
        }
        catch(SalesOrderEntryFailure ex){_saveIsError=true;_saveMessage=ex.Message;_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task CreateLiveProductAsync(DemoType type)
    {
        if(_busy || Entry.HasPendingCommand)return;
        if(_order?.Cancelled==true){_liveError="This order is cancelled. New products cannot be added.";return;}
        if(_order?.LegacyId is not { } id || !await LeaveProductAsync())return;
        if(type.UnsupportedReason is not null){_liveError=type.UnsupportedReason;return;}
        if(HeaderDirty){_liveError="Save the header changes before adding a product.";return;}
        _busy=true;
        try
        {
            var result=await Entry.CreateProductAsync(id,new CreateProductCommand(Guid.NewGuid(),_order.Revision,type.Id));
            ApplySnapshot(result.Order,result.ProductId);
            if(_configuringLine is { Quantity:<=0 } freshLine){freshLine.Quantity=1;freshLine.RecalculateParameters=true;}
            _selectingProduct=false;
        }
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task<bool> SaveLiveProductAsync()
    {
        if(_order?.LegacyId is not { } id || _configuringLine?.LegacyId is not { } productId || !ProductDirty)return true;
        if(!_configuringLine.CanEdit||!_order.WritesAllowed){_liveError="This product is read-only.";return false;}
        var headerBuffer=HeaderDirty?HeaderInput():null;
        ClearProductErrors();
        _busy=true;
        try
        {
            var line = _configuringLine;
            if (BusinessConfigurationDirty(line))
            {
                var wasPreviewPending = !_previewTask.IsCompleted;
                await _previewTask;
                if (line.RecalculateParameters || line.CalculationRevision is null || line.CalculationContext != CalculationContext(line))
                {
                    await UpdateLivePreviewAsync(false);
                    if (string.IsNullOrEmpty(_previewError))
                    {
                        _priceMessage = "Parameters refreshed. Review them, then click Save & back to order again.";
                        ShowNotice(_priceMessage, "warning", "Review refreshed parameters");
                    }
                    else ShowProductFailure(_previewFailure ?? new SalesOrderEntryFailure(_previewError));
                    return false;
                }
                if (wasPreviewPending)
                {
                    _priceMessage = "Parameters are ready. Review them, then click Save & back to order again.";
                    ShowNotice(_priceMessage, "warning", "Review refreshed parameters");
                    return false;
                }
            }
            var result=await Entry.ConfigureAsync(id,productId,new ConfigureProductCommand(Guid.NewGuid(),_order.Revision,ProductInput(_configuringLine)));
            ApplySnapshot(result.Order,productId);
            RestoreHeaderBuffer(headerBuffer);
            ClearProductErrors();
            _priceMessage="Product saved; parameters and amounts updated.";
            if(result.Order.Warnings.Count > 0) ShowNotice(string.Join(" ", result.Order.Warnings), "warning", "Product saved with a warning");
            else ShowNotice("The product changes were saved.", "success", "Product saved");
            return true;
        }
        catch(SalesOrderEntryFailure ex){ShowProductFailure(ex);return false;}
        finally{_busy=false;}
    }
    private void RestoreHeaderBuffer(OrderHeaderInput? input)
    {
        if(input is null||_order is null)return;
        _order.CustomerId=input.CustomerId;_order.Customer=ReferenceName(Catalog.Customers,input.CustomerId);
        _order.StoreId=input.StoreId;_order.Store=ReferenceName(Catalog.Stores,input.StoreId);
        _order.ManagerId=input.ManagerId;_order.Manager=ReferenceName(Catalog.Employees,input.ManagerId);
        _order.ConsultantId=input.ConsultantId;_order.Consultant=ReferenceName(Catalog.Employees,input.ConsultantId);
        _order.Date=input.Date;_order.Due=input.Due;_order.MountingDate=input.MountingDate;_order.PriorityId=input.PriorityId;
        _order.Note=input.Note ?? "";_order.DiscountPercent=input.DiscountPercent;
        _editorDate=input.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        _editorDue=input.Due.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        _editorMountingDate=input.MountingDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) ?? "";
    }
    private async Task BackToOrderAsync()
    {
        if(!IsLive || !ProductDirty) { _configuringLine=null; ClearProductErrors(); DismissNotice(); return; }
        if(Entry.HasPendingCommand) { _liveError="Check the outstanding save before leaving this product."; return; }
        if(!await JS.InvokeAsync<bool>("confirm", "Discard the unsaved product changes and return to the order? Previously saved values remain."))return;
        if(_order?.LegacyId is not { } id)return;
        _busy=true;
        try
        {
            // Read authoritative saved values; preserve independent header/address buffers.
            // Do not leave edited values in the order's shared in-memory product list.
            _previewGeneration++;
            ApplySnapshot(await Entry.ReadAsync(id));
            ClearProductErrors(); DismissNotice();
        }
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task<bool> LeaveProductAsync()
    {
        if(_configuringLine is null)return true;
        if(ProductDirty && !await SaveLiveProductAsync())return false;
        _configuringLine=null;return true;
    }
    private async Task OpenStandalonePriedaiAsync()
    {
        if(!await LeaveProductAsync())return;
        if(!await RefreshLiveCatalogAsync())return;
        _catalogSearch="";_selectedCategoryId=Catalog.Categories.FirstOrDefault(x=>x.Name=="Priedai")?.Id ?? 0;
        _selectingProduct=true;
    }
    private async Task SaveLiveAccessoryAsync()
    {
        if(_order?.LegacyId is not { } id||_attachedTo?.LegacyId is not { } productId||SelectedAccessory is null)return;
        if(!SelectedAccessory.HasReferencePrice && !_accessoryPriceEntered){_pickerError="This reference has no price. Enter an explicit unit price before saving.";return;}
        _busy=true;
        try
        {
            var result=await Entry.AccessoryAsync(id,productId,new SaveAccessoryCommand(Guid.NewGuid(),_order.Revision,SelectedAccessory.Id,_quantity,_price));
            ApplySnapshot(result.Order,productId);ClosePicker();
        }
        catch(SalesOrderEntryFailure ex){_pickerError=ex.Message;_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task DeleteLiveAccessoryAsync(DemoOrderLine parent,DemoOrderLine accessory)
    {
        if(_order?.LegacyId is not { } id||parent.LegacyId is not { } productId||accessory.AccessoryId is not { } accessoryId)return;
        if(!await SaveLiveProductAsync())return;
        _busy=true;
        try { var result=await Entry.DeleteAccessoryAsync(id,productId,accessoryId,new DeleteAccessoryCommand(Guid.NewGuid(),_order!.Revision));ApplySnapshot(result.Order,productId); }
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task DeleteLiveProductAsync(DemoOrderLine line)
    {
        if(_order?.LegacyId is not { } id||line.LegacyId is not { } productId)return;
        if(!line.CanDelete){_liveError="This product cannot be deleted in its current state.";return;}
        if(!await JS.InvokeAsync<bool>("confirm","Delete this product and its accessories from the saved order?"))return;
        _busy=true;
        try{var result=await Entry.DeleteProductAsync(id,productId,new DeleteProductCommand(Guid.NewGuid(),_order.Revision));ApplySnapshot(result.Order);}
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private int _priceRequestGeneration;
    private void MarkManualPrice() { if(IsLive && _configuringLine is not null) { _priceRequestGeneration++; _configuringLine.UseListPrice=false; } }
    private async Task LookupZeroPriceAsync()
    {
        if(IsLive && _configuringLine is { Price:0, WidthMm:>0, HeightMm:>0, ColourId:not null and not 0 })await LookupLivePriceAsync();
    }
    private async Task LookupLivePriceAsync()
    {
        if(!IsLive||_order is null||_configuringLine is null)return;
        var line=_configuringLine;
        if(line.ColourId is null or 0 || line.WidthMm<=0 || line.HeightMm<=0)
        {
            _priceMessage=CanEnterManualPrice ? "Enter a unit price, or select dimensions and material for a price-list lookup." : "Enter dimensions and choose material before looking up a price.";
            return;
        }
        try
        {
            var generation=++_priceRequestGeneration;
            var savedDate=SavedOrderDate;
            var query=new SalesUnitPriceQuery(line.TypeId,line.ColourId.Value,line.WidthMm,line.HeightMm,line.Quantity,savedDate);
            var result=await Entry.PriceAsync(query);
            if(generation!=_priceRequestGeneration || !ReferenceEquals(line,_configuringLine) || line.TypeId!=query.TypeId || line.ColourId!=query.ComponentId || line.WidthMm!=query.WidthMm || line.HeightMm!=query.HeightMm || line.Quantity!=query.Quantity || SavedOrderDate!=query.OrderDate)return;
            if(result.Status==SalesUnitPriceStatus.Found){_productErrors.Remove("price");line.Price=result.UnitPrice!.Value;line.UseListPrice=true;_priceMessage=_order.Date==savedDate ? "Price from legacy list." : $"Price from legacy list for saved order date {savedDate:yyyy-MM-dd}. Save the header date before quoting for another date.";}
            else
            {
                _priceMessage=$"No list price was found for {line.WidthMm} × {line.HeightMm} mm, material {line.ColourCode}, order date {savedDate:yyyy-MM-dd}. Check these values or ask for the price list to be updated.";
                ShowProductFailure(new SalesOrderEntryFailure(_priceMessage, code:"PRICE_NOT_FOUND"), focusField:false);
            }
        }
        catch(SalesOrderEntryFailure ex){_priceMessage=ex.Message;ShowProductFailure(ex, focusField:false);}
    }
    private async Task SaveLiveDutyAsync()
    {
        if(_order?.LegacyId is not { } id||_employeeDutyId is null||_employeeId is null||!DateOnly.TryParse(_employeeDate,out var date)){_employeeError="Choose a duty, employee and date.";return;}
        _busy=true;
        try{var result=await Entry.SaveDutyAsync(id,new SaveOrderDutyCommand(Guid.NewGuid(),_order.Revision,new OrderDutyInput(_employeeDutyId.Value,_employeeId.Value,date,null)));ApplySnapshot(result.Order);_employeeError="";}
        catch(SalesOrderEntryFailure ex){_employeeError=ex.Message;}
        finally{_busy=false;}
    }
    private bool _leaving;
    private int _previewGeneration;
    private Task _previewTask = Task.CompletedTask;
    private string _previewError = "";
    private SalesOrderEntryFailure? _previewFailure;
    private decimal? _requiredMaterial;
    private decimal? _availableMaterial;
    private decimal? _freeMaterial;
    private string? _shortageDisposition;
    private bool? _isMaterialShort;
    private string? _materialUnit;
    private bool _stockPreviewPending;
    private string StockMessage => _stockPreviewPending ? "Checking material availability…" : _shortageDisposition switch
    {
        "block" => "Insufficient material. Saving this configuration is blocked.",
        "warn" => "Insufficient material. The configured policy permits entry with a warning.",
        "allow" => "Material shortage. The configured policy permits entry.",
        "unknown-policy" => "The shortage rule is unavailable. This configuration cannot be saved.",
        "invalid-requirement" => "Check the required material quantity.",
        _ => "Availability is checked again when saving."
    };
    private string StockPercent => (_requiredMaterial > 0 && _availableMaterial is not null
        ? Math.Clamp(_availableMaterial.Value / _requiredMaterial.Value * 100, 0, 100) : 0).ToString("0.##",CultureInfo.InvariantCulture) + "%";

    private string _addressSearch = "";
    private IReadOnlyList<OrderAddressReferenceDto> _addressOptions = [];
    private string _addressError = "";
    private bool _accessoryPriceEntered;
    private void MarkAccessoryPrice() => _accessoryPriceEntered=true;
    private async Task GuardNavigationAsync(Microsoft.AspNetCore.Components.Routing.LocationChangingContext context)
    {
        if(_leaving || !IsLive || !(HeaderDirty || ProductDirty || AddressDirty || PaymentDirty || ConfirmationDirty || Entry.HasPendingCommand))return;
        if(!await JS.InvokeAsync<bool>("confirm","Leave this editor? Unsaved inputs will be lost. Saved legacy records remain."))context.PreventNavigation();
    }
    private Task UpdateLivePreviewAsync(bool recalculate = true) => _previewTask = RunLivePreviewAsync(recalculate);
    private async Task RunLivePreviewAsync(bool recalculate)
    {
        if(_configuringLine is not { } line)return;
        var generation=++_previewGeneration;
        _previewError = "";
        _previewFailure=null;
        _stockPreviewPending = true;
        _requiredMaterial=null; _availableMaterial=null; _freeMaterial=null;
        _isMaterialShort=null; _shortageDisposition=null;
        var calculationContext = CalculationContext(line);
        var input=new LegacyProductPreviewRequest(line.TypeId,line.WidthMm,line.HeightMm,line.Quantity,line.ColourId,line.ControlType,line.ParameterValues.Select(x=>(string?)x).ToArray(),recalculate || line.RecalculateParameters,_order?.LegacyId,line.LegacyId);
        try
        {
            await Task.Delay(180);
            if(generation!=_previewGeneration)return;
            var result=await Entry.PreviewAsync(input);
            if(generation!=_previewGeneration || !ReferenceEquals(line,_configuringLine))return;
            // A stock-only refresh must not rewrite stored PPP text/decimal
            // formatting or create a dirty edit on opening an unchanged row.
            if(input.RecalculateParameters)
                for(var i=0;i<12;i++) if(!line.ParameterManual[i])line.ParameterValues[i]=result.Parameters.ElementAtOrDefault(i) ?? "";
            line.CalculationRevision = result.CalculationRevision;
            line.CalculationContext = calculationContext;
            _requiredMaterial=result.RequiredMaterial;
            _availableMaterial=result.AvailableMaterialForLine;
            _freeMaterial=result.FreeMaterial;
            _shortageDisposition=result.ShortageDisposition;
            _isMaterialShort=result.IsShort;
            _materialUnit=result.MaterialUnit;
            _stockPreviewPending=false;
            line.RecalculateParameters=false;
            if(input.RecalculateParameters) _priceMessage="Parameters recalculated. Save to apply.";
            await InvokeAsync(StateHasChanged);
        }
        catch(SalesOrderEntryFailure ex){if(generation==_previewGeneration){_stockPreviewPending=false;_previewError=ex.Message;_previewFailure=ex;_priceMessage=ex.Message;await InvokeAsync(StateHasChanged);}}
    }
    private async Task EditAccessoryAsync(DemoOrderLine parent,DemoOrderLine accessory)
    {
        await OpenAccessoryPicker(parent);
        if(_pickerMode is null)return;
        _selectedAccessoryId=accessory.AccessoryId ?? accessory.TypeId;
        _quantity=accessory.Quantity;_price=accessory.Price;_accessoryPriceEntered=true;
    }
    private async Task FindAddressesAsync()
    {
        _addressError="";
        try{_addressOptions=await Entry.AddressesAsync(_addressSearch);}
        catch(SalesOrderEntryFailure ex){_addressError=ex.Message;}
    }
    private async Task SelectAddress(ChangeEventArgs e)
    {
        if(_order is null)return;
        if(!int.TryParse(e.Value?.ToString(),out var id)){_order.StreetId=null;return;}
        var selected=_addressOptions.FirstOrDefault(x=>x.Id==id);
        if(selected?.HasChildren==true)
        {
            try{_addressOptions=await Entry.AddressesAsync(parentId:id);_addressError=$"Choose a street/address in {selected.Name}.";}
            catch(SalesOrderEntryFailure ex){_addressError=ex.Message;}
            return;
        }
        _order.StreetId=id;_order.Street=selected?.Path ?? selected?.Name ?? "";_addressError="";
    }
    private async Task SaveLiveAddressAsync()
    {
        if(_order?.LegacyId is not { } id||_order.StreetId is not { } streetId){_addressError="Choose a street/address reference from the search results.";return;}
        if(!await SaveLiveProductAsync())return;
        var buffer=HeaderDirty?HeaderInput():null;
        _busy=true;
        try
        {
            var result=await Entry.SaveAddressAsync(id,new SaveOrderAddressCommand(Guid.NewGuid(),_order!.Revision,
                new OrderAddressInput(streetId,_order.Building,_order.Flat,_order.Phone,_order.AddressNote)));
            ApplySnapshot(result.Order,preserveAddress:false);RestoreHeaderBuffer(buffer);_addressError="Address saved.";
        }
        catch(SalesOrderEntryFailure ex){_addressError=ex.Message;}
        finally{_busy=false;}
    }

    private string? _finalPriceInput;
    private async Task SetLiveFinalPriceAsync(ChangeEventArgs e)
    {
        _finalPriceInput=e.Value?.ToString();
        if(_order?.LegacyId is not { } id)return;
        if(HeaderDirty){_discountError="Save the header changes before setting a final price.";return;}
        if(!decimal.TryParse(_finalPriceInput,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var price)||price<0){_discountError="Enter a nonnegative final price.";return;}
        if(!await SaveLiveProductAsync())return;
        _busy=true;
        try
        {
            var result=await Entry.SetFinalPriceAsync(id,new SetOrderFinalPriceCommand(Guid.NewGuid(),_order!.Revision,price));
            ApplySnapshot(result.Order);_finalPriceInput=null;_discountError="";_saveMessage="Final price saved.";
        }
        catch(SalesOrderEntryFailure ex){_discountError=ex.Message;_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task CancelLiveOrderAsync()
    {
        if(_order?.CancellationUnavailableReason is { } reason){_liveError=reason;return;}
        if(_order?.LegacyId is not { } id)return;
        if(HeaderDirty||AddressDirty||ProductDirty){_liveError="Save the current changes before cancelling the order.";return;}
        if(!await JS.InvokeAsync<bool>("confirm","Cancel this saved order? Its history remains available."))return;
        _busy=true;
        try{var result=await Entry.CancelOrderAsync(id,new CancelOrderCommand(Guid.NewGuid(),_order.Revision));ApplySnapshot(result.Order);}
        catch(SalesOrderEntryFailure ex){_liveError=ex.Message;}
        finally{_busy=false;}
    }
    private async Task RemoveEmployeeAsync(DemoResponsibleEmployee employee)
    {
        if(!IsLive){_order?.Employees.Remove(employee);return;}
        if(_order?.LegacyId is not { } id||employee.DutyId is not { } dutyId||employee.EmployeeId is not { } employeeId)return;
        _busy=true;
        try{var result=await Entry.DeleteDutyAsync(id,dutyId,employeeId,new DeleteOrderDutyCommand(Guid.NewGuid(),_order.Revision));ApplySnapshot(result.Order);_employeeError="";}
        catch(SalesOrderEntryFailure ex){_employeeError=ex.Message;}
        finally{_busy=false;}
    }

}
