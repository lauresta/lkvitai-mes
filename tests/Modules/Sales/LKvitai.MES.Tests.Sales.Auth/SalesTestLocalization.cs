using System.Reflection;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace LKvitai.MES.Tests.Sales.Auth;
internal static class SalesTestLocalization
{
    internal static IStringLocalizer<SalesResources> Text { get; } = new StringLocalizer<SalesResources>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()),NullLoggerFactory.Instance));
    internal static void Wire(NewOrderWorkspace component) => typeof(NewOrderWorkspace)
        .GetProperty("L",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(component,Text);
}
