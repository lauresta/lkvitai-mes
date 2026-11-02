# Customer-request cancellation notice

`CustomerPrivacyNotice` uses the approved warning tokens and this exact copy:

> Personal data restricted — GDPR
>
> This order was cancelled at the customer’s request. Certain order information has been removed for privacy reasons.
>
> ← Back to orders

Order details use the server-provided `IsPrivacyRestricted` flag. Live order and
product workspaces use `LegacyOrderPrivacy.IsRestricted` against the saved order
note. The shared rule matches only `Kliento prasymu anuliuotas`, ignoring case
and surrounding whitespace. Ordinary cancellation, a partial match, or manually
typing the marker in a demo draft does not trigger this presentation.

The notice includes a translucent, diagonal watermark anchored to the existing
card or workspace surface. Its overlay ignores pointer events; the Back to
orders anchor remains interactive and keyboard reachable. In the live workspace
it uses the existing close handler and unsaved/pending-save safeguards.

Customer, product and monetary values retain their existing rendering. This UI
does not erase, anonymize, blur or substitute stored data. The raw standard order
note marker is replaced by the approved notice rather than shown in the Note
textarea. The active order, product selector or product editor shows the notice;
an obscured workspace beneath the active product view does not duplicate it.

Nine static-renderer checks cover exact copy, inactive output, back-link markup,
all three workspace views, unchanged customer/product/amount output, ordinary
cancellation, partial markers and demo notes.
