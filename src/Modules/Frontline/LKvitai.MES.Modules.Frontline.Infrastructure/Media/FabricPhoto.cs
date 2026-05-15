namespace LKvitai.MES.Modules.Frontline.Infrastructure.Media;

public sealed class FabricPhoto
{
    public Guid Id { get; set; }
    public string FabricCode { get; set; } = string.Empty;
    public string OriginalObjectKey { get; set; } = string.Empty;
    public string ThumbObjectKey { get; set; } = string.Empty;
    public string? SourceImageUrl { get; set; }
    public string? SourcePageUrl { get; set; }
    public string? SourceImageFileName { get; set; }
    public string? Sha256 { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public long? FileSizeBytes { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
