namespace FabricPhotoImporter;

public sealed class FabricPhotoImportRow
{
    public string FabricCode { get; set; } = string.Empty;
    public string? FabricGroup { get; set; }
    public string? SourceImageUrl { get; set; }
    public string? SourcePageUrl { get; set; }
    public string OriginalFile { get; set; } = string.Empty;
    public string ThumbFile { get; set; } = string.Empty;
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
}
