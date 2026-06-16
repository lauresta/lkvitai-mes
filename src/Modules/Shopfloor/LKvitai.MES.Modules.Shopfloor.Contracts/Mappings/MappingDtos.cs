namespace LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;

/// <summary>A single legacy-product-type → workflow-template mapping.</summary>
public sealed record ProductTypeMappingDto(
    string LegacyProductTypeCode,
    Guid WorkflowTemplateId,
    string WorkflowCode,
    string WorkflowName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Coverage roll-up for the Legacy Types &amp; Coverage page header.
/// Counts exclude removed legacy product types.
/// </summary>
public sealed record CoverageSummaryDto(
    int TotalLegacyTypes,
    int MappedLegacyTypes,
    int UnmappedLegacyTypes,
    int FamilyCount);

/// <summary>Assign one workflow template to many legacy product types.</summary>
public sealed record BulkAssignMappingRequest(
    IReadOnlyList<string> LegacyProductTypeCodes,
    Guid WorkflowTemplateId);
