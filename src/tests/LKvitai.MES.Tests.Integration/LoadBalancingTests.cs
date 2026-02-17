using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class LoadBalancingTests
{
    [Fact]
    public void Compose_ShouldContainThreeApiInstancesAndNginx()
    {
        var compose = ReadFileFromRepo("docker-compose.yml");

        Assert.Contains("api-1:", compose, StringComparison.Ordinal);
        Assert.Contains("api-2:", compose, StringComparison.Ordinal);
        Assert.Contains("api-3:", compose, StringComparison.Ordinal);
        Assert.Contains("nginx:", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_ShouldContainSharedPostgresAndRedis()
    {
        var compose = ReadFileFromRepo("docker-compose.yml");

        Assert.Contains("pg:", compose, StringComparison.Ordinal);
        Assert.Contains("redis:", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void NginxConfig_ShouldContainRoundRobinUpstreamServers()
    {
        var nginx = ReadFileFromRepo("nginx.conf");

        Assert.Contains("upstream warehouse_api", nginx, StringComparison.Ordinal);
        Assert.Contains("server api-1:8080 max_fails=3 fail_timeout=30s;", nginx, StringComparison.Ordinal);
        Assert.Contains("server api-2:8080 max_fails=3 fail_timeout=30s;", nginx, StringComparison.Ordinal);
        Assert.Contains("server api-3:8080 max_fails=3 fail_timeout=30s;", nginx, StringComparison.Ordinal);
        Assert.Contains("keepalive 32;", nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void NginxConfig_ShouldContainStickyHubRouting()
    {
        var nginx = ReadFileFromRepo("nginx.conf");

        Assert.Contains("upstream warehouse_hubs", nginx, StringComparison.Ordinal);
        Assert.Contains("ip_hash;", nginx, StringComparison.Ordinal);
        Assert.Contains("location /hubs/", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://warehouse_hubs;", nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void NginxConfig_ShouldProxyDefaultTrafficToWarehouseApi()
    {
        var nginx = ReadFileFromRepo("nginx.conf");

        Assert.Contains("location /", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://warehouse_api;", nginx, StringComparison.Ordinal);
    }

    private static string ReadFileFromRepo(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (!File.Exists(candidate))
                {
                    throw new FileNotFoundException($"Unable to resolve {relativePath} from repository root {directory.FullName}.");
                }

                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (.git) from test runtime directory.");
    }
}
