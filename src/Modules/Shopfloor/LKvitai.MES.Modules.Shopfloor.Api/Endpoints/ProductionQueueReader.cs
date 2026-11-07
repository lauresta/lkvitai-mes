using System.Data;
using System.Net;
using System.Net.Http.Json;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Live read-only Access queue under the mapped Cikada SQL identity.
/// Every request uses a new, unpooled physical connection.</summary>
public sealed class ProductionQueueReader(IHttpContextAccessor accessor, IHttpClientFactory http,
    CikadaUserBridgeProof proof, CikadaSsoAccountLookup accounts, IConfiguration configuration,
    ILogger<ProductionQueueReader> logger)
{
    // Access modMeniu opens frmBlindsProduction through this menu action.
    internal const int ProductionTaskMenuId = 49;
    // Access opens dlgPrintDocuments from frmMadeProductionDocuments too.
    internal const int FinishedGoodsMenuId = 41;
    private sealed record Mapping(Guid UserId, string? LegacySqlLogin);

    public async Task<ProductionQueuePage> ReadAsync(int page, int pageSize, string? orderNumber,
        long? departmentId, long? seriesId, long? categoryId, long? typeId,
        long? priorityId, CancellationToken ct)
        => await WithActorConnectionAsync((connection, token) =>
            ReadRowsAsync(connection, page, pageSize, orderNumber, departmentId, seriesId,
                categoryId, typeId, priorityId, token), ct, ProductionTaskMenuId);

    public Task<IReadOnlyList<ProductionQueueFacet>> ReadFacetsAsync(CancellationToken ct)
        => WithActorConnectionAsync(ReadFacetsRowsAsync, ct, ProductionTaskMenuId);

    internal async Task<T> WithActorConnectionAsync<T>(Func<SqlConnection, CancellationToken, Task<T>> action,
        CancellationToken ct, int? requiredMenuId = null, int? alternativeMenuId = null)
    {
        var actor = await ResolveActorAsync(ct);
        var login = await ResolveLoginAsync(actor, ct);
        var connectionString = configuration.GetConnectionString("LKvitaiDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw Denied("QUEUE_UNAVAILABLE", "Production queue is unavailable.", 503);
        var cs = new SqlConnectionStringBuilder(connectionString)
        { Pooling = false, MultipleActiveResultSets = false, Enlist = false };
        await using var connection = new SqlConnection(cs.ConnectionString);
        await connection.OpenAsync(ct);
        var original = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(128),ORIGINAL_LOGIN())", ct);
        var effective = await ScalarStringAsync(connection, "SELECT CONVERT(nvarchar(128),SYSTEM_USER)", ct);
        if (original is null || !Same(original, effective))
            throw Denied("LEGACY_SQL_CONTEXT_INVALID", "The legacy SQL connection has an unexpected security context.", 403);
        var canonical = await ResolvePrincipalAsync(connection, login, original, ct);
        var cookie = await SwitchAsync(connection, canonical, original, ct);
        try
        {
            if (requiredMenuId is not null)
                await EnsureMenuPermissionAsync(connection, requiredMenuId.Value, alternativeMenuId, ct);
            return await action(connection, ct);
        }
        finally { await RestoreBestEffortAsync(connection, cookie, original); }
    }

    private static async Task EnsureMenuPermissionAsync(SqlConnection connection, int menuId,
        int? alternativeMenuId, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT CASE WHEN SYSTEM_USER=N'sa' OR EXISTS (
                SELECT 1 FROM dbo.PROJECT_TBD_UserMenus
                WHERE sUserName=SYSTEM_USER AND iProjectMenuButtonID IN (@menuId,@alternativeMenuId)
                  AND bItemEnabledFlag=1) THEN 1 ELSE 0 END;
            """, connection);
        command.Parameters.Add("@menuId", SqlDbType.Int).Value = menuId;
        command.Parameters.Add("@alternativeMenuId", SqlDbType.Int).Value =
            (object?)alternativeMenuId ?? DBNull.Value;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw Denied("PRODUCTION_MENU_DISABLED", "Your legacy SQL account cannot open this production screen.", 403);
    }

    internal async Task<Guid> ResolveActorAsync(CancellationToken ct)
    {
        var user = accessor.HttpContext?.User;
        var identities = user?.Identities.Where(x => x.IsAuthenticated).ToArray() ?? [];
        var identity = identities.Length == 1 ? identities[0] : null;
        var cookie = identity?.AuthenticationType == "Cookies";
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString() ?? "";
        var ssoBearer = identity?.AuthenticationType == PortalStructuredBearerAuthenticationDefaults.Scheme
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && CikadaJwtValidator.LooksLikeJwt(authorization["Bearer ".Length..])
            && identity.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource;
        // The older structured bearer is unsigned and cannot select a SQL actor.
        if ((!cookie && !ssoBearer) || user!.HasClaim(SalesCredentialProtector.ReviewClaim, "true"))
            throw Denied("AUTHENTICATED_ACTOR_REQUIRED", "Sign in to use production.", 401);
        if (ssoBearer || identity!.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource)
        {
            try
            {
                return (await accounts.ResolveAsync(user, ct))?.UserId
                    ?? throw Denied("AUTHENTICATED_ACTOR_REQUIRED", "An active Cikada account is required.", 401);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
                or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            { throw Denied("IDENTITY_SERVICE_UNAVAILABLE", "The user access service is unavailable.", 503); }
        }
        return SalesCredentialProtector.Subject(identity)
            ?? throw Denied("AUTHENTICATED_ACTOR_REQUIRED", "Sign in to use production.", 401);
    }

    private async Task<string> ResolveLoginAsync(Guid actor, CancellationToken ct)
    {
        using var lookup = new HttpRequestMessage(HttpMethod.Get, "/api/internal/legacy-sql-identity");
        lookup.Headers.Add(CikadaUserBridgeProof.HeaderName,
            proof.Create(actor, CikadaUserBridgeProof.LookupLegacySql));
        Mapping? mapping;
        try
        {
            using var response = await http.CreateClient("CikadaUsers").SendAsync(lookup, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                throw Denied("LEGACY_MAPPING_REQUIRED", "Your Cikada account needs a legacy SQL login mapping.", 403);
            if (!response.IsSuccessStatusCode)
                throw Denied("IDENTITY_SERVICE_UNAVAILABLE", "The user access service is unavailable.", 503);
            mapping = await response.Content.ReadFromJsonAsync<Mapping>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
            or TaskCanceledException && !ct.IsCancellationRequested)
        { throw Denied("IDENTITY_SERVICE_UNAVAILABLE", "The user access service is unavailable.", 503); }
        var login = mapping?.LegacySqlLogin;
        if (mapping?.UserId != actor || string.IsNullOrWhiteSpace(login) || login.Length > 128
            || login != login.Trim() || login.Any(char.IsControl))
            throw Denied("LEGACY_MAPPING_REQUIRED", "Your Cikada account needs a valid legacy SQL login mapping.", 403);
        return login;
    }

    private static async Task<string> ResolvePrincipalAsync(SqlConnection connection, string login,
        string original, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            DECLARE @canonical sysname, @sid varbinary(85);
            SELECT @canonical=name FROM sys.server_principals
            WHERE name=@login AND type IN ('S','U') AND is_disabled=0;
            IF @canonical IS NULL AND CHARINDEX(N'\',@login)>1
               AND NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@login)
            BEGIN
                SET @sid=SUSER_SID(@login);
                IF @sid IS NOT NULL
                BEGIN
                    SET @canonical=SUSER_SNAME(@sid);
                    IF @canonical<>@login SET @canonical=NULL;
                END;
            END;
            SELECT @canonical;
            """, connection);
        command.Parameters.Add("@login", SqlDbType.NVarChar, 128).Value = login;
        var canonical = await command.ExecuteScalarAsync(ct) as string;
        if (canonical is null)
            throw Denied("LEGACY_SQL_LOGIN_UNAVAILABLE", "Your mapped legacy SQL login is unavailable.", 403);
        if (Same(canonical, original) && !Same(canonical, "sa"))
            throw Denied("LEGACY_SQL_SERVICE_LOGIN_FORBIDDEN", "This mapping cannot use the service login.", 403);
        return canonical;
    }

    private static async Task<byte[]> SwitchAsync(SqlConnection connection, string login,
        string original, CancellationToken ct)
    {
        // WITH COOKIE requires an ad hoc EXECUTE AS batch; SQL Server rejects
        // SqlClient's parameterized sp_executesql form (error 15590). The only
        // literal is the canonical enabled principal read from sys.server_principals
        // or SID-resolved Windows identity, and its quote is escaped below.
        var literal = login.Replace("'", "''", StringComparison.Ordinal);
        var switchSql = $"""
            DECLARE @cookie varbinary(8000);
            EXECUTE AS LOGIN = N'{literal}' WITH COOKIE INTO @cookie;
            SELECT @cookie, CONVERT(nvarchar(128),SYSTEM_USER),
                   CONVERT(nvarchar(128),ORIGINAL_LOGIN()),
                   HAS_PERMS_BY_NAME(NULL,NULL,'CONNECT SQL'),
                   HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONNECT');
            """;
        await using var command = new SqlCommand(switchSql, connection); // NOSONAR: SQL Server rejects parameterized EXECUTE AS WITH COOKIE.
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new DataException("SQL context switch failed.");
        var cookie = (byte[])reader.GetValue(0);
        if (cookie.Length is 0 or > 8000 || !Same(reader.GetString(1), login)
            || !Same(reader.GetString(2), original)
            || reader.IsDBNull(3) || reader.GetInt32(3) != 1
            || reader.IsDBNull(4) || reader.GetInt32(4) != 1)
            throw Denied("LEGACY_SQL_CONTEXT_INVALID", "Your legacy SQL identity could not be established.", 403);
        return cookie;
    }

    private static async Task<ProductionQueuePage> ReadRowsAsync(SqlConnection connection,
        int page, int pageSize, string? orderNumber, long? departmentId, long? seriesId,
        long? categoryId, long? typeId, long? priorityId, CancellationToken ct)
    {
        await using var command = new SqlCommand(QueueSql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@seriesId", SqlDbType.BigInt).Value = (object?)seriesId ?? DBNull.Value;
        command.Parameters.Add("@departmentId", SqlDbType.BigInt).Value = (object?)departmentId ?? DBNull.Value;
        command.Parameters.Add("@categoryId", SqlDbType.BigInt).Value = (object?)categoryId ?? DBNull.Value;
        command.Parameters.Add("@orderNumber", SqlDbType.NVarChar, 80).Value = (object?)orderNumber ?? DBNull.Value;
        command.Parameters.Add("@typeId", SqlDbType.BigInt).Value = (object?)typeId ?? DBNull.Value;
        command.Parameters.Add("@priorityId", SqlDbType.BigInt).Value = (object?)priorityId ?? DBNull.Value;
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@take", SqlDbType.Int).Value = pageSize + 1;
        var items = new List<ProductionQueueItem>(pageSize + 1);
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct)) items.Add(MapRow(rows));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(pageSize);
        return new ProductionQueuePage(page, pageSize, hasMore, items);
    }

    private static async Task<IReadOnlyList<ProductionQueueFacet>> ReadFacetsRowsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        await using var command = new SqlCommand(FacetsSql, connection) { CommandTimeout = 30 };
        var facets = new List<ProductionQueueFacet>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            facets.Add(new ProductionQueueFacet(rows.GetString(0), Convert.ToInt64(rows.GetValue(1)),
                rows.GetString(2), rows.IsDBNull(3) ? null : Convert.ToInt64(rows.GetValue(3)),
                rows.GetInt32(4), rows.GetDecimal(5)));
        return facets;
    }

    private static ProductionQueueItem MapRow(SqlDataReader row) => new(
        Convert.ToInt64(row.GetValue(0)), Convert.ToInt64(row.GetValue(1)), row.GetString(2), row.GetDateTime(3),
        row.IsDBNull(4) ? null : row.GetDateTime(4), Convert.ToInt64(row.GetValue(5)), Convert.ToInt64(row.GetValue(6)),
        Convert.ToInt64(row.GetValue(7)), row.GetString(8), Convert.ToInt64(row.GetValue(9)), row.GetString(10),
        row.IsDBNull(11) ? null : row.GetString(11), row.IsDBNull(12) ? null : row.GetString(12),
        row.IsDBNull(13) ? null : Convert.ToDecimal(row.GetValue(13)),
        row.IsDBNull(14) ? null : Convert.ToDecimal(row.GetValue(14)), row.GetDecimal(15),
        row.IsDBNull(16) ? null : row.GetString(16),
        row.IsDBNull(17) ? null : Convert.ToInt64(row.GetValue(17)), Convert.ToInt64(row.GetValue(18)),
        Convert.ToInt64(row.GetValue(19)), row.GetString(20), row.GetString(21),
        row.GetString(22), row.IsDBNull(23) ? null : row.GetString(23));

    private async Task RestoreBestEffortAsync(SqlConnection connection, byte[] cookie, string original)
    {
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var restore = new SqlCommand("""
                IF @@TRANCOUNT <> 0 THROW 51000,'A transaction remains active.',1;
                REVERT WITH COOKIE = @cookie;
                SELECT CONVERT(nvarchar(128),SYSTEM_USER),CONVERT(nvarchar(128),ORIGINAL_LOGIN());
                """, connection);
            restore.Parameters.Add("@cookie", SqlDbType.VarBinary, 8000).Value = cookie;
            await using var reader = await restore.ExecuteReaderAsync(cleanup.Token);
            if (!await reader.ReadAsync(cleanup.Token)
                || !Same(reader.GetString(0), original) || !Same(reader.GetString(1), original))
                throw new DataException("Original SQL context was not restored.");
        }
        catch (Exception ex)
        {
            // Pooling is disabled: disposal always discards the physical session.
            logger.LogError(ex, "Legacy production SQL context restoration failed; discarding the session.");
        }
        finally { Array.Clear(cookie); }
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static ProductionQueueAccessException Denied(string code, string message, int status) => new(code, message, status);
    private static async Task<string?> ScalarStringAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(ct) as string;
    }

    // Parenthesized eligibility is the five OR branches in SP_fsubProduction,
    // factored without changing its result. The explicit ACL is outside the OR.
    internal const string QueueSourceSql = """
        FROM dbo.ParduotiGaminiai pg
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID=u.UzsakymoSerija
        JOIN dbo.Zinynas_filialai f ON f.FilialoID=us.FilialoID
        JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
        JOIN dbo.Zinynas_busenos b ON b.BusenosID=pg.BusenosID
        JOIN dbo.Zinynas_rusys r ON r.RusiesID=t.Rusis
        JOIN dbo.DIC_Priorities p ON p.PriorityID=u.PriorityID
        JOIN dbo.Zinynas_uzsakovai cs ON cs.UzsakovoID=u.UzsakovoID
        LEFT JOIN dbo.TBD_Components c ON c.iComponentID=pg.Spalva
        WHERE EXISTS (SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=us.FilialoID)
          AND t.Gamininamas<>0 AND b.GalimaGamintiGamini<>0
          AND t.Rusis<>985019188
          AND (CASE WHEN u.sNotes=N'Kliento prasymu anuliuotas' THEN 0 ELSE u.Sugadintas END)=0
          AND (u.Patvirtintas<>0 OR pg.BusenosID IN (1173102375,1173102377))
          AND (DATEDIFF(day,dbo.DATETRIM(u.UzsakymoData),GETDATE())<>0
               OR p.PriorityID>2 OR cs.bIsVIPClientFlag<>0)
        """;

    private static readonly string QueueSql = $"""
        SELECT pg.ParduotoGaminioID, pg.UzsakymoID,
            us.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe,
            u.UzsakymoData, u.UzsakymoTerminas, u.UzsakymoSerija, us.FilialoID,
            r.RusiesID, r.RusiesPavadinimas, t.TipoID, t.TipoPavadinimas,
            pg.Valdymas, c.sCodeInternal, pg.Plotis, pg.Aukstis, pg.Kiekis,
            pg.Pastabos, pg.GamibinesUzduotiesID, pg.BusenosID,
            p.PriorityID, p.sPriority, f.FilialoPavadinimas, b.Busena, pg.iBarCode
        {QueueSourceSql}
          AND (@departmentId IS NULL OR us.FilialoID=@departmentId)
          AND (@seriesId IS NULL OR u.UzsakymoSerija=@seriesId)
          AND (@categoryId IS NULL OR r.RusiesID=@categoryId)
          AND (@orderNumber IS NULL OR us.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe=@orderNumber)
          AND (@typeId IS NULL OR t.TipoID=@typeId)
          AND (@priorityId IS NULL OR u.PriorityID=@priorityId)
        ORDER BY u.UzsakymoTerminas, u.UzsakymoData, pg.ParduotoGaminioID
        OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
        """;

    private static readonly string FacetsSql = $"""
        SELECT N'branch' AS Kind, CONVERT(bigint,f.FilialoID) AS Id,
               f.FilialoPavadinimas AS Name, CONVERT(bigint,NULL) AS ParentId,
               COUNT(*) AS Rows, SUM(pg.Kiekis) AS Quantity
        {QueueSourceSql}
        GROUP BY f.FilialoID,f.FilialoPavadinimas
        UNION ALL
        SELECT N'series',CONVERT(bigint,us.UzsakymoSerijaID),us.UzsakymoSerijaPav,
               CONVERT(bigint,us.FilialoID),COUNT(*),SUM(pg.Kiekis)
        {QueueSourceSql}
        GROUP BY us.UzsakymoSerijaID,us.UzsakymoSerijaPav,us.FilialoID
        UNION ALL
        SELECT N'category',CONVERT(bigint,r.RusiesID),r.RusiesPavadinimas,
               CONVERT(bigint,NULL),COUNT(*),SUM(pg.Kiekis)
        {QueueSourceSql}
        GROUP BY r.RusiesID,r.RusiesPavadinimas
        UNION ALL
        SELECT N'type',CONVERT(bigint,t.TipoID),t.TipoPavadinimas,
               CONVERT(bigint,r.RusiesID),COUNT(*),SUM(pg.Kiekis)
        {QueueSourceSql}
        GROUP BY t.TipoID,t.TipoPavadinimas,r.RusiesID
        UNION ALL
        SELECT N'priority',CONVERT(bigint,p.PriorityID),p.sPriority,
               CONVERT(bigint,NULL),COUNT(*),SUM(pg.Kiekis)
        {QueueSourceSql}
        GROUP BY p.PriorityID,p.sPriority
        ORDER BY Kind,Name;
        """;
}
