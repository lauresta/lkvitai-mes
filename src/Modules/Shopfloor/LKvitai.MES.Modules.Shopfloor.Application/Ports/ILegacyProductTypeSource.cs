namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

/// <summary>A single legacy product type row pulled from the source system.</summary>
public sealed record LegacyProductTypeRow(string Code, string KindName, string Name);

/// <summary>
/// Read-only source of legacy product types (the legacy LKvitaiDb SQL Server in
/// production). Implemented in Infrastructure.
/// </summary>
public interface ILegacyProductTypeSource
{
    Task<IReadOnlyList<LegacyProductTypeRow>> FetchAsync(CancellationToken cancellationToken);
}
