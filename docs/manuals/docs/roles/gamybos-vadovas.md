# Gamybos vadovas / Sandėlio vadovas

**Rolė sistemoje:** `Manager` (UI: Warehouse Manager)

---

## Aprašymas

Sandėlio vadovas atsakingas už operacijų planavimą, patvirtinimą ir stebėseną. Turi teisę koreguoti atsargas, tvirtinti perkėlimus ir inventorizacijos rezultatus.

## Prieigos teisės

| Sritis | Prieiga |
|--------|---------|
| Visos operatoriaus funkcijos | ✅ |
| Atsargų koregavimas | ✅ |
| Inventorizacijos planavimas ir patvirtinimas | ✅ |
| Perkėlimų kūrimas ir tvirtinimas | ✅ |
| Pardavimo orderių valdymas | ✅ |
| Partijų ir serijų valdymas | ✅ |
| Ataskaitos | ✅ |
| Vertinimo funkcijos | Tik apžvalga (CFO tvirtina) |
| Administravimas | ❌ Tik administratorius |

## Pagrindiniai meniu punktai (UI)

- `Stock` → visos funkcijos (UI: Stock)
- `Inbound` → visos funkcijos (UI: Inbound)
- `Outbound` → visos funkcijos (UI: Outbound)
- `Operations` → visos funkcijos (UI: Operations)
- `Finance` → `Valuation` apžvalga (UI: Finance → Valuation)
- `Reports` → visos ataskaitos (UI: Reports)
- `Analytics` → visi KPI (UI: Analytics)

## Susiję procesai

Vadovas dalyvauja visuose procesuose P-01..P-13. Svarbiausi:

- [P-05 Inventorizacija](../procesai/P-05-inventorizacija-cycle-count.md) — planuoja ir tvirtina
- [P-06 Atsargų koregavimas](../procesai/P-06-atsargu-koregavimas-adjustments.md) — vienintelis galintis koreguoti
- [P-04 Perkėlimas](../procesai/P-04-perkelimas-transfer.md) — tvirtina perkėlimus
- [P-13 Ataskaitos](../procesai/P-13-ataskaitos-analize-reports.md)
