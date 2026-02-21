namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Per-async-flow storage used to signal idempotent command replay back to the HTTP layer.
/// </summary>
public static class IdempotencyExecutionContext
{
    private static readonly AsyncLocal<bool?> ReplayFlag = new();

    public static void MarkReplay() => ReplayFlag.Value = true;

    public static void Clear() => ReplayFlag.Value = null;

    public static bool ConsumeReplayFlag()
    {
        var replayed = ReplayFlag.Value == true;
        ReplayFlag.Value = null;
        return replayed;
    }
}
