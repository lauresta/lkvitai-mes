namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>
/// Customer names for the portal "Search everything" box (issue #375). Implementations run under the
/// caller's legacy login and return only customers of orders that caller may see.
/// </summary>
public interface ISalesCustomerSearch
{
    /// <summary>Up to <paramref name="limit"/> distinct customer names containing <paramref name="search"/>.</summary>
    Task<IReadOnlyList<string>> SearchAsync(string search, int limit, CancellationToken cancellationToken);
}
