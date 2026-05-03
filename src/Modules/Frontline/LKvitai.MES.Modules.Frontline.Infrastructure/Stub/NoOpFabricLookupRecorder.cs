using LKvitai.MES.Modules.Frontline.Application.Ports;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Stub;

/// <summary>
/// In-memory <see cref="IFabricLookupRecorder"/> that drops every audit
/// request on the floor. Registered when
/// <c>Frontline:FabricDataSource = "Stub"</c>, and used by unit / WebUI
/// smoke tests so the lookup endpoint can be exercised without spinning up
/// a SQL Server. Swallows everything silently — keeps the stub flow
/// indistinguishable from the SQL flow as far as the endpoint is concerned.
/// </summary>
public sealed class NoOpFabricLookupRecorder : IFabricLookupRecorder
{
    public Task RecordAsync(string code, string? checkedBy, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
