using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace LKvitai.MES.Modules.Sales.WebUI.Resources;

// Uses the same standard resource lookup for static input-format helpers and
// standalone transport tests. IStringLocalizer reads CurrentUICulture per call;
// no user language or mutable circuit state is cached here.
internal static class SalesResourceText
{
    internal static IStringLocalizer<SalesResources> Localizer { get; } =
        new StringLocalizer<SalesResources>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));
}
