namespace LKvitai.MES.Application.Services;

public interface ISkuGenerationService
{
    Task<string> GenerateNextSkuAsync(int categoryId, CancellationToken cancellationToken = default);
}
