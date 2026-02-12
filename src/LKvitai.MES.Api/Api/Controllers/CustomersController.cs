using LKvitai.MES.Api.Security;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public CustomersController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Customers
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.Status == CustomerStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                x.CustomerCode.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new CustomerLookupResponse(
                x.Id,
                x.CustomerCode,
                x.Name,
                x.Status.ToString().ToUpperInvariant(),
                x.DefaultShippingAddress == null
                    ? null
                    : new AddressResponse(
                        x.DefaultShippingAddress.Street,
                        x.DefaultShippingAddress.City,
                        x.DefaultShippingAddress.State,
                        x.DefaultShippingAddress.ZipCode,
                        x.DefaultShippingAddress.Country)))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    public sealed record AddressResponse(
        string Street,
        string City,
        string State,
        string ZipCode,
        string Country);

    public sealed record CustomerLookupResponse(
        Guid Id,
        string CustomerCode,
        string Name,
        string Status,
        AddressResponse? DefaultShippingAddress);
}
