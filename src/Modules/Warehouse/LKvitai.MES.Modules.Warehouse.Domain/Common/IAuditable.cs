namespace LKvitai.MES.Modules.Warehouse.Domain.Common;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}
