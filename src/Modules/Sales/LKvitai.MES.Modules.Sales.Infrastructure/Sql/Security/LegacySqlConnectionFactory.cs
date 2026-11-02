using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;

public interface ILegacySqlConnectionFactory
{
    Task<LegacySqlConnectionLease> OpenAsync(CancellationToken cancellationToken);
}

/// <summary>A fresh, unpooled physical session bound to exactly one trusted actor.
/// The business transaction must end before restoring the service security context.</summary>
public sealed class LegacySqlConnectionFactory : ILegacySqlConnectionFactory
{
    private readonly SalesSqlOptions _options;
    private readonly ILegacySqlIdentityResolver _identities;
    private readonly Func<string,int,ILegacySqlSession> _sessions;
    private readonly ILogger<LegacySqlConnectionFactory>? _logger;

    public LegacySqlConnectionFactory(SalesSqlOptions options, ILegacySqlIdentityResolver identities,
        ILogger<LegacySqlConnectionFactory>? logger = null)
        : this(options,identities,(connectionString,timeout)=>new LegacySqlSession(connectionString,timeout),logger) { }

    internal LegacySqlConnectionFactory(SalesSqlOptions options, ILegacySqlIdentityResolver identities,
        Func<string,int,ILegacySqlSession> sessions, ILogger<LegacySqlConnectionFactory>? logger = null)
    { _options=options; _identities=identities; _sessions=sessions; _logger=logger; }

    public async Task<LegacySqlConnectionLease> OpenAsync(CancellationToken cancellationToken)
    {
        // Fail before opening the privileged service connection if no active
        // trusted mapping exists. There is intentionally no sa/default fallback.
        var identity=await _identities.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if(identity is null || !ValidLogin(identity.Login))
            throw Denied("LEGACY_SQL_IDENTITY_REQUIRED","Your account has no valid legacy SQL access mapping.");
        cancellationToken.ThrowIfCancellationRequested();
        var builder=new SqlConnectionStringBuilder(_options.ConnectionString)
        {
            Pooling=false,
            MultipleActiveResultSets=false,
            Enlist=false
        };
        var session=_sessions(builder.ConnectionString,_options.CommandTimeoutSeconds>0?_options.CommandTimeoutSeconds:30);
        byte[]? cookie=null;
        LegacySqlOriginalIdentity? original=null;
        try
        {
            original=await session.OpenAsync(cancellationToken).ConfigureAwait(false);
            if(!SameLogin(original.EffectiveLogin,original.OriginalLogin))
                throw Denied("LEGACY_SQL_CONTEXT_INVALID","The legacy SQL connection has an unexpected security context.");
            var principal=await session.FindPrincipalAsync(identity.Login,cancellationToken).ConfigureAwait(false);
            if(principal is null)
                throw Denied("LEGACY_SQL_LOGIN_UNAVAILABLE","Your mapped legacy SQL login is unavailable or disabled.");
            if(SameLogin(principal,original.OriginalLogin) && !identity.AllowServiceLogin)
                throw Denied("LEGACY_SQL_SERVICE_LOGIN_FORBIDDEN","Your account is not permitted to use the SQL service login.");
            var switched=await session.SwitchAsync(principal,cancellationToken).ConfigureAwait(false);
            cookie=switched.Cookie;
            if(cookie.Length==0 || cookie.Length>8000 || !SameLogin(switched.EffectiveLogin,principal)
                || !SameLogin(switched.OriginalLogin,original.OriginalLogin)
                || !switched.CanConnectSql || !switched.CanConnectDatabase)
                throw Denied("LEGACY_SQL_CONTEXT_INVALID","The legacy SQL identity could not be established.");
            cancellationToken.ThrowIfCancellationRequested();
            return new LegacySqlConnectionLease(session,cookie,principal,original,_logger);
        }
        catch
        {
            // Cancellation may arrive after SQL changed identity but before its
            // cookie was returned. Unpooled physical disposal is then the only
            // safe fallback, and still prevents reuse under another identity.
            await LegacySqlConnectionLease.CleanupAsync(session,cookie,original?.OriginalLogin,_logger).ConfigureAwait(false);
            if(cookie is not null)Array.Clear(cookie);
            throw;
        }
    }

    internal static bool ValidLogin(string? login)=> !string.IsNullOrWhiteSpace(login) && login.Length<=128
        && login==login.Trim() && !login.Any(char.IsControl);
    internal static bool SameLogin(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    private static OrderEntryException Denied(string code,string message)=>new(code,message,403);
}

public sealed class LegacySqlConnectionLease : IAsyncDisposable
{
    private readonly ILegacySqlSession _session;
    private readonly byte[] _cookie;
    private readonly ILogger<LegacySqlConnectionFactory>? _logger;
    private int _disposed;
    internal LegacySqlConnectionLease(ILegacySqlSession session,byte[] cookie,string effectiveLogin,
        LegacySqlOriginalIdentity original,ILogger<LegacySqlConnectionFactory>? logger)
    {
        _session=session; _cookie=cookie; _logger=logger; EffectiveLogin=effectiveLogin;
        OriginalLogin=original.OriginalLogin; DatabaseGuid=original.DatabaseGuid; DatabaseName=original.DatabaseName;
    }
    public SqlConnection Connection { get { ObjectDisposedException.ThrowIf(_disposed!=0,this); return _session.Connection; } }
    public string EffectiveLogin { get; }
    public string OriginalLogin { get; }
    public Guid? DatabaseGuid { get; }
    public string DatabaseName { get; }

    /// <summary>Track local transactions so interrupted requests can always roll
    /// them back before REVERT. Do not begin raw transactions on Connection.</summary>
    public Task<SqlTransaction> BeginTransactionAsync(CancellationToken ct)
        =>BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
    public Task<SqlTransaction> BeginTransactionAsync(IsolationLevel isolationLevel,CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed!=0,this);
        return _session.BeginTransactionAsync(isolationLevel,ct);
    }
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref _disposed,1)!=0)return;
        await CleanupAsync(_session,_cookie,OriginalLogin,_logger).ConfigureAwait(false);
        Array.Clear(_cookie);
    }
    internal static async Task CleanupAsync(ILegacySqlSession session,byte[]? cookie,string? originalLogin,
        ILogger<LegacySqlConnectionFactory>? logger)
    {
        // Deliberately independent of the cancelled HTTP request token.
        using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await session.RollbackOutstandingAsync(cleanup.Token).ConfigureAwait(false);
            if(cookie is { Length:>0 } && originalLogin is not null)
                await session.RestoreAsync(cookie,originalLogin,cleanup.Token).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            // The connection is never returned to a pool. Avoid turning a
            // committed operation into a reported failure solely on close.
            logger?.LogError(ex,"Legacy SQL context cleanup failed; discarding the unpooled physical session.");
        }
        finally
        {
            try { await session.DisposeAsync().ConfigureAwait(false); }
            catch(Exception ex) { logger?.LogError(ex,"Legacy SQL physical session disposal failed."); }
        }
    }
}

internal sealed record LegacySqlOriginalIdentity(string EffectiveLogin,string OriginalLogin,string DatabaseName,Guid? DatabaseGuid);
internal sealed record LegacySqlSwitchedIdentity(byte[] Cookie,string EffectiveLogin,string OriginalLogin,
    bool CanConnectSql=true,bool CanConnectDatabase=true);
internal interface ILegacySqlSession : IAsyncDisposable
{
    SqlConnection Connection { get; }
    Task<LegacySqlOriginalIdentity> OpenAsync(CancellationToken ct);
    Task<string?> FindPrincipalAsync(string login,CancellationToken ct);
    Task<LegacySqlSwitchedIdentity> SwitchAsync(string canonicalLogin,CancellationToken ct);
    Task<SqlTransaction> BeginTransactionAsync(IsolationLevel isolationLevel,CancellationToken ct);
    Task RollbackOutstandingAsync(CancellationToken ct);
    Task RestoreAsync(byte[] cookie,string originalLogin,CancellationToken ct);
}
