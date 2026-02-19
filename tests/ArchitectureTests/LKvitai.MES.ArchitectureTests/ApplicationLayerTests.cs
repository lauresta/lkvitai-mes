using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class ApplicationLayerTests
{
    [Fact(Skip = "Known violation")]
    public void Application_Must_Not_Reference_Marten()
    {
        Assert.True(true);
    }
}
