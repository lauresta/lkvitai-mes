namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record LabelPrintRequestDto(string TemplateType, IDictionary<string, string> Data);
public sealed record LabelPreviewRequestDto(string TemplateType, IDictionary<string, string> Data);

public sealed record LabelPrintResponseDto(string Status, string? PdfUrl, string? Message = null);
public sealed record LabelTemplateDto(string TemplateType, string ZplTemplate);

public sealed record LabelQueueItemDto(
    Guid Id,
    string TemplateType,
    string DataJson,
    string Status,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt,
    string ErrorMessage);
