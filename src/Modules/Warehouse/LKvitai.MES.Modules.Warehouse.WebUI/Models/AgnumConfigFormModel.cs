using System.ComponentModel.DataAnnotations;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

/// <summary>Agnum configuration form. A new configuration starts switched off: nothing is sent to Agnum until the owner turns it on.</summary>
public sealed class AgnumConfigFormModel
{
    public Guid? ConfigId { get; set; }

    [Required]
    public string Scope { get; set; } = "BY_CATEGORY";

    [Required]
    public string Schedule { get; set; } = "0 23 * * *";

    [Required]
    public string Format { get; set; } = "CSV";

    [Url]
    public string? ApiEndpoint { get; set; }

    public string? ApiKey { get; set; }

    public bool IsActive { get; set; }
}
