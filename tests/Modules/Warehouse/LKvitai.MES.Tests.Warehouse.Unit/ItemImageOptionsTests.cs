using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemImageOptionsTests
{
    [Fact]
    public void FromConfiguration_WhenMinSearchScoreIsValidInvariantNumber_ShouldParseValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ItemImages:MinSearchScore"] = "0.75"
            })
            .Build();

        var options = ItemImageOptions.FromConfiguration(configuration);

        Assert.Equal(0.75d, options.MinSearchScore, 6);
    }

    [Fact]
    public void FromConfiguration_WhenMinSearchScoreIsInvalid_ShouldUseDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ItemImages:MinSearchScore"] = "invalid"
            })
            .Build();

        var options = ItemImageOptions.FromConfiguration(configuration);

        Assert.Equal(ItemImageOptions.DefaultMinSearchScore, options.MinSearchScore, 6);
    }
}
