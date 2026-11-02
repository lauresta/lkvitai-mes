using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.References;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// Strict reference mappings scoped by the acting SQL login's Loc_leidimas UKS.
/// Scope is checked independently again inside each mutation transaction.
/// </summary>
public sealed class SqlSalesOrderEntryReferences : ISalesOrderEntryReferences
{
    private readonly SalesSqlOptions _options;
    private readonly ILegacySqlConnectionFactory _connections;

    public SqlSalesOrderEntryReferences(SalesSqlOptions options, ILegacySqlConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Live references require a SQL connection.", nameof(options));
        _options = options;
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    public async Task<PagedResult<OrderSeriesReferenceDto>> GetSeriesAsync(
        string? search, int page, int pageSize, bool includeHidden,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await using var lease = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        // Two result sets preserve Total even for an empty out-of-range page.
        const string sql = """
            SELECT COUNT(*) FROM dbo.Zinynas_UzsakymoSerijos s
            WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID)
              AND (@IncludeHidden = 1 OR s.bHideInApplicationFlag = 0)
              AND (@Search IS NULL OR s.UzsakymoSerijaPav LIKE @Search ESCAPE N'\');
            SELECT s.UzsakymoSerijaID, s.UzsakymoSerijaPav, s.FilialoID,
                   customer.UzsakovoID AS UzaskovoID, place.VietaID, manager.DarbuotojoID AS Vad_DarbuotojasID,
                   consultant.DarbuotojoID AS Kons_DarbuotojoID, s.bHideInApplicationFlag
            FROM dbo.Zinynas_UzsakymoSerijos s
            LEFT JOIN dbo.Zinynas_uzsakovai customer ON customer.UzsakovoID=s.UzaskovoID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=customer.PriskirtasFilialui)
            LEFT JOIN dbo.Zinynas_vieta place ON place.VietaID=s.VietaID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=place.Filialas)
            LEFT JOIN dbo.Zinynas_darbuotojai manager ON manager.DarbuotojoID=s.Vad_DarbuotojasID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=manager.FilialasID)
            LEFT JOIN dbo.Zinynas_darbuotojai consultant ON consultant.DarbuotojoID=s.Kons_DarbuotojoID AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=consultant.FilialasID)
            WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=s.FilialoID)
              AND (@IncludeHidden = 1 OR s.bHideInApplicationFlag = 0)
              AND (@Search IS NULL OR s.UzsakymoSerijaPav LIKE @Search ESCAPE N'\')
            ORDER BY s.UzsakymoSerijaPav, s.UzsakymoSerijaID
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add("@IncludeHidden", SqlDbType.Bit).Value = includeHidden;
        AddPageAndSearch(command, search, page, pageSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var total = await ReadTotalAsync(reader, cancellationToken).ConfigureAwait(false);
        var rows = new List<OrderSeriesReferenceDto>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var branchId = NullableInt(reader, "FilialoID");
            rows.Add(new OrderSeriesReferenceDto(
                reader.GetInt32(reader.GetOrdinal("UzsakymoSerijaID")),
                NullableString(reader, "UzsakymoSerijaPav"), branchId,
                NullableInt(reader, "UzaskovoID"), NullableInt(reader, "VietaID"),
                NullableInt(reader, "Vad_DarbuotojasID"), NullableInt(reader, "Kons_DarbuotojoID"),
                reader.GetBoolean(reader.GetOrdinal("bHideInApplicationFlag")), branchId == 7));
        }
        return new PagedResult<OrderSeriesReferenceDto>(rows, total, page, pageSize);
    }

    public async Task<IReadOnlyList<ProductCategoryReferenceDto>> GetCategoriesAsync(
        bool includeInactive, CancellationToken cancellationToken)
    {
        await using var lease = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        const string sql = """
            SELECT RusiesID, RusiesPavadinimas, Naudojamas, TeikiamaNuolaida
            FROM dbo.Zinynas_rusys
            WHERE @IncludeInactive = 1 OR Naudojamas = 1
            ORDER BY RRusiavimas, RusiesPavadinimas, RusiesID;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add("@IncludeInactive", SqlDbType.Bit).Value = includeInactive;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ProductCategoryReferenceDto>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new ProductCategoryReferenceDto(
                reader.GetInt32(reader.GetOrdinal("RusiesID")), NullableString(reader, "RusiesPavadinimas"),
                NullableBool(reader, "Naudojamas") == true, NullableBool(reader, "TeikiamaNuolaida")));
        return rows;
    }

    public async Task<PagedResult<ProductTypeReferenceDto>> GetTypesAsync(
        int? categoryId, string? search, int page, int pageSize, bool includeInactive,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await using var lease = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        // New choices require both an active type and an active category.
        // Historical orphan/inactive references remain readable explicitly.
        const string sql = """
            SELECT COUNT(*) FROM dbo.Zinynas_tipai t
            LEFT JOIN dbo.Zinynas_rusys c ON c.RusiesID = t.Rusis
            WHERE (@CategoryId IS NULL OR t.Rusis = @CategoryId)
              AND (@IncludeInactive = 1 OR (t.Naudojamas = 1 AND c.Naudojamas = 1))
              AND (@Search IS NULL OR t.TipoPavadinimas LIKE @Search ESCAPE N'\');
            SELECT t.TipoID, t.Rusis, t.TipoPavadinimas, t.Naudojamas, t.Gamininamas
            FROM dbo.Zinynas_tipai t
            LEFT JOIN dbo.Zinynas_rusys c ON c.RusiesID = t.Rusis
            WHERE (@CategoryId IS NULL OR t.Rusis = @CategoryId)
              AND (@IncludeInactive = 1 OR (t.Naudojamas = 1 AND c.Naudojamas = 1))
              AND (@Search IS NULL OR t.TipoPavadinimas LIKE @Search ESCAPE N'\')
            ORDER BY c.RRusiavimas, t.Rusis, t.Rusiavimas, t.TipoPavadinimas, t.TipoID
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = (object?)categoryId ?? DBNull.Value;
        command.Parameters.Add("@IncludeInactive", SqlDbType.Bit).Value = includeInactive;
        AddPageAndSearch(command, search, page, pageSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var total = await ReadTotalAsync(reader, cancellationToken).ConfigureAwait(false);
        var rows = new List<ProductTypeReferenceDto>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadType(reader));
        return new PagedResult<ProductTypeReferenceDto>(rows, total, page, pageSize);
    }

    public async Task<ProductTypeDefinitionDto?> GetTypeDefinitionAsync(
        int typeId, CancellationToken cancellationToken)
    {
        await using var lease = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var connection = lease.Connection;
        // Slot names are fixed internal constants, never user-provided SQL.
        var slots = Enumerable.Range(1, 12).SelectMany(i => new[]
        {
            $"c.P{i:D2}", $"t.PP{i:D2}", $"t.NPP{i:D2}",
        });
        var sql = """
            SELECT t.TipoID, t.Rusis, t.TipoPavadinimas, t.Naudojamas, t.Gamininamas,
                   c.RusiesPavadinimas, c.Naudojamas AS CategoryActive,
                   c.KainosIvestis, t.KainosMatas, t.MinKainosPlotis,
                   t.MinKainosAukstis, t.MinKainosPlotas,
            """ + string.Join(", ", slots) + """
             FROM dbo.Zinynas_tipai t
             LEFT JOIN dbo.Zinynas_rusys c ON c.RusiesID = t.Rusis
             WHERE t.TipoID = @TypeId;
            """;
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add("@TypeId", SqlDbType.Int).Value = typeId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var parameters = Enumerable.Range(1, 12).Select(i => new ProductParameterDefinitionDto(
            i, NullableString(reader, $"P{i:D2}"), NullableString(reader, $"NPP{i:D2}"),
            NullableInt(reader, $"PP{i:D2}"),
            i == 12 ? ProductParameterStorageKind.Decimal : ProductParameterStorageKind.Text)).ToArray();
        var type = ReadType(reader);
        var mode = NullableString(reader, "KainosIvestis");
        var pricing = new ProductPricingDefinitionDto(
            LegacyPriceEntryRules.NormalizeMode(mode), NullableInt(reader, "KainosMatas"),
            NullableDecimal(reader, "MinKainosPlotis"), NullableDecimal(reader, "MinKainosAukstis"),
            NullableDecimal(reader, "MinKainosPlotas"),
            LegacyPriceEntryRules.SupportsManualUnitPriceForType(type.Id, mode));
        return new ProductTypeDefinitionDto(type, NullableString(reader, "RusiesPavadinimas"),
            NullableBool(reader, "CategoryActive") == true, parameters, pricing);
    }

    private SqlCommand CreateCommand(SqlConnection connection, string text) => new(text, connection)
    {
        CommandType = CommandType.Text,
        CommandTimeout = _options.CommandTimeoutSeconds > 0 ? _options.CommandTimeoutSeconds : 30,
    };

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if ((long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page));
    }

    private static void AddPageAndSearch(SqlCommand command, string? search, int page, int pageSize)
    {
        var text = search?.Trim();
        if (text?.Length > 100) throw new ArgumentException("Search is limited to 100 characters.", nameof(search));
        // Literal substring search: percent, underscore, bracket and escape
        // characters must not become accidental wildcard queries.
        var pattern = string.IsNullOrEmpty(text) ? null : "%" + text.Replace("\\", "\\\\")
            .Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 202).Value = (object?)pattern ?? DBNull.Value;
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
    }

    private static async Task<int> ReadTotalAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new DataException("Reference count result is missing.");
        var total = reader.GetInt32(0);
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            throw new DataException("Reference page result is missing.");
        return total;
    }

    private static ProductTypeReferenceDto ReadType(SqlDataReader reader) => new(
        reader.GetInt32(reader.GetOrdinal("TipoID")), NullableInt(reader, "Rusis"),
        NullableString(reader, "TipoPavadinimas"), NullableBool(reader, "Naudojamas") == true,
        NullableBool(reader, "Gamininamas"));

    // GetOrdinal and typed getters intentionally fail on schema drift rather
    // than converting missing IDs, null foreign keys or incompatible types to 0.
    private static string? NullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? NullableInt(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static bool? NullableBool(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private static decimal? NullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }
}
