namespace LKvitai.MES.Application.Ports;

public interface IProjectionVerificationDataAccess
{
    Task<string?> ResolveQualifiedTableNameAsync(string tableName, CancellationToken cancellationToken);

    Task<int> CountRowsAsync(string tableName, CancellationToken cancellationToken);

    Task<string> ComputeChecksumAsync(
        string tableName,
        string fieldExpression,
        string sortExpression,
        CancellationToken cancellationToken);
}
