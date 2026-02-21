using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/features")]
[Authorize]
public sealed class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagsController(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    [HttpGet("{flagKey}")]
    public IActionResult GetFeatureFlag(string flagKey, [FromQuery] string? userId = null)
    {
        var result = _featureFlagService.Evaluate(flagKey, User, userId);
        return Ok(new
        {
            flagKey = result.FlagKey,
            enabled = result.Enabled,
            numericValue = result.NumericValue
        });
    }
}
