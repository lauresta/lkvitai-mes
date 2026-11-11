using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Configuration;

/// <summary>Single configured physical warehouse code (<c>Warehouse:Code</c>, env <c>Warehouse__Code</c>).</summary>
public sealed class WarehouseCodeOptions
{
    public const string SectionName = "Warehouse";
    public const string DefaultCode = "WH1";

    public string? Code { get; set; }
}

/// <summary>The only source of the warehouse code written into stock-ledger streams and stock reads.</summary>
public interface IWarehouseCodeProvider
{
    string Code { get; }
}

public sealed class WarehouseCodeProvider : IWarehouseCodeProvider
{
    public WarehouseCodeProvider(IOptions<WarehouseCodeOptions> options)
        : this(options.Value.Code)
    {
    }

    private WarehouseCodeProvider(string? code)
    {
        Code = string.IsNullOrWhiteSpace(code) ? WarehouseCodeOptions.DefaultCode : code.Trim();
    }

    public string Code { get; }

    /// <summary>Fixed code for tests and non-DI composition.</summary>
    public static IWarehouseCodeProvider For(string? code) => new WarehouseCodeProvider(code);
}
