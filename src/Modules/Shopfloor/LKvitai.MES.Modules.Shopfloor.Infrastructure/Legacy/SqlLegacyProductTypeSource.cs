using System.Globalization;
using LKvitai.MES.Modules.Shopfloor.Application.Configuration;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Legacy;

/// <summary>
/// Reads legacy product types directly from the LKvitaiDb SQL Server. The
/// connection string is supplied by the Api composition root via
/// <see cref="SqlLegacyProductTypeSourceOptions"/>.
/// </summary>
public sealed class SqlLegacyProductTypeSource : ILegacyProductTypeSource
{
    private const string Query = """
        select
            t.TipoID,
            zr.RusiesPavadinimas,
            t.TipoTrPavadinimas
        from dbo.Zinynas_tipai t
        join dbo.Zinynas_rusys zr on zr.RusiesID = t.Rusis
        where zr.Naudojamas <> 0
          and t.Naudojamas <> 0
          and t.Gamininamas <> 0
          and t.TipoID <> 504437610
        """;

    private readonly SqlLegacyProductTypeSourceOptions _options;
    private readonly LegacyProductTypesOptions _syncOptions;
    private readonly ILogger<SqlLegacyProductTypeSource> _logger;

    public SqlLegacyProductTypeSource(
        SqlLegacyProductTypeSourceOptions options,
        LegacyProductTypesOptions syncOptions,
        ILogger<SqlLegacyProductTypeSource> logger)
    {
        _options = options;
        _syncOptions = syncOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LegacyProductTypeRow>> FetchAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'LKvitaiDb' is required for the legacy product type sync.");
        }

        var rows = new List<LegacyProductTypeRow>();

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = Query;
        command.CommandTimeout = _syncOptions.CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var tipoId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var kindName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

            rows.Add(new LegacyProductTypeRow(
                tipoId.ToString(CultureInfo.InvariantCulture),
                kindName,
                name));
        }

        _logger.LogInformation("[Shopfloor] fetched {Count} legacy product types from LKvitaiDb", rows.Count);
        return rows;
    }
}

/// <summary>Holds the resolved LKvitaiDb connection string for the SQL source.</summary>
public sealed class SqlLegacyProductTypeSourceOptions
{
    public string? ConnectionString { get; set; }
}
