using LKvitai.MES.Modules.Shopfloor.Application.Ports;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Legacy;

/// <summary>
/// Deterministic fixture source for local dev / tests when
/// <c>Shopfloor:LegacyProductTypes:DataSource = "Stub"</c>. Avoids needing a
/// live LKvitaiDb SQL Server.
/// </summary>
public sealed class StubLegacyProductTypeSource : ILegacyProductTypeSource
{
    private static readonly IReadOnlyList<LegacyProductTypeRow> Rows = new[]
    {
        new LegacyProductTypeRow("1001", "Roletai", "Roletas Standart"),
        new LegacyProductTypeRow("1002", "Roletai", "Roletas Dual"),
        new LegacyProductTypeRow("1003", "Zaliuzes", "Zaliuze 25mm"),
        new LegacyProductTypeRow("1004", "Zaliuzes", "Zaliuze 50mm"),
        new LegacyProductTypeRow("1005", "Plisuotos", "Plisuota uzuolaida"),
        new LegacyProductTypeRow("1006", "Rulonines", "Rulonine diena-naktis"),
    };

    public Task<IReadOnlyList<LegacyProductTypeRow>> FetchAsync(CancellationToken cancellationToken)
        => Task.FromResult(Rows);
}
