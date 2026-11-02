using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;

internal sealed class LegacySqlSession(string connectionString,int timeoutSeconds) : ILegacySqlSession
{
    public SqlConnection Connection { get; }=new(connectionString);
    private SqlTransaction? _transaction;
    public async Task<LegacySqlOriginalIdentity> OpenAsync(CancellationToken ct)
    {
        await Connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command=Command("""
            SELECT CONVERT(nvarchar(128),SYSTEM_USER), CONVERT(nvarchar(128),ORIGINAL_LOGIN()),
                   DB_NAME(), database_guid
            FROM sys.database_recovery_status WHERE database_id=DB_ID();
            """);
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false))throw new DataException("The legacy database identity is unavailable.");
        return new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetGuid(3));
    }
    public async Task<string?> FindPrincipalAsync(string login,CancellationToken ct)
    {
        await using var command=Command("""
            DECLARE @canonical sysname, @sid varbinary(85);
            SELECT @canonical=name FROM sys.server_principals
            WHERE name=@login AND type IN ('S','U') AND is_disabled=0;
            -- Native Access Windows users can enter through an existing Windows
            -- group without a separate server/database principal row. Resolve
            -- that actual Windows SID and its canonical name; never reinterpret
            -- an ordinary SQL alias as a different Windows identity. An explicit
            -- disabled server login cannot use this fallback.
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
            """);
        command.Parameters.Add("@login",SqlDbType.NVarChar,128).Value=login;
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }
    public async Task<LegacySqlSwitchedIdentity> SwitchAsync(string canonicalLogin,CancellationToken ct)
    {
        // EXECUTE AS grammar requires a login literal. This is exclusively the
        // canonical enabled server principal or SID-resolved Windows account
        // selected by the parameterized query above, escaped as a Unicode SQL
        // string; never caller-provided SQL text. Group-authenticated Windows
        // accounts must retain actual CONNECT permissions after impersonation.
        var literal=canonicalLogin.Replace("'","''",StringComparison.Ordinal);
        await using var command=Command($"""
            DECLARE @cookie varbinary(8000);
            EXECUTE AS LOGIN = N'{literal}' WITH COOKIE INTO @cookie;
            SELECT @cookie,CONVERT(nvarchar(128),SYSTEM_USER),CONVERT(nvarchar(128),ORIGINAL_LOGIN()),
                   HAS_PERMS_BY_NAME(NULL,NULL,'CONNECT SQL'),
                   HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONNECT');
            """);
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false))throw new DataException("The legacy SQL context cookie is unavailable.");
        return new((byte[])reader.GetValue(0),reader.GetString(1),reader.GetString(2),
            !reader.IsDBNull(3)&&reader.GetInt32(3)==1,!reader.IsDBNull(4)&&reader.GetInt32(4)==1);
    }
    public async Task<SqlTransaction> BeginTransactionAsync(IsolationLevel isolationLevel,CancellationToken ct)
    {
        if(_transaction?.Connection is not null)throw new InvalidOperationException("The legacy SQL session already has an active transaction.");
        _transaction=(SqlTransaction)await Connection.BeginTransactionAsync(isolationLevel,ct).ConfigureAwait(false);
        return _transaction;
    }
    public async Task RollbackOutstandingAsync(CancellationToken ct)
    {
        if(_transaction is null)return;
        try
        {
            if(_transaction.Connection is not null)await _transaction.RollbackAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction=null;
        }
    }
    public async Task RestoreAsync(byte[] cookie,string originalLogin,CancellationToken ct)
    {
        if(Connection.State!=ConnectionState.Open)return; // broken/closed is discarded, never pooled
        await using var command=Command("""
            IF @@TRANCOUNT <> 0 THROW 51000,'A transaction remains active before security-context restoration.',1;
            REVERT WITH COOKIE = @cookie;
            SELECT CONVERT(nvarchar(128),SYSTEM_USER), CONVERT(nvarchar(128),ORIGINAL_LOGIN());
            """);
        command.CommandTimeout=5;
        command.Parameters.Add("@cookie",SqlDbType.VarBinary,8000).Value=cookie;
        await using var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false)
            || !LegacySqlConnectionFactory.SameLogin(reader.GetString(0),originalLogin)
            || !LegacySqlConnectionFactory.SameLogin(reader.GetString(1),originalLogin))
            throw new DataException("The original legacy SQL context was not restored.");
    }
    private SqlCommand Command(string text)=>new(text,Connection){CommandTimeout=timeoutSeconds};
    public ValueTask DisposeAsync()=>Connection.DisposeAsync();
}
