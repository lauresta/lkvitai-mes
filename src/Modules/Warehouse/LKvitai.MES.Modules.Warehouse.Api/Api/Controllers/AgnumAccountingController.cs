using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Blueprint 4.5 / 4.6 / 4.11: stock account profiles, posting rules, audit journal, supplier sync.</summary>
[ApiController]
[Route("api/warehouse/v1/agnum/accounting")]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
public sealed class AgnumAccountingController : ControllerBase
{
    private readonly WarehouseDbContext _db;
    private readonly AccountingConfigService _config;
    private readonly AgnumSupplierSyncService _suppliers;
    private readonly IAgnumReferenceClient _agnum;

    public AgnumAccountingController(
        WarehouseDbContext db,
        AccountingConfigService config,
        AgnumSupplierSyncService suppliers,
        IAgnumReferenceClient agnum)
    {
        _db = db;
        _config = config;
        _suppliers = suppliers;
        _agnum = agnum;
    }

    public sealed record ProfileRequest(int Id, string Name, string StockAccount, int AgnumSndId,
        string? PriceDifferenceAccount, string? WriteOffAccount, string? MarkdownAccount, bool IsDefault);

    public sealed record RuleRequest(int StockAccountProfileId, VatTreatment VatTreatment, int SchemeId, string? VatCode);
    public sealed record CostTypeRequest(int Id, string Code, string Name, bool IncludedInCost, string? ExpenseAccount, bool IsActive);

    public sealed record CategoryProfileRequest(int? ProfileId);

    private string User => HttpContext.User.Identity?.Name ?? "unknown";

    [HttpGet("profiles")]
    public async Task<IActionResult> GetProfiles(CancellationToken ct)
        => Ok(await _db.StockAccountProfiles.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct));

    [HttpPut("profiles")]
    public Task<IActionResult> SaveProfile([FromBody] ProfileRequest r, CancellationToken ct)
        => Guard(async () => Ok(await _config.SaveProfileAsync(new StockAccountProfile
        {
            Id = r.Id, Name = r.Name, StockAccount = r.StockAccount, AgnumSndId = r.AgnumSndId,
            PriceDifferenceAccount = r.PriceDifferenceAccount, WriteOffAccount = r.WriteOffAccount,
            MarkdownAccount = r.MarkdownAccount, IsDefault = r.IsDefault
        }, User, ct)));

    [HttpDelete("profiles/{id:int}")]
    public Task<IActionResult> DeleteProfile(int id, CancellationToken ct)
        => Guard(async () => { await _config.DeleteProfileAsync(id, User, ct); return NoContent(); });

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
        => Ok(await _db.AgnumPostingRules.AsNoTracking().OrderBy(r => r.StockAccountProfileId).ThenBy(r => r.VatTreatment).ToListAsync(ct));

    [HttpPut("rules")]
    public Task<IActionResult> SaveRule([FromBody] RuleRequest r, CancellationToken ct)
        => Guard(async () => Ok(await _config.SaveRuleAsync(new AgnumPostingRule
        {
            StockAccountProfileId = r.StockAccountProfileId, VatTreatment = r.VatTreatment,
            SchemeId = r.SchemeId, VatCode = r.VatCode
        }, User, ct)));

    [HttpDelete("rules/{id:int}")]
    public Task<IActionResult> DeleteRule(int id, CancellationToken ct)
        => Guard(async () => { await _config.DeleteRuleAsync(id, User, ct); return NoContent(); });

    [HttpPut("categories/{categoryId:int}/profile")]
    public Task<IActionResult> SetCategoryProfile(int categoryId, [FromBody] CategoryProfileRequest r, CancellationToken ct)
        => Guard(async () => { await _config.SetCategoryProfileAsync(categoryId, r.ProfileId, User, ct); return NoContent(); });

    [HttpGet("cost-types")]
    public async Task<IActionResult> GetCostTypes(CancellationToken ct)
        => Ok(await _db.AdditionalCostTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct));

    [HttpPut("cost-types")]
    public Task<IActionResult> SaveCostType([FromBody] CostTypeRequest r, CancellationToken ct)
        => Guard(async () => Ok(await _config.SaveCostTypeAsync(new AdditionalCostType
        {
            Id = r.Id, Code = r.Code, Name = r.Name, IncludedInCost = r.IncludedInCost,
            ExpenseAccount = r.ExpenseAccount, IsActive = r.IsActive
        }, User, ct)));

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] int take = 200, CancellationToken ct = default)
        => Ok(await _db.AccountingConfigAudit.AsNoTracking()
            .OrderByDescending(a => a.Id).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));

    [HttpGet("reference/schemes")]
    public Task<IActionResult> Schemes(CancellationToken ct) => Guard(async () => Ok(await _agnum.GetSchemesAsync(ct)));

    [HttpGet("reference/warehouses")]
    public Task<IActionResult> Warehouses(CancellationToken ct) => Guard(async () => Ok(await _agnum.GetWarehousesAsync(ct)));

    [HttpGet("reference/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) => Guard(async () => Ok(await _agnum.GetAccountsAsync(ct)));

    [HttpGet("reference/vat-codes")]
    public Task<IActionResult> VatCodes(CancellationToken ct) => Guard(async () => Ok(await _agnum.GetVatCodesAsync(ct)));

    [HttpPost("suppliers/sync")]
    public Task<IActionResult> SyncSuppliers(CancellationToken ct)
        => Guard(async () => Ok(await _suppliers.SyncAsync(ct)));

    [HttpGet("suppliers/{id:int}/postable")]
    public async Task<IActionResult> SupplierPostable(int id, CancellationToken ct)
        => Ok(await _suppliers.CheckPostableAsync(id, ct));

    private async Task<IActionResult> Guard(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (AccountingConfigValidationException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Configuration was not saved",
                Detail = string.Join(" ", ex.Problems),
                Status = StatusCodes.Status422UnprocessableEntity,
                Extensions = { ["problems"] = ex.Problems }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not found", Detail = ex.Message, Status = 404 });
        }
        catch (AgnumApiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Agnum service unavailable", Detail = ex.Message, Status = 502 });
        }
    }
}
