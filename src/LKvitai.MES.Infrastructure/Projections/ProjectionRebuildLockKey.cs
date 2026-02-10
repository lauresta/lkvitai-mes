namespace LKvitai.MES.Infrastructure.Projections;

public static class ProjectionRebuildLockKey
{
    public static string For(string projectionName)
        => $"projection-rebuild:{projectionName.Trim().ToLowerInvariant()}";
}
