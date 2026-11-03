# Shopfloor localization and shared header

Shopfloor follows [the module localization contract](README.md). Its Blazor pages use
`ShopfloorResources.resx` for English and `ShopfloorResources.lt.resx` for Lithuanian.
The shared Portal language switcher sets the culture cookie for both Shopfloor
layouts. `GamybaLayout` uses `PortalAuthenticatedShell`, so the environment badge,
version, user and language switcher come from the authenticated Portal session.
The production barcode/GU/GT search remains directly below the module tabs and
still supports F2 and Enter.

The workflow editor and operator queue are embedded HTML editors. Their iframe
URL receives the host's UI culture as `?culture=lt` because the culture cookie is
HttpOnly. `shopfloor-prototype-i18n.js` changes display text only; task data,
identifiers, route names, `data-*` command values and input values are unchanged.
The editor toolbars keep their actions, while their duplicate brand blocks are
hidden because the page already has the shared Portal header.
