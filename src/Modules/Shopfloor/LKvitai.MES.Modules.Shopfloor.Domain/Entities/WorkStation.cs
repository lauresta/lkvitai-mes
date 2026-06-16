namespace LKvitai.MES.Modules.Shopfloor.Domain.Entities;

/// <summary>A work station that belongs to a work center.</summary>
public sealed class WorkStation
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid WorkCenterId { get; private set; }
    public int? WipLimit { get; private set; }
    public bool IsActive { get; private set; }

    private WorkStation() { }

    public WorkStation(Guid id, string code, string name, Guid workCenterId, int? wipLimit, bool isActive)
    {
        Id = id;
        Update(code, name, workCenterId, wipLimit, isActive);
    }

    public void Update(string code, string name, Guid workCenterId, int? wipLimit, bool isActive)
    {
        if (workCenterId == Guid.Empty)
        {
            throw new ArgumentException("workCenterId is required.", nameof(workCenterId));
        }

        if (wipLimit is < 0)
        {
            throw new ArgumentException("wipLimit cannot be negative.", nameof(wipLimit));
        }

        Code = NormalizeRequired(code, nameof(code));
        Name = NormalizeRequired(name, nameof(name));
        WorkCenterId = workCenterId;
        WipLimit = wipLimit;
        IsActive = isActive;
    }

    private static string NormalizeRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        return value.Trim();
    }
}
