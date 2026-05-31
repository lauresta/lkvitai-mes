using MudBlazor;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class AppTheme
{
    public static readonly MudTheme Default = new()
    {
        Palette = new PaletteLight
        {
            Primary = "#2f8f8b",
            PrimaryDarken = "#1d5d5a",
            PrimaryLighten = "#56aaa7",
            Secondary = "#56aaa7",
            SecondaryDarken = "#1d5d5a",
            Tertiary = "#56aaa7",
            Success = "#19744a",
            Warning = "#7c5f2a",
            Error = "#8a1f12",
            Info = "#1d5d5a",
            Background = "#f5f6f8",
            BackgroundGrey = "#eef1f4",
            Surface = "#ffffff",
            AppbarBackground = "#20242c",
            AppbarText = "#ffffff",
            DrawerBackground = "#20242c",
            DrawerText = "#eef1f4",
            DrawerIcon = "#a5adb8",
            TextPrimary = "#151922",
            TextSecondary = "#66717f",
            TextDisabled = "#8996a4",
            ActionDefault = "#66717f",
            ActionDisabled = "#b9c3ce",
            Divider = "#e2e7ec",
            DividerLight = "#eef1f4",
            LinesDefault = "#d5dce4",
            LinesInputs = "#d5dce4",
            TableLines = "#eef1f4",
            TableStriped = "#fbfcfd",
            TableHover = "#fbfcfd",
            HoverOpacity = 0.04,
            OverlayLight = "rgba(47,143,139,0.12)"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "56px"
        },
        Typography = new Typography
        {
            Default = new Default
            {
                FontFamily = ["Inter", "Segoe UI", "system-ui", "sans-serif"],
                FontSize = "12.5px",
                LineHeight = 1.45
            },
            H6 = new H6 { FontSize = "15px", FontWeight = 600 },
            Subtitle2 = new Subtitle2 { FontSize = "13px", FontWeight = 600 },
            Body1 = new Body1 { FontSize = "12.5px" },
            Body2 = new Body2 { FontSize = "12px" },
            Caption = new Caption { FontSize = "11px" },
            Button = new Button
            {
                FontSize = "12px",
                FontWeight = 700,
                TextTransform = "none"
            }
        }
    };
}
