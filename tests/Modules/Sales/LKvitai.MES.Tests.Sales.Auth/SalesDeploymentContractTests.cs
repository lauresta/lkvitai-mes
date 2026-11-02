using System.Text.RegularExpressions;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesDeploymentContractTests
{
    [Fact]
    public void Automatic_test_deployment_restores_only_the_owner_approved_M2_write_scope()
    {
        var compose = File.ReadAllText(Path.Combine(RepositoryRoot(), "docker-compose.test.yml"));
        var api = Service(compose, "sales-api", "sales-webui");
        Assert.Equal("true", Value(api, "Sales__OrderEntry__Enabled"));
        Assert.Equal(Guid.Parse("31FC1889-3D3E-4422-A4E3-362899649FF5"),
            Guid.Parse(Value(api, "Sales__OrderEntry__ExpectedDatabaseGuid")));
        Assert.Equal(new[] { 2136330188, 2136330191, 551738835, 768439496 },
            Values(api, "Sales__OrderEntry__AllowedSeriesIds"));
        Assert.Equal("false", Value(api, "Sales__OrderEntry__CancellationEnabled"));
        Assert.Equal("true", Value(api, "Sales__OrderEntry__PaymentsEnabled"));
        Assert.Equal("2", Value(api, "Sales__OrderEntry__PaymentCurrencyId"));
        Assert.Equal(new[] { 77249366 }, Values(api, "Sales__OrderEntry__AllowedPaymentEmployeeIds"));
        Assert.Equal(new[] { 1, 2, 81423987, 834019474 }, Values(api, "Sales__OrderEntry__AllowedPaymentMethodIds"));
        Assert.Equal("true", Value(api, "Sales__OrderEntry__ProductionConfirmationEnabled"));
        Assert.Equal("MEST", Value(api, "Sales__OrderEntry__ProductionDocumentPrefix"));
        Assert.Equal("${IMAGE_TAG:-working-tree}", Value(api, "Sales__OrderEntry__BuildVersion"));
        Assert.Equal("${LKVITAI_MSACCESS_DB_CONNECTION}", Value(api, "ConnectionStrings__LKvitaiDb"));
    }

    [Fact]
    public void Durable_identity_bridge_keeps_the_shared_keys_database_and_SSO_metadata()
    {
        var compose = File.ReadAllText(Path.Combine(RepositoryRoot(), "docker-compose.test.yml"));
        var warehouse = Service(compose, "api", "webui");
        var warehouseWeb = Service(compose, "webui", "portal-api");
        var portal = Service(compose, "portal-api", "portal-webui");
        var api = Service(compose, "sales-api", "sales-webui");
        var web = Service(compose, "sales-webui", "frontline-api");
        Assert.Equal(Value(warehouse, "ConnectionStrings__WarehouseDb"), Value(portal, "ConnectionStrings__PortalDb"));
        Assert.Contains("Password=${TEST_DB_PASSWORD}", Value(warehouse, "ConnectionStrings__WarehouseDb"));
        Assert.Equal("/app/auth-keys", Value(warehouse, "PortalAuth__DataProtectionKeysPath"));
        Assert.Contains("${AUTH_KEYS_DIR:-/opt/lkvitai-mes/auth-keys}:/app/auth-keys:ro", warehouse);
        Assert.Contains("<<: *oidc-api", warehouseWeb);
        Assert.Equal("http://lkvitai-mes-warehouse-api:8080", Value(warehouseWeb, "WarehouseApi__BaseUrl"));
        foreach (var module in new[] { api, web })
        {
            Assert.Contains("<<: *oidc-api", module);
            Assert.Equal("http://lkvitai-mes-warehouse-api:8080", Value(module, "CikadaUsers__BaseUrl"));
            Assert.Equal("/app/auth-keys", Value(module, "PortalAuth__DataProtectionKeysPath"));
            Assert.Contains("${AUTH_KEYS_DIR:-/opt/lkvitai-mes/auth-keys}:/app/auth-keys", module);
        }
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github/workflows/deploy-test.yml"));
        Assert.Contains("LKVITAI_MSACCESS_DB_CONNECTION: ${{ secrets.LKVITAI_MSACCESS_DB_CONNECTION }}", workflow);
        Assert.Contains("CIKADA_OIDC_CLIENT_SECRET: ${{ secrets.CIKADA_OIDC_CLIENT_SECRET }}", workflow);
        Assert.Contains("CIKADA_OIDC_CLIENT_ID: ${{ vars.CIKADA_OIDC_CLIENT_ID }}", workflow);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.test.yml"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Service(string compose, string name, string next)
    {
        var start = compose.IndexOf($"\n  {name}:\n", StringComparison.Ordinal);
        var end = compose.IndexOf($"\n  {next}:\n", start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Missing service {name} or {next}.");
        return compose[start..end];
    }

    private static string Value(string service, string key)
    {
        var match = Regex.Match(service, @"^\s+" + Regex.Escape(key) + @": ""([^""]*)""", RegexOptions.Multiline);
        Assert.True(match.Success, $"Missing deployment setting {key}.");
        return match.Groups[1].Value;
    }

    private static int[] Values(string service, string prefix) => Regex.Matches(service,
        @"^\s+" + Regex.Escape(prefix) + @"__([0-9]+): ""([0-9]+)""", RegexOptions.Multiline)
        .OrderBy(match => int.Parse(match.Groups[1].Value))
        .Select(match => int.Parse(match.Groups[2].Value)).ToArray();
}
