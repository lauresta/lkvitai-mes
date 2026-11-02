using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.JSInterop;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

public sealed partial class SalesOrderEntryClient
{
    private const int MaximumOutboxBytes=1_048_576,MaximumBodyBytes=65_536,MaximumPendingEntries=64;
    private static readonly JsonSerializerOptions OutboxJson=new(JsonSerializerDefaults.Web);
    private readonly Dictionary<(Guid Actor,string Scope),PendingCommand> _pending=[];
    private readonly Dictionary<Guid,List<JsonElement>> _unrecognized=[];
    private readonly HashSet<Guid> _restoredActors=[];
    private readonly Dictionary<Guid,Task> _restoreTasks=[];
    private Guid? _currentActor;
    private string _activeScope="create";
    private bool _explicitScope;
    public Guid? LegacyPendingId { get; private set; }
    public string? PendingRestoreWarning { get; private set; }
    private PendingCommand? ActivePending => _currentActor is { } actor && _pending.TryGetValue((actor,_activeScope),out var command) ? command : null;
    public bool HasPendingCommand => ActivePending is not null;
    public Guid? PendingCommandId => ActivePending?.Id;
    public long? PendingOrderId => ActivePending?.OrderId;
    public IReadOnlyList<PendingSalesSave> OtherPendingSaves => _currentActor is not { } actor ? [] : _pending.Values.Where(x=>x.ActorId==actor && x.Scope!=_activeScope).Select(x=>new PendingSalesSave(x.Id,x.OrderId)).ToArray();
    private sealed record PendingCommand(int Version,Guid ActorId,Guid Id,string Scope,long? OrderId,string Method,string Path,JsonElement Body,DateTimeOffset CreatedAt)
    {
        public bool OutcomeWasUnknown {get;set;}
    }
    public void SetOrderScope(long? orderId)
    {
        _activeScope=Scope(orderId);_explicitScope=true;
    }
    private static string Scope(long? orderId)=>orderId is null ? "create" : "order:"+orderId.Value.ToString(CultureInfo.InvariantCulture);
    public static string OutboxStorageKey(Guid actor)=>"sales-entry-outbox-v2:"+actor.ToString("D");
    private async Task<Guid> BindActorAsync()
    {
        _currentActor=await credentials.SubjectAsync();
        return _currentActor ?? throw new SalesOrderEntryFailure(L["Errors.Client.SessionRenew"].Value,false,401);
    }
    public async Task RestorePendingAsync()
    {
        var actor=await BindActorAsync();
        if(_restoredActors.Contains(actor))return;
        if(_restoreTasks.TryGetValue(actor,out var running)){await running;return;}
        var task=RestoreActorAsync(actor);_restoreTasks[actor]=task;
        try{await task;}finally{_restoreTasks.Remove(actor);}
    }
    private async Task RestoreActorAsync(Guid actor)
    {
        try
        {
            var raw=await js.InvokeAsync<string?>("sessionStorage.getItem",OutboxStorageKey(actor));
            if(!string.IsNullOrWhiteSpace(raw))
            {
                if(raw.Length>MaximumOutboxBytes)
                {
                    // Preserve oversized or malformed originals separately before a future save.
                    await js.InvokeVoidAsync("sessionStorage.setItem",OutboxStorageKey(actor)+":quarantine:"+DateTimeOffset.UtcNow.Ticks,raw);
                    PendingRestoreWarning=L["Errors.Client.InputTooLarge"].Value;
                }
                else
                {
                    try
                    {
                        var elements=JsonSerializer.Deserialize<JsonElement[]>(raw,OutboxJson) ?? [];
                        foreach(var item in elements)
                        {
                            PendingCommand? command=null;
                            try{command=item.Deserialize<PendingCommand>(OutboxJson);}catch(JsonException){}
                            if(command is not null && IsRestorable(command,actor) && _pending.TryGetValue((actor,command.Scope),out var existing) && existing.Id==command.Id)continue;
                            if(command is not null && IsRestorable(command,actor) && !_pending.ContainsKey((actor,command.Scope)) && !_pending.Values.Any(x=>x.ActorId==actor && x.Id==command.Id) && _pending.Count(x=>x.Key.Actor==actor)<MaximumPendingEntries)
                            {
                                command.OutcomeWasUnknown=true;
                                _pending[(actor,command.Scope)]=command;
                            }
                            else
                            {
                                if(!_unrecognized.TryGetValue(actor,out var preserved))_unrecognized[actor]=preserved=[];
                                preserved.Add(item.Clone());
                                PendingRestoreWarning=L["Errors.Client.OlderInputNotRestored"].Value;
                            }
                        }
                    }
                    catch(JsonException)
                    {
                        await js.InvokeVoidAsync("sessionStorage.setItem",OutboxStorageKey(actor)+":quarantine:"+DateTimeOffset.UtcNow.Ticks,raw);
                        PendingRestoreWarning=L["Errors.Client.OlderInputUnreadable"].Value;
                    }
                }
            }
            var legacy=await js.InvokeAsync<string?>("sessionStorage.getItem","sales-entry-pending");
            if(Guid.TryParse(legacy,out var id))LegacyPendingId=id;
            _restoredActors.Add(actor);
        }
        catch(Exception ex) when(ex is JSException or InvalidOperationException)
        { PendingRestoreWarning=L["Errors.Client.StorageUnavailable"].Value; }
    }
    private static bool IsRestorable(PendingCommand command,Guid actor)=>command.Version==2 && command.ActorId==actor && command.Id!=Guid.Empty
        && command.Body.ValueKind==JsonValueKind.Object && command.Body.GetRawText().Length<=MaximumBodyBytes
        && TryRoute(command.Method,command.Path,out var orderId) && command.OrderId==orderId && command.Scope==Scope(orderId)
        && command.Body.TryGetProperty("commandId",out var id) && id.ValueKind==JsonValueKind.String && Guid.TryParse(id.GetString(),out var parsed) && parsed==command.Id;
    private static bool TryRoute(string method,string path,out long? orderId)
    {
        orderId=null;
        if(string.IsNullOrEmpty(method) || string.IsNullOrEmpty(path) || path.Length>512 || method.Length>8)return false;
        if(path=="/api/sales/order-entry/orders")return method=="POST";
        var match=Regex.Match(path,@"\A/api/sales/order-entry/orders/(?<order>-?[0-9]+)(?<tail>/.*)?\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(!match.Success || !long.TryParse(match.Groups["order"].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var order))return false;
        orderId=order;
        var tail=match.Groups["tail"].Value;
        return method switch
        {
            "PUT"=>Regex.IsMatch(tail,@"\A(?:|/address|/final-price|/products/-?[0-9]+)\z",RegexOptions.CultureInvariant),
            "POST"=>Regex.IsMatch(tail,@"\A/(?:products|duties|payments|confirm|cancel|products/-?[0-9]+/(?:duplicate|accessories))\z",RegexOptions.CultureInvariant),
            "DELETE"=>Regex.IsMatch(tail,@"\A/(?:products/-?[0-9]+(?:/accessories/-?[0-9]+)?|duties/-?[0-9]+/-?[0-9]+)\z",RegexOptions.CultureInvariant),
            _=>false
        };
    }
    private async Task PersistPendingAsync(Guid actor,bool required=false)
    {
        var items=_pending.Values.Where(x=>x.ActorId==actor).Cast<object>().ToList();
        if(_unrecognized.TryGetValue(actor,out var preserved))items.AddRange(preserved.Cast<object>());
        try
        {
            if(items.Count==0)await js.InvokeVoidAsync("sessionStorage.removeItem",OutboxStorageKey(actor));
            else
            {
                var json=JsonSerializer.Serialize(items,OutboxJson);
                if(json.Length>MaximumOutboxBytes)throw new SalesOrderEntryFailure(L["Errors.Client.TooMuchRecoveryInput"].Value);
                await js.InvokeVoidAsync("sessionStorage.setItem",OutboxStorageKey(actor),json);
            }
        }
        catch(Exception ex) when(ex is JSException or InvalidOperationException)
        {
            PendingRestoreWarning=L["Errors.Client.StorageUnavailable"].Value;
            if(required)throw new SalesOrderEntryFailure(L["Errors.Client.InputNotRetained"].Value);
        }
    }
    public async Task ClearRecoveredAsync(Guid commandId)
    {
        var actor=await BindActorAsync();
        var match=_pending.FirstOrDefault(x=>x.Key.Actor==actor && x.Value.Id==commandId);
        if(match.Value is not null){_pending.Remove(match.Key);await PersistPendingAsync(actor);}
    }
    public async Task ClearRecoveredLegacyAsync(Guid commandId)
    {
        if(LegacyPendingId!=commandId)return;
        try{await js.InvokeVoidAsync("sessionStorage.removeItem","sales-entry-pending");LegacyPendingId=null;}
        catch(Exception ex) when(ex is JSException or InvalidOperationException){PendingRestoreWarning=L["Errors.Client.OldMarkerNotCleared"].Value;}
    }
    public async Task<OrderEntryCommandResult> RetryPendingAsync()
    {
        await BindActorAsync();
        var command=ActivePending ?? throw new SalesOrderEntryFailure(L["Errors.Client.NoUnresolvedSave"].Value);
        return await SendPendingAsync(command);
    }
    private async Task<OrderEntryCommandResult> CommandAsync(Guid id,HttpMethod method,string path,object body)
    {
        var actor=await BindActorAsync();
        await RestorePendingAsync();
        if(!_restoredActors.Contains(actor))throw new SalesOrderEntryFailure(L["Errors.Client.RecoveryInputUnreadable"].Value);
        if(!TryRoute(method.Method,path,out var orderId))throw new SalesOrderEntryFailure(L["Errors.Client.UnsupportedSaveRoute"].Value);
        var scope=Scope(orderId);
        if(!_explicitScope)_activeScope=scope;
        if(_explicitScope && scope!=_activeScope)throw new SalesOrderEntryFailure(L["Errors.Client.EditorChanged"].Value);
        if(_pending.ContainsKey((actor,scope)))throw new SalesOrderEntryFailure(L["Errors.Client.ResolveInterruptedSave"].Value,true);
        if(_pending.Values.Any(x=>x.ActorId==actor && x.Id==id))throw new SalesOrderEntryFailure(L["Errors.Client.InputBelongsToOtherOrder"].Value,true,409,"RECOVERY_SCOPE_MISMATCH");
        if(_pending.Count(x=>x.Key.Actor==actor)>=MaximumPendingEntries)throw new SalesOrderEntryFailure(L["Errors.Client.TooManyUnresolvedSaves"].Value);
        // Clone now: later edits to caller-owned arrays must never alter an issued request.
        var frozen=JsonSerializer.SerializeToElement(body,body.GetType(),OutboxJson);
        var command=new PendingCommand(2,actor,id,scope,orderId,method.Method,path,frozen,DateTimeOffset.UtcNow);
        if(!IsRestorable(command,actor))throw new SalesOrderEntryFailure(L["Errors.Client.UnsafeRecoveryInput"].Value);
        _pending[(actor,scope)]=command;
        try{await PersistPendingAsync(actor,true);}catch{_pending.Remove((actor,scope));throw;}
        return await SendPendingAsync(command);
    }
    private async Task<OrderEntryCommandResult> SendPendingAsync(PendingCommand command)
    {
        if(await credentials.SubjectAsync()!=command.ActorId)throw new SalesOrderEntryFailure(L["Errors.Client.ReturnToOriginalAccount"].Value,true,401);
        try
        {
            var result=await SendAsync<OrderEntryCommandResult>(new HttpMethod(command.Method),command.Path,command.Body);
            if(command.OrderId is { } expected && result.Order.Header.Id!=expected)
                throw new SalesOrderEntryFailure(L["Errors.Client.ResultBelongsToOtherOrder"].Value,true,409,"RECOVERY_SCOPE_MISMATCH");
            _pending.Remove((command.ActorId,command.Scope));await PersistPendingAsync(command.ActorId);
            return result;
        }
        catch(SalesOrderEntryFailure ex)
        {
            if(ex.OutcomeUnknown){command.OutcomeWasUnknown=true;await PersistPendingAsync(command.ActorId);}
            else if(command.OutcomeWasUnknown)
                throw new SalesOrderEntryFailure(L["Errors.Client.EarlierSaveUnconfirmed",ex.Message].Value,true,ex.Status,ex.Code);
            else{_pending.Remove((command.ActorId,command.Scope));await PersistPendingAsync(command.ActorId);}
            throw;
        }
    }
}
public sealed record PendingSalesSave(Guid Id,long? OrderId);
