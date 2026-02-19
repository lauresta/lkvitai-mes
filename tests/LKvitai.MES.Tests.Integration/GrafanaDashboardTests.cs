using System.Text.Json;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class GrafanaDashboardTests
{
    [Fact]
    public void DashboardFiles_ShouldExist_ForAllRequiredDashboards()
    {
        var required = new[]
        {
            "grafana/dashboards/business-metrics.json",
            "grafana/dashboards/sla-tracking.json",
            "grafana/dashboards/system-health.json",
            "grafana/dashboards/errors.json",
            "grafana/dashboards/capacity-planning.json"
        };

        foreach (var path in required)
        {
            var absolute = ResolvePathFromRepoRoot(path);
            Assert.True(File.Exists(absolute), $"Missing dashboard file: {path}");
        }
    }

    [Fact]
    public void Dashboards_ShouldUsePrometheusDatasource_AndAutoRefresh()
    {
        var dashboards = Directory.GetFiles(ResolvePathFromRepoRoot("grafana/dashboards"), "*.json");
        Assert.True(dashboards.Length >= 5, "Expected at least 5 dashboard definitions.");

        foreach (var dashboardPath in dashboards)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(dashboardPath));
            var root = document.RootElement;

            Assert.Equal("30s", root.GetProperty("refresh").GetString());
            Assert.True(root.TryGetProperty("panels", out var panels));
            Assert.True(panels.GetArrayLength() > 0, $"Dashboard has no panels: {dashboardPath}");

            foreach (var panel in panels.EnumerateArray())
            {
                if (!panel.TryGetProperty("datasource", out var datasource))
                {
                    continue;
                }

                Assert.Equal("prometheus", datasource.GetProperty("type").GetString());
            }
        }
    }

    [Fact]
    public void Compose_ShouldContainGrafanaServiceAndProvisioningMounts()
    {
        var compose = File.ReadAllText(ResolvePathFromRepoRoot("docker-compose.yml"));

        Assert.Contains("grafana:", compose, StringComparison.Ordinal);
        Assert.Contains("grafana/grafana", compose, StringComparison.Ordinal);
        Assert.Contains("3000:3000", compose, StringComparison.Ordinal);
        Assert.Contains("./grafana/dashboards:/var/lib/grafana/dashboards:ro", compose, StringComparison.Ordinal);
        Assert.Contains("./grafana/provisioning/datasources:/etc/grafana/provisioning/datasources:ro", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void DatasourceProvisioning_ShouldDefineDefaultPrometheus()
    {
        var datasourceConfig = File.ReadAllText(ResolvePathFromRepoRoot("grafana/provisioning/datasources/prometheus.yml"));

        Assert.Contains("name: Prometheus", datasourceConfig, StringComparison.Ordinal);
        Assert.Contains("type: prometheus", datasourceConfig, StringComparison.Ordinal);
        Assert.Contains("url: http://prometheus:9090", datasourceConfig, StringComparison.Ordinal);
        Assert.Contains("isDefault: true", datasourceConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardProvisioning_ShouldPollEveryThirtySeconds()
    {
        var provisioningConfig = File.ReadAllText(ResolvePathFromRepoRoot("grafana/provisioning/dashboards/dashboards.yml"));

        Assert.Contains("updateIntervalSeconds: 30", provisioningConfig, StringComparison.Ordinal);
        Assert.Contains("path: /var/lib/grafana/dashboards", provisioningConfig, StringComparison.Ordinal);
    }

    private static string ResolvePathFromRepoRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (.git) from test runtime directory.");
    }
}
