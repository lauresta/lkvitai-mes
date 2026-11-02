# UI localization (EN / LT) — how it works and how to translate a module

Audience: engineers and AI agents asked to "translate module X" (Warehouse, Sales,
Frontline, Shopfloor, Scanning, …). Portal dashboard is the reference
implementation — copy it.

## 1. What already exists (do NOT re-implement)

| Piece | Where | What it does |
|---|---|---|
| `CikadaLocalization` | `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/Localization/CikadaLocalization.cs` | Supported languages (`en` default, `lt`), request-localization options, `culture/set` endpoint |
| Culture cookie | standard `.AspNetCore.Culture`, `Path=/`, domain = `PortalAuth:CookieDomain`, 1 year | One choice applies to **every** module (same host or sub-domains) |
| `LanguageSwitcher.razor` | `BuildingBlocks.WebUI/Components` | EN / LT buttons in the shared topbar (after ENV/VER, before the user block) |
| Shell strings | `BuildingBlocks.WebUI/Localization/ShellResources(.lt).resx` | Topbar, test strip, search popover, logout |
| Host wiring | every `*.WebUI/Program.cs` | `AddCikadaLocalization`, `UseCikadaRequestLocalization`, `MapCikadaCultureEndpoint` — already done for Portal, Warehouse, Sales, Frontline, Shopfloor, Scanning |

Behaviour:
- Only the **UI culture** follows the cookie (`CultureInfo.CurrentUICulture` → which `.resx` is used).
- The **formatting culture** (numbers, dates, decimal separator) deliberately stays the host default,
  so switching to LT never changes how `12.50` is parsed or rendered. Do not change this per module.
- Switching posts a plain GET form to `culture/set?culture=lt&returnUrl=...`, the cookie is written
  and the page reloads (new Blazor circuit in the new culture). No JS needed.
- No cookie → English.

If you create a **new** WebUI host, add the three wiring lines (see the XML doc on `CikadaLocalization`):
`builder.Services.AddCikadaLocalization(builder.Configuration);`,
`app.UseCikadaRequestLocalization();` right after `UsePathBase(...)` and before `UseRouting()`/`MapBlazorHub()`,
`app.MapCikadaCultureEndpoint();` before `MapBlazorHub()`. If the host has its own anonymous-path
gate middleware, allow `/culture`.

## 2. Recipe: translate a module (copy Portal)

Reference: `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/Resources/` + `_Imports.razor` + `Pages/Home.razor`.

1. **Marker class** `Resources/<Module>Resources.cs` in the WebUI project:
   ```csharp
   namespace LKvitai.MES.Modules.<Module>.WebUI.Resources;
   public sealed class <Module>Resources { }
   ```
   The project's `RootNamespace` must equal its assembly name (default) — `AddLocalization()` is
   registered **without** `ResourcesPath`, so the resource name must equal the type's full name.
2. **Resource files** next to it: `<Module>Resources.resx` (English = default/neutral) and
   `<Module>Resources.lt.resx` (Lithuanian). Same keys in both. No csproj changes needed
   (Razor SDK embeds `*.resx`; the `lt` satellite assembly is published automatically).
3. **`_Imports.razor`** of the module:
   ```razor
   @using Microsoft.Extensions.Localization
   @using LKvitai.MES.Modules.<Module>.WebUI.Resources
   @inject IStringLocalizer<<Module>Resources> L
   ```
4. **Replace literals**: markup `@L["Orders.Title"]`; attributes `placeholder="@L["Common.Search"]"`
   (also `title`, `aria-label`, `alt`, MudBlazor `Label=`/`Placeholder=`/`HelperText=`);
   C# code `L["Key"].Value`; with arguments `L["Orders.Count", count]` with `{0}` in the resx
   value. **Never** concatenate translated fragments — one key per sentence.
5. **Keys**: `Area.Element` PascalCase, dot-separated, grouped by page/component
   (`Receiving.Grid.Qty`, `Receiving.Toast.Saved`). Shared words go to `Common.*`
   (`Common.Save`, `Common.Cancel`, `Common.Loading`). Keep keys stable — they are the contract.
6. **What NOT to translate**: data from API/DB (customer names, SKUs, order numbers, notes),
   enum values used as data, CSS classes, routes, log messages, exception messages that only
   reach logs, the product name `Cikada.MES`, short code badges like `ENV`/`VER`/`SKU`.
   Status values coming from the API as codes may be mapped to keys (`Status.Active`) with a
   fallback to the raw value: `var s = L[$"Status.{code}"]; var text = s.ResourceNotFound ? code : s.Value;`
7. **Shared-shell parameters**: modules may pass their own `MobileNavigationLabel` etc. to
   `PortalAuthenticatedShell`; pass a localized value (`L["Nav.Open"]`) or omit it to get the
   shared localized default.
8. **Static HTML pages** (e.g. Portal `login.html`) are outside Blazor and are not covered by
   this mechanism — leave them unless a task explicitly asks.

## 3. Lithuanian glossary (use consistently)

| EN | LT |
|---|---|
| Dashboard | Skydelis |
| Warehouse | Sandėlis |
| Sales | Pardavimai |
| Order / Orders | Užsakymas / Užsakymai |
| Customer | Klientas |
| Supplier | Tiekėjas |
| Item / Items | Prekė / Prekės |
| Stock | Atsargos |
| Location | Vieta |
| Receiving | Priėmimas |
| Picking | Komplektavimas |
| Shipment / Dispatch | Siunta / Išsiuntimas |
| Price / Cost | Kaina / Savikaina |
| Quantity | Kiekis |
| Status | Būsena |
| Settings | Nustatymai |
| Search | Paieška / Ieškoti |
| Save / Cancel / Delete / Edit | Išsaugoti / Atšaukti / Ištrinti / Redaguoti |
| Create / Add | Sukurti / Pridėti |
| Loading… | Įkeliama… |
| No data | Duomenų nėra |
| Logout | Atsijungti |

Write proper diacritics (ą č ę ė į š ų ū ž), keep labels short, use the infinitive for
buttons (`Išsaugoti`), and Lithuanian quotes/number words only inside resx values.

## 4. Done checklist for a module PR

- [ ] `grep` the module's `.razor` files: no user-visible English literals left between tags or in
      `placeholder/title/aria-label/Label` attributes (except items in "What NOT to translate").
- [ ] Every key exists in both `.resx` files.
- [ ] `dotnet build src/LKvitai.MES.sln -c Release` passes (TreatWarningsAsErrors).
- [ ] Manually: switch EN ↔ LT in the topbar on 2–3 pages of the module; the choice survives
      navigation to Portal and back.

## 5. Module coverage

- Portal dashboard: reference implementation above.
- Sales: [coverage, contracts and verification](sales.md).
