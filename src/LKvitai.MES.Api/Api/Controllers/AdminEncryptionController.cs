using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/encryption")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminEncryptionController : ControllerBase
{
    private readonly IPiiEncryptionService _service;

    public AdminEncryptionController(IPiiEncryptionService service)
    {
        _service = service;
    }

    [HttpPost("rotate-key")]
    public async Task<IActionResult> RotateKeyAsync(CancellationToken cancellationToken = default)
    {
        var result = await _service.RotateKeyAsync(cancellationToken);
        return Ok(result);
    }
}
