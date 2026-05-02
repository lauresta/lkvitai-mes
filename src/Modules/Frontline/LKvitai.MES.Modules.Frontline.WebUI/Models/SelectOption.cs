namespace LKvitai.MES.Modules.Frontline.WebUI.Models;

/// <summary>
/// Pure UI model used by <see cref="Components.LkSelect"/>. Mirror of
/// Sales.WebUI's SelectOption — kept module-local until shared web-UI
/// assembly extraction (tracked as tech debt with the duplicated tokens.css).
/// </summary>
public sealed record SelectOption(string Value, string Label);
