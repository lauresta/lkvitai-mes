using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Resources;

public static class WarehouseErrorText
{
    public static string Get(IStringLocalizer<WarehouseResources> localizer, ApiException? error)
    {
        if (error is null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(error.ErrorCode))
        {
            var byCode = localizer[$"Error.Code.{error.ErrorCode}"];
            if (!byCode.ResourceNotFound) return byCode.Value;
        }

        var byStatus = localizer[$"Error.Http.{error.StatusCode}"];
        return byStatus.ResourceNotFound ? error.UserMessage : byStatus.Value;
    }
}
