# Authoring rules for the user manuals

These manuals are read by people (MkDocs site) and by AI assistants (every page is split into chunks and served by the Cikada MCP tools `docs_search` / `docs_get`). Write for both.

## Language

- User-facing text is Lithuanian, with correct diacritics.
- Every button, menu item, tab and field is given exactly as the screen shows it, in brackets: `(UI: Create Inbound)`, `(UI: Finance > Month end)`. Do not translate UI labels.
- Do not invent steps. Every step must be traceable to a page, button or API call that exists in the code. If you could not confirm something, leave it out.

## What is ready and what is a stub

Each page starts with a status line right under the title:

- `**Būsena:** veikia` — the screen works end to end.
- `**Būsena:** dalinai` — some parts work; list what does not.
- `**Būsena:** laikina (zagluška)` — the screen exists but shows fixed sample data, has buttons that do nothing, or is a placeholder. Say exactly what is a placeholder.

Decide the status from the code (hard-coded sample data, `NotImplemented`, TODO, disabled buttons, missing API calls), not from the menu.

## Page layout (one page per process or per screen group)

```markdown
---
module: shopfloor            # warehouse | sales | shopfloor | frontline | finance | scanning | portal | mcp
title: Operatoriaus darbų eilė
routes: ["/shopfloor/operator-work-queue"]
roles: ["Operator", "ShopfloorManager"]     # roles from the code; [] if open to every signed-in user
status: veikia               # veikia | dalinai | laikina
---

# Operatoriaus darbų eilė

**Būsena:** veikia

## Kam skirta
Two or three sentences: who uses it and for what.

## Prieš pradedant
Access rights, data that must exist first.

## Žingsniai
### 1. Short name of the step
1. Numbered actions with `(UI: ...)` labels.

![Short description of the picture](../img/shopfloor/operator-work-queue.png)

## Ką reiškia būsenos ir spalvos        (when the screen has statuses)
## Dažnos klaidos                        (message shown -> cause -> what to do)
## Dar neįgyvendinta                     (only when something is a stub)
```

Rules that matter for the AI chunks:

- Each `##` section must make sense when read alone: name the screen and module in its first sentence instead of saying "this page".
- Keep a `##` section under roughly 350 words; split a long procedure into several `###` steps.
- No tables wider than four columns. No HTML.
- Use the same Lithuanian term for the same thing everywhere on the page.

## Screenshots

- Files go to `docs/manuals/docs/img/<module>/<name>.png`; reference them relatively from the page (`../img/<module>/<name>.png`).
- Do not create image files. Instead add an entry for every picture you reference to `docs/manuals/shots/<module>.json`:

```json
[
  {
    "file": "img/shopfloor/operator-work-queue.png",
    "url": "https://cikada-test.lauresta.com/shopfloor/operator-work-queue",
    "steps": [
      { "click": "button:has-text('Filtrai')" },
      { "fill": ["input[placeholder='Paieška']", "ROL"] },
      { "wait": 1000 }
    ],
    "fullPage": false,
    "note": "what should be visible"
  }
]
```

- `url` is the test stand address. Module base paths: `/warehouse`, `/sales`, `/shopfloor`, `/frontline`, `/finance`, `/scan`, `/portal`.
- `steps` are optional Playwright actions run after the page loads (selectors must come from the page markup in the code). Only actions that do not change data: open a dialog, switch a tab, type into a filter. Never a save, post, delete or send.
- For a URL that needs an id, put `{firstRow}` style placeholders only if you also explain in `note` how to get the value; prefer list pages.
- One or two pictures per page; a picture per step only where the step is hard to find.

### Real data on the test stand

Sales, Finance and the Shopfloor queue read the live legacy database even on the test stand. Never put customer or payer names into the manuals: add `"blurColumns": ["Klientas"]` (table column header texts) or `"blur": [".css-selector"]` to the shot entry, and for detail screens use an order of the test series.
