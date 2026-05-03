namespace LKvitai.MES.Modules.Frontline.Application.Ports;

/// <summary>
/// Side-effect port the API endpoint calls after a successful fabric look-up
/// to record an audit-log row plus a "last checked" stamp on the master
/// component. Implementations MUST never throw on infrastructural failures —
/// audit writes never fail the look-up response (the operator on the floor
/// must still see the stock card even if the bookkeeping table is unhappy).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>Sql</b> implementation calls <c>dbo.mes_Fabric_RecordLookup</c>
///   (F-2.1), which transactionally updates
///   <c>TBD_Components.mes_LastCheckedAt</c> and inserts into
///   <c>dbo.mes_FabricAvailabilityCheckLog</c>.</item>
///   <item><b>Stub</b> implementation is a no-op — keeps the WebUI / unit
///   tests free from any database wiring while still letting the endpoint
///   call the recorder unconditionally.</item>
/// </list>
/// </remarks>
public interface IFabricLookupRecorder
{
    /// <summary>
    /// Persist a single fabric look-up attempt.
    /// </summary>
    /// <param name="code">Normalised fabric code (already trimmed and
    /// uppercased by the caller). Empty / whitespace-only values are
    /// silently dropped — they cannot identify a valid audit row.</param>
    /// <param name="checkedBy">Operator identity, or <c>null</c> when
    /// unknown. Frontline endpoints are anonymous in F-2 (operator
    /// identity lands in F-3); the SQL proc treats <c>NULL</c> as
    /// "operator unknown" rather than rejecting the row.</param>
    /// <param name="cancellationToken">Caller cancellation. Implementations
    /// MUST propagate <see cref="OperationCanceledException"/> on this
    /// token but swallow every other failure.</param>
    Task RecordAsync(string code, string? checkedBy, CancellationToken cancellationToken);
}
