namespace LKvitai.MES.Modules.Shopfloor.WebUI.Services;

/// <summary>Personal marks are evaluated for the current user; Signed means any active sign-off.</summary>
public static class RoletaiPaperlessFilter
{
    public static bool Matches(string mode, IReadOnlySet<int> selectedSteps, bool requireSigned,
        bool hideMine, IReadOnlySet<int> myMarkedSteps, bool anySigned, bool mineSigned)
    {
        if (hideMine && mineSigned) return false;
        if (mode == "All" || (selectedSteps.Count == 0 && !requireSigned)) return true;
        var all = selectedSteps.All(myMarkedSteps.Contains) && (!requireSigned || anySigned);
        return mode == "Marked" ? all : !all;
    }
}
