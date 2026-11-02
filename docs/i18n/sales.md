# Sales EN / LT

Sales uses the shared [localization contract](README.md), language switcher and
`.AspNetCore.Culture` cookie. English is the neutral resource; Lithuanian is the
`lt` satellite. `CurrentUICulture` alone changes: money/date formats, parsing,
order numbers and business values retain their existing behavior.

The 299 paired resource keys cover the current main/test Sales order list,
filters, details, order-entry preview, products, dimensions/live preview, PPP,
accessories, payment preview, validation, tooltips, accessibility labels and
host-page title/error text. This changes the currently deployed module; it does
not integrate the separate M2 branch or enable database-writing capabilities.

Names, notes, material/product catalog entries and stored values remain data.
Option labels are localized while their original values remain unchanged.
Known status labels are mapped for presentation only; unknown nonempty labels
remain verbatim. Full native workflow labels retain their meaning even when an
older API assigns a generic semantic chip code. Filters still submit the native
value and chip codes are untouched.

## Verification — 2026-11-02

- 22 Sales tests pass: the 18 authentication regressions plus resource/format
  parity and source-key coverage, real order-entry rendering in EN and LT,
  unchanged number/date/money formatting and unknown-status preservation.
- Isolated browser QA covers EN/LT list/details, LT order creation, product
  categories/editor/live preview/PPP, accessories and payment methods. Browser
  option values remain Via PPK / Bank transfer / Cash desk / EKA register.
- Local QA uses the existing sample-only review mode with the Sales API pointing
  at a disabled loopback address; no business records are written.
- Shared cookie persistence through Portal was verified during the earlier
  translation pass; remote test acceptance follows this deployment.
- The pre-existing Sales WebUI → Infrastructure architecture violation is
  outside localization, as in the authentication fix PR #228.

The full Release solution build passes with existing warnings. Treating all
solution warnings as errors is blocked by 14 pre-existing dependency advisory
errors (Marten, ImageSharp, SSH.NET and related packages). The strict Sales WebUI
build passes with zero warnings. Dependency upgrades are outside this task.
