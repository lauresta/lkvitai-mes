using MudBlazor;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class AppTheme
{
    public static readonly MudTheme Default = new()
    {
        Palette = new Palette
        {
            Primary = "#0b57a4",
            Secondary = "#2c7be5",
            Success = "#2e7d32",
            Warning = "#ed6c02",
            Error = "#d32f2f",
            Background = "#f6f8fb",
            Surface = "#ffffff",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2f46",
            DrawerBackground = "#1f2937",
            DrawerText = "#e5e7eb"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
