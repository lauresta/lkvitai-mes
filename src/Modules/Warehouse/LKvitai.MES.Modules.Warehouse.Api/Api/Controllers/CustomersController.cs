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
                x.PriceGroupId,
                x.PriceGroup != null ? x.PriceGroup.Name : null,
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
                x.PaymentTerms.ToString().ToUpperInvariant(),
                x.CreditLimit,
                x.PriceGroupId,
                x.PriceGroup != null ? x.PriceGroup.Name : null,
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

        if (!TryParsePaymentTerms(request.PaymentTerms, out var paymentTerms))
        {
            return BadRequest($"Invalid payment terms '{request.PaymentTerms}'.");
        }

        if (request.PriceGroupId.HasValue)
        {
            var groupExists = await _dbContext.PriceGroups
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.PriceGroupId.Value, cancellationToken);
            if (!groupExists)
            {
                return BadRequest($"Price group '{request.PriceGroupId.Value}' does not exist.");
            }
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
            PaymentTerms = paymentTerms,
            CreditLimit = request.CreditLimit,
            PriceGroupId = request.PriceGroupId
        };

        _dbContext.Customers.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"customer:{row.Id:N}", cancellationToken);

        return Created($"/api/warehouse/v1/customers/{row.Id}", new { row.Id, row.CustomerCode });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateCustomerRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Name and Email are required.");
        }

        if (!TryParsePaymentTerms(request.PaymentTerms, out var paymentTerms))
        {
            return BadRequest($"Invalid payment terms '{request.PaymentTerms}'.");
        }

        if (!TryParseStatus(request.Status, out var status))
        {
            return BadRequest($"Invalid status '{request.Status}'.");
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (request.PriceGroupId.HasValue)
        {
            var groupExists = await _dbContext.PriceGroups
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.PriceGroupId.Value, cancellationToken);
            if (!groupExists)
            {
                return BadRequest($"Price group '{request.PriceGroupId.Value}' does not exist.");
            }
        }

        customer.Name = request.Name.Trim();
        customer.Email = request.Email.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        customer.BillingAddress = request.BillingAddress is null
            ? new Address()
            : new Address
            {
                Street = request.BillingAddress.Street,
                City = request.BillingAddress.City,
                State = request.BillingAddress.State,
                ZipCode = request.BillingAddress.ZipCode,
                Country = request.BillingAddress.Country
            };
        customer.DefaultShippingAddress = request.ShippingAddress is null
            ? null
            : new Address
            {
                Street = request.ShippingAddress.Street,
                City = request.ShippingAddress.City,
                State = request.ShippingAddress.State,
                ZipCode = request.ShippingAddress.ZipCode,
                Country = request.ShippingAddress.Country
            };
        customer.Status = status;
        customer.PaymentTerms = paymentTerms;
        customer.CreditLimit = request.CreditLimit;
        customer.PriceGroupId = request.PriceGroupId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"customer:{id:N}", cancellationToken);

        return Ok(new { customer.Id, customer.CustomerCode });
    }

    private static bool TryParsePaymentTerms(string? value, out PaymentTerms paymentTerms)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            paymentTerms = PaymentTerms.Net30;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out paymentTerms);
    }

    private static bool TryParseStatus(string? value, out CustomerStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = CustomerStatus.Active;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out status);
    }

    [HttpPut("{id:guid}/price-group")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> SetPriceGroupAsync(
        Guid id,
        [FromBody] SetCustomerPriceGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        if (request.PriceGroupId.HasValue)
        {
            var groupExists = await _dbContext.PriceGroups
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.PriceGroupId.Value, cancellationToken);
            if (!groupExists)
            {
                return BadRequest($"Price group '{request.PriceGroupId.Value}' does not exist.");
            }
        }

        customer.PriceGroupId = request.PriceGroupId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"customer:{id:N}", cancellationToken);

        return Ok(new { customer.Id, customer.PriceGroupId });
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
        int? PriceGroupId,
        string? PriceGroupName,
        AddressResponse? DefaultShippingAddress);

    public sealed record CustomerDetailResponse(
        Guid Id,
        string CustomerCode,
        string Name,
        string Email,
        string? Phone,
        string Status,
        string PaymentTerms,
        decimal? CreditLimit,
        int? PriceGroupId,
        string? PriceGroupName,
        AddressResponse BillingAddress,
        AddressResponse? DefaultShippingAddress);

    public sealed record SetCustomerPriceGroupRequest(int? PriceGroupId);

    public sealed record CreateCustomerRequest(
        string Name,
        string Email,
        string? Phone,
        AddressResponse? BillingAddress,
        AddressResponse? ShippingAddress,
        string? PaymentTerms = null,
        decimal? CreditLimit = null,
        int? PriceGroupId = null);

    public sealed record UpdateCustomerRequest(
        string Name,
        string Email,
        string? Phone,
        AddressResponse? BillingAddress,
        AddressResponse? ShippingAddress,
        string Status,
        string PaymentTerms,
        decimal? CreditLimit,
        int? PriceGroupId);
}
