using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumApiClientFactory : IAgnumApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyDictionary<int, (string ClientName, AgnumWarehouseKeyOptions Options)> _clients;
    private readonly ILogger<AgnumApiClientFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AgnumApiClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<AgnumApiClientOptions> clientOptions,
        IConfiguration configuration,
        ILogger<AgnumApiClientFactory> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _loggerFactory = loggerFactory;

        var warehouseSection = configuration.GetSection("Agnum:Warehouses");
        var clients = new Dictionary<int, (string, AgnumWarehouseKeyOptions)>(EqualityComparer<int>.Default);

        foreach (var warehouse in warehouseSection.GetChildren())
        {
            var options = warehouse.Get<AgnumWarehouseKeyOptions>();
            if (options is null)
            {
                continue;
            }

            var name = warehouse.Key;
            if (clients.ContainsKey(options.SndId))
            {
                throw new InvalidOperationException($"Duplicate Agnum warehouse configuration for sndId {options.SndId}.");
            }

            clients[options.SndId] = ($"Agnum-{name}", options);
        }

        _clients = clients;
    }

    public IAgnumApiClient GetForSndId(int sndId)
    {
        if (!_clients.TryGetValue(sndId, out var clientEntry))
        {
            throw new KeyNotFoundException($"No configured Agnum client for sndId {sndId}.");
        }

        var httpClient = _httpClientFactory.CreateClient(clientEntry.ClientName);
        return new AgnumApiClient(httpClient, clientEntry.Options, _loggerFactory.CreateLogger<AgnumApiClient>());
    }
}
