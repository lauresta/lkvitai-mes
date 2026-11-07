using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public sealed class RoletaiPaperlessFilterTests
{
    [Fact]
    public void MarkedRequiresEverySelectedPersonalStepAndAnySharedSignOff()
    {
        HashSet<int> selected = [1,2];
        HashSet<int> mine = [1];
        Assert.False(RoletaiPaperlessFilter.Matches("Marked",selected,true,false,mine,true,false));
        mine.Add(2);
        Assert.False(RoletaiPaperlessFilter.Matches("Marked",selected,true,false,mine,false,false));
        Assert.True(RoletaiPaperlessFilter.Matches("Marked",selected,true,false,mine,true,false));
    }

    [Fact]
    public void UnmarkedMeansMissingAnySelectedKeyAndHideMineIsIndependent()
    {
        HashSet<int> selected = [1,2];
        HashSet<int> mine = [1];
        Assert.True(RoletaiPaperlessFilter.Matches("Unmarked",selected,true,false,mine,true,false));
        mine.Add(2);
        Assert.False(RoletaiPaperlessFilter.Matches("Unmarked",selected,true,false,mine,true,false));
        Assert.True(RoletaiPaperlessFilter.Matches("Unmarked",selected,true,false,mine,false,false));
        Assert.False(RoletaiPaperlessFilter.Matches("All",selected,true,true,mine,true,true));
        Assert.True(RoletaiPaperlessFilter.Matches("All",selected,true,true,mine,true,false));
    }
}
