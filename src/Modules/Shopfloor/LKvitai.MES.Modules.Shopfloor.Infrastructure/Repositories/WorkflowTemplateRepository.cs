using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;

public sealed class WorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly ShopfloorDbContext _db;

    public WorkflowTemplateRepository(ShopfloorDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkflowTemplateWithStats>> ListAsync(CancellationToken cancellationToken)
    {
        var counts = await _db.ProductTypeWorkflowMaps.AsNoTracking()
            .GroupBy(m => m.WorkflowTemplateId)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var templates = await _db.WorkflowTemplates.AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return templates
            .Select(t => new WorkflowTemplateWithStats(t, counts.GetValueOrDefault(t.Id)))
            .ToList();
    }

    public Task<WorkflowTemplate?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.WorkflowTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        var query = _db.WorkflowTemplates.Where(x => x.Code == normalized);
        if (excludeId is { } id)
        {
            query = query.Where(x => x.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public void Add(WorkflowTemplate template) => _db.WorkflowTemplates.Add(template);

    public void Remove(WorkflowTemplate template) => _db.WorkflowTemplates.Remove(template);
}
