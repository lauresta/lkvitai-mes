using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.ErrorHandling;

public static class ResultProblemDetailsExtensions
{
    public static IActionResult ToApiResult(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, controller.HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public static IActionResult ToApiResult<T>(this ControllerBase controller, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, controller.HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
}
