using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Services;

public sealed class ShopfloorDatabaseStatusService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ShopfloorDatabaseStatusService> _logger;

    public ShopfloorDatabaseStatusService(
        IConfiguration configuration,
        ILogger<ShopfloorDatabaseStatusService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ShopfloorDatabaseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ShopfloorDatabaseStatus.Unavailable("ConnectionStrings:WarehouseDb is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                select
                    version(),
                    current_database(),
                    current_schema(),
                    inet_server_addr()::text,
                    inet_server_port()
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ShopfloorDatabaseStatus.Unavailable("PostgreSQL returned no diagnostic row.");
            }

            return new ShopfloorDatabaseStatus(
                IsAvailable: true,
                Message: "Connected",
                ServerVersion: reader.GetString(0),
                Database: reader.GetString(1),
                Schema: reader.GetString(2),
                ServerEndpoint: $"{reader.GetString(3)}:{reader.GetInt32(4)}",
                CheckedAt: DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            _logger.LogWarning(ex, "Shopfloor database diagnostic query failed");
            return ShopfloorDatabaseStatus.Unavailable(ex.Message);
        }
    }

    private string? ResolveConnectionString() =>
        _configuration.GetConnectionString("WarehouseDb") ??
        _configuration.GetConnectionString("ShopfloorDb");
}

public sealed record ShopfloorDatabaseStatus(
    bool IsAvailable,
    string Message,
    string? ServerVersion,
    string? Database,
    string? Schema,
    string? ServerEndpoint,
    DateTimeOffset CheckedAt)
{
    public static ShopfloorDatabaseStatus Unavailable(string message) =>
        new(false, message, null, null, null, null, DateTimeOffset.Now);
}
