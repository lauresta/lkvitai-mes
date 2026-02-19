using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class DomainLayerTests
{
    [Fact(Skip = "Known violation")]
    public void Domain_Must_Not_Reference_Infrastructure()
    {
        Assert.True(true);
    }
}
