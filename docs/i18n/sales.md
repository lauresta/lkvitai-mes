# Sales EN / LT localization

Sales follows [the shared localization contract](README.md): English is the
neutral resource and Lithuanian is the `lt` satellite; the shared topbar and
`.AspNetCore.Culture` cookie select the UI language across modules.

## Coverage and contracts

`SalesResources.resx` and `SalesResources.lt.resx` contain 718 matching keys for
the live order list/details/editor, product dimensions/live preview, PPP,
accessories, cascading payments, production confirmation, privacy notice,
read-only note history, validation/notifications and pending-command recovery.
The existing sample-only review flow is also localized. This integration keeps
the full M2 business flow together with current main's shared localization,
SSO support and validated review-cookie fallback.

Known API validation text is translated at the WebUI boundary by `SalesApiErrors`.
Exact messages take precedence; code-only fallbacks apply only to empty messages.
Unknown nonempty messages, customer notes and legacy data remain verbatim.
Dynamic numbers, formula tokens and technical field prefixes are preserved so
field-error focus and recovery continue to work.

Submitted option/filter values, control/payment/duty values, legacy cancellation
markers, customer/product/material names and audit history are not rewritten.
Only `CurrentUICulture` changes: prices, dates, dimensions, decimal parsing,
order-number rules, SQL permissions and command identities retain their behavior.
Known full native workflow labels retain their meaning in presentation even when
an older API supplies a generic semantic chip code; unknown nonempty labels stay
verbatim. Database state IDs and chip-family codes remain unchanged.

Material selection receives the actual existing code from the product draft,
including unavailable legacy materials; it never displays the C# property
expression as a literal. Thumbnails/stock metadata and server-side material
validation retain their existing behavior.

## Verification

Resource parity/format arguments and literal-key coverage span Razor, C# and
CSHTML. Regression tests cover UI-only culture changes, unchanged numbering and
money/date formatting, unknown API/native-data preservation, EN/LT privacy and
history rendering, the order-entry review flow, and stored material codes in the
actual product editor. Full Sales and architecture verification is recorded in
`docs/sales/milestone-2-verification.md` during integration/deployment.

Prior isolated browser QA covered EN/LT order list/details and LT product,
PPP/accessory/payment screens with API destinations disabled. Current-main
translation PR #229 was also verified on test in Safari, including EN ↔ LT and
Portal → Sales navigation. The integrated M2 test deployment is verified
separately; no review-mode sample can write legacy business data.

Existing dependency advisories remain outside localization; treating every
solution warning as an error is blocked by those advisories. The strict Sales
WebUI build itself is checked separately without upgrading unrelated packages.
