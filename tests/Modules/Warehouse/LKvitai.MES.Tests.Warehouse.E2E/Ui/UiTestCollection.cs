using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[CollectionDefinition("ui-e2e", DisableParallelization = true)]
public sealed class UiTestCollection : ICollectionFixture<PlaywrightFixture>
{
}
