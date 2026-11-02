# Sales login loop recurrence — 2026-11-02

The test deployment of main revision `d0ad3da` replaced the manually deployed
M2 Sales WebUI containing the S2-016 correction. Main still uses a policy
selector based only on the presence of `lkvitai.sales.review`. An expired,
tampered or otherwise invalid review ticket shadows a valid Portal session.
Portal logs show successful login followed by the Sales login page again.
This is not evidence that Safari rejected the Portal cookie or password.

The narrow correction restores `SalesReviewSelectorHandler`: authenticate the
review ticket first, retain sample-only priority only when it succeeds, and
otherwise validate the normal Portal cookie. No identities are combined and
invalid Portal tickets remain unauthenticated. No cookies, keys, credentials,
API authentication rules or database permissions are changed.

Validation: the original selector fails four of eighteen cookie-combination
regressions; the corrected selector passes all eighteen. Tests include valid,
absent, expired, tampered, wrong-key and invalid-identity review tickets, disabled
review configuration, and valid/expired/tampered/absent Portal tickets. The fix
is prepared against main including PR #226 (SSO), separately from M2 and Sales
localization. Browser acceptance on test is pending deployment.

A durable repair must be included in main; another manually patched container
would be overwritten by the next automatic deployment. At preparation time,
the separate SSO deployment was running database migrations. Its deployment
must not be raced or rolled back by this correction.
