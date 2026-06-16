namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

/// <summary>Persists all changes tracked in the current scope.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
