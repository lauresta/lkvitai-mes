using System.Data;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class LegacySqlConnectionFactoryTests
{
    [Fact]
    public async Task Missing_mapping_is_403_before_opening_service_connection()
    {
        var opened=false;
        var factory=Factory(null,(_,_)=>{opened=true;throw new InvalidOperationException();});
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>factory.OpenAsync(CancellationToken.None));
        Assert.Equal(403,error.Status);Assert.Equal("LEGACY_SQL_IDENTITY_REQUIRED",error.Code);Assert.False(opened);
    }

    [Theory]
    [InlineData("")][InlineData("  ")][InlineData(" user")][InlineData("user ")][InlineData("user\nother")]
    public async Task Malformed_mapping_never_opens_privileged_connection(string login)
    {
        var factory=Factory(new(login),(_,_)=>throw new InvalidOperationException("Must not open"));
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>factory.OpenAsync(CancellationToken.None));
        Assert.Equal(403,error.Status);
    }

    [Fact]
    public async Task Disabled_or_nonexistent_SQL_principal_has_no_service_fallback()
    {
        var session=new Session { Principal=null };
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>Factory(new("operator"),session).OpenAsync(CancellationToken.None));
        Assert.Equal("LEGACY_SQL_LOGIN_UNAVAILABLE",error.Code);
        Assert.DoesNotContain("switch",session.Events);Assert.Equal("dispose",session.Events.Last());
    }

    [Fact]
    public async Task Service_login_requires_explicit_administrator_mapping()
    {
        var session=new Session { Principal="service" };
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>Factory(new("service"),session).OpenAsync(CancellationToken.None));
        Assert.Equal("LEGACY_SQL_SERVICE_LOGIN_FORBIDDEN",error.Code);Assert.DoesNotContain("switch",session.Events);
        var admin=new Session { Principal="service",Effective="service" };
        await using var lease=await Factory(new("service",AllowServiceLogin:true),admin).OpenAsync(CancellationToken.None);
        Assert.Equal("service",lease.EffectiveLogin);
    }

    [Fact]
    public async Task Session_is_unpooled_and_isolation_from_ambient_transactions_is_explicit()
    {
        var session=new Session();SqlConnectionStringBuilder? actual=null;
        var factory=Factory(new("operator"),(connectionString,_)=>{actual=new(connectionString);return session;});
        await using var lease=await factory.OpenAsync(CancellationToken.None);
        Assert.NotNull(actual);Assert.False(actual.Pooling);Assert.False(actual.MultipleActiveResultSets);Assert.False(actual.Enlist);
        Assert.Equal(session.Original.DatabaseGuid,lease.DatabaseGuid);Assert.Equal("LKVITAIDBV1SQL",lease.DatabaseName);
        Assert.Equal("operator",lease.EffectiveLogin);Assert.Equal("service",lease.OriginalLogin);
    }

    [Fact]
    public async Task Mismatched_effective_identity_is_reverted_and_discarded_without_exposing_lease()
    {
        var session=new Session { Effective="someone-else" };
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>Factory(new("operator"),session).OpenAsync(CancellationToken.None));
        Assert.Equal("LEGACY_SQL_CONTEXT_INVALID",error.Code);
        Assert.Equal(new[]{"open","principal","switch","rollback","revert","dispose"},session.Events);
    }

    [Theory]
    [InlineData(false,true)][InlineData(true,false)]
    public async Task Impersonation_without_native_connect_permission_does_not_expose_connection(bool server,bool database)
    {
        var session=new Session{CanConnectSql=server,CanConnectDatabase=database};
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>Factory(new("operator"),session).OpenAsync(CancellationToken.None));
        Assert.Equal("LEGACY_SQL_CONTEXT_INVALID",error.Code);
        Assert.Equal(new[]{"open","principal","switch","rollback","revert","dispose"},session.Events);
    }

    [Fact]
    public async Task Cancellation_after_switch_uses_uncancelled_cleanup_and_rolls_back_before_revert()
    {
        using var request=new CancellationTokenSource();
        var session=new Session { AfterSwitch=request.Cancel };
        await Assert.ThrowsAsync<OperationCanceledException>(()=>Factory(new("operator"),session).OpenAsync(request.Token));
        Assert.Equal(new[]{"open","principal","switch","rollback","revert","dispose"},session.Events);
        Assert.All(session.CleanupTokens,token=>Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task Switch_failure_before_cookie_still_physically_discards_session()
    {
        var session=new Session { FailSwitch=true };
        await Assert.ThrowsAsync<IOException>(()=>Factory(new("operator"),session).OpenAsync(CancellationToken.None));
        Assert.Equal(new[]{"open","principal","switch","rollback","dispose"},session.Events);
    }

    [Fact]
    public async Task Lease_disposal_is_idempotent_and_cannot_be_reused_after_cleanup()
    {
        var session=new Session();var lease=await Factory(new("operator"),session).OpenAsync(CancellationToken.None);
        await lease.DisposeAsync();await lease.DisposeAsync();
        Assert.Equal(new[]{"open","principal","switch","rollback","revert","dispose"},session.Events);
        Assert.Throws<ObjectDisposedException>(()=>lease.Connection);
        Assert.Throws<ObjectDisposedException>(()=>{_ = lease.BeginTransactionAsync(CancellationToken.None);});
    }

    [Theory]
    [InlineData(true,false)][InlineData(false,true)]
    public async Task Cleanup_failure_always_discards_physical_session(bool rollbackFails,bool revertFails)
    {
        var session=new Session { FailRollback=rollbackFails,FailRevert=revertFails };
        var lease=await Factory(new("operator"),session).OpenAsync(CancellationToken.None);
        await lease.DisposeAsync();Assert.Equal("dispose",session.Events.Last());
        if(rollbackFails)Assert.DoesNotContain("revert",session.Events);
    }

    private static LegacySqlConnectionFactory Factory(LegacySqlIdentity? identity,Session session)
        =>Factory(identity,(_,_)=>session);
    private static LegacySqlConnectionFactory Factory(LegacySqlIdentity? identity,Func<string,int,ILegacySqlSession> sessions)
        =>new(new SalesSqlOptions{ConnectionString="Server=unused;Database=LKVITAIDBV1SQL;Integrated Security=true;Pooling=true;MultipleActiveResultSets=true;Enlist=true"},new Resolver(identity),sessions);
    private sealed class Resolver(LegacySqlIdentity? identity):ILegacySqlIdentityResolver
    { public Task<LegacySqlIdentity?> ResolveAsync(CancellationToken cancellationToken)=>Task.FromResult(identity); }
    private sealed class Session:ILegacySqlSession
    {
        public SqlConnection Connection { get; }=new();
        public LegacySqlOriginalIdentity Original { get; }=new("service","service","LKVITAIDBV1SQL",Guid.NewGuid());
        public List<string> Events { get; }=[];
        public List<CancellationToken> CleanupTokens { get; }=[];
        public string? Principal { get; init; }="operator";
        public string Effective { get; init; }="operator";
        public Action? AfterSwitch { get; init; }
        public bool FailSwitch { get; init; }
        public bool FailRollback { get; init; }
        public bool FailRevert { get; init; }
        public bool CanConnectSql { get; init; }=true;
        public bool CanConnectDatabase { get; init; }=true;
        public Task<LegacySqlOriginalIdentity> OpenAsync(CancellationToken ct) { Events.Add("open");return Task.FromResult(Original); }
        public Task<string?> FindPrincipalAsync(string login,CancellationToken ct) { Events.Add("principal");return Task.FromResult(Principal); }
        public Task<LegacySqlSwitchedIdentity> SwitchAsync(string canonicalLogin,CancellationToken ct)
        {
            Events.Add("switch");if(FailSwitch)throw new IOException("Transport interrupted after switch");
            AfterSwitch?.Invoke();return Task.FromResult(new LegacySqlSwitchedIdentity([1,2,3],Effective,"service",CanConnectSql,CanConnectDatabase));
        }
        public Task<SqlTransaction> BeginTransactionAsync(IsolationLevel isolationLevel,CancellationToken ct)=>throw new NotSupportedException();
        public Task RollbackOutstandingAsync(CancellationToken ct)
        { Events.Add("rollback");CleanupTokens.Add(ct);if(FailRollback)throw new IOException("Rollback failed");return Task.CompletedTask; }
        public Task RestoreAsync(byte[] cookie,string originalLogin,CancellationToken ct)
        { Events.Add("revert");CleanupTokens.Add(ct);if(FailRevert)throw new IOException("Revert failed");return Task.CompletedTask; }
        public async ValueTask DisposeAsync() { Events.Add("dispose");await Connection.DisposeAsync(); }
    }
}
