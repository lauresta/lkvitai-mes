# Sales EN / LT localization

Sales follows [the shared localization contract](README.md): English is the
neutral resource and Lithuanian is the `lt` satellite; the shared topbar and
`.AspNetCore.Culture` cookie select the UI language across modules.

## Coverage

`SalesResources.resx` and `SalesResources.lt.resx` contain 702 matching keys for
the order list, details, new-order form, product editor/live preview, PPP,
accessories, payments, confirmation, privacy notice, note history, validation,
notifications, session renewal and pending-command recovery. The host page's
language, title and reload/error text also follow the UI culture.

Known API validation text is translated at the WebUI boundary by `SalesApiErrors`.
Exact messages take precedence; code-only fallbacks apply only to empty messages.
Unknown nonempty messages, customer notes and legacy data remain verbatim.
Dynamic numbers, formula tokens and technical field prefixes are preserved so
field-error focus and recovery continue to work. Future API messages need new
resource entries/mappings; the API and SQL contracts remain language-independent.

Stored status/filter values, control/payment/duty values, legacy cancellation
markers, customer/product/material names and audit history are not rewritten.
Only `CurrentUICulture` changes: prices, dates, dimensions, decimal parsing,
order-number rules, SQL permissions and command identities retain their behavior.
Language switching reloads the page as specified by the shared contract; the
existing unsaved-input navigation guards still apply.

## Verification — 2026-11-02

- All 325 Sales tests pass, including resource parity/format arguments, literal
  key coverage (Razor/C#/CSHTML), UI-only culture changes, unchanged numbering and
  money/date formatting, API unknown-data preservation, and EN/LT rendering of
  privacy/history components.
- Full Release solution build passes. Sales builds with warnings as errors and
  no warnings; the full solution retains existing dependency advisories tracked
  under S2-018. No dependency upgrades are included.
- Browser: EN/LT order list and order details, LT new-order form, product category
  selection, product/PPP/accessory fields and live preview; native data and
  monetary/date text remain unchanged. The same culture choice survives
  Sales → Portal → Sales and a language change in Portal.
- Browser QA used isolated loopback hosts, synthetic local authentication and
  the existing sample-only Sales review mode, with API destinations disabled.
  No legacy writes were made. Temporary QA startup code is outside the repo.
- The remote test host currently runs main's shared-localization deployment;
  this M2-based Sales translation is prepared locally on the branch of Sales PR #224
  and has not been committed or pushed.
  This task does not replace that deployment with an unmerged M2 branch.
