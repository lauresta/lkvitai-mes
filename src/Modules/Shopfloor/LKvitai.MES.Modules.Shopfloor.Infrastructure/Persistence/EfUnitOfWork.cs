using LKvitai.MES.Modules.Shopfloor.Application.Ports;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ShopfloorDbContext _dbContext;

    public EfUnitOfWork(ShopfloorDbContext dbContext) => _dbContext = dbContext;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
