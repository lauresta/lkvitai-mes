using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"customer:{id:N}";
        var cached = await Cache.GetAsync<CustomerDetailResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Ok(cached);
        }

        var row = await _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CustomerDetailResponse(
                x.Id,
                x.CustomerCode,
                x.Name,
                x.Email,
                x.Phone,
                x.Status.ToString().ToUpperInvariant(),
                new AddressResponse(
                    x.BillingAddress.Street,
                    x.BillingAddress.City,
                    x.BillingAddress.State,
                    x.BillingAddress.ZipCode,
                    x.BillingAddress.Country),
                x.DefaultShippingAddress == null
                    ? null
                    : new AddressResponse(
                        x.DefaultShippingAddress.Street,
                        x.DefaultShippingAddress.City,
                        x.DefaultShippingAddress.State,
                        x.DefaultShippingAddress.ZipCode,
                        x.DefaultShippingAddress.Country)))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return NotFound();
        }

        await Cache.SetAsync(cacheKey, row, TimeSpan.FromMinutes(30), cancellationToken);
        return Ok(row);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Name and Email are required.");
        }

        var row = new Customer
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            BillingAddress = request.BillingAddress is null
                ? new Address()
                : new Address
                {
                    Street = request.BillingAddress.Street,
                    City = request.BillingAddress.City,
                    State = request.BillingAddress.State,
                    ZipCode = request.BillingAddress.ZipCode,
                    Country = request.BillingAddress.Country
                },
            DefaultShippingAddress = request.ShippingAddress is null
                ? null
                : new Address
                {
                    Street = request.ShippingAddress.Street,
                    City = request.ShippingAddress.City,
                    State = request.ShippingAddress.State,
                    ZipCode = request.ShippingAddress.ZipCode,
                    Country = request.ShippingAddress.Country
                },
            Status = CustomerStatus.Active,
            PaymentTerms = PaymentTerms.Net30
        };

        _dbContext.Customers.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"customer:{row.Id:N}", cancellationToken);

        return Created($"/api/warehouse/v1/customers/{row.Id}", new { row.Id, row.CustomerCode });
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

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

    public sealed record CustomerDetailResponse(
        Guid Id,
        string CustomerCode,
        string Name,
        string Email,
        string? Phone,
        string Status,
        AddressResponse BillingAddress,
        AddressResponse? DefaultShippingAddress);

    public sealed record CreateCustomerRequest(
        string Name,
        string Email,
        string? Phone,
        AddressResponse? BillingAddress,
        AddressResponse? ShippingAddress);
}
