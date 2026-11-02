using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>Fails closed before any physical connection; used by validation and
/// trusted-identity boundary tests, never as a production fallback.</summary>
internal sealed class RejectingLegacySqlConnectionFactory : ILegacySqlConnectionFactory
{
    public int OpenAttempts { get; private set; }
    public Task<LegacySqlConnectionLease> OpenAsync(CancellationToken cancellationToken)
    {
        OpenAttempts++;
        throw new OrderEntryException("LEGACY_MAPPING_REQUIRED", "No SQL identity is mapped to this actor.", 403);
    }
}
