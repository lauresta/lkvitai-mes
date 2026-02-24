# Kokybės inspektorius

**Rolė sistemoje:** `QC Inspector` (UI: QC Inspector)

---

## Aprašymas

Kokybės inspektorius tikrina priimamų ir grąžinamų prekių kokybę, patvirtina arba atmeta siuntas, sukuria ir tvarko partijų (lot) numerius.

## Prieigos teisės

| Sritis | Prieiga |
|--------|---------|
| Atsargų peržiūra | ✅ Skaitoma |
| QC tikrinimas (priėmimas) | ✅ Vykdymas |
| Partijų kūrimas | ✅ |
| Sandėlio operacijos | ✅ (kaip operatorius) |
| Atsargų koregavimas | ❌ Tik vadovas |
| Administravimas | ❌ |

## Pagrindiniai meniu punktai (UI)

- `Inbound` → `Receiving QC` (UI: Inbound → Receiving QC)
- `Inbound` → `Inbound Shipments` (UI: Inbound → Inbound Shipments)
- `Reports` → `Traceability` (UI: Reports → Traceability)
- `Admin` → `Lots` (UI: Admin → Lots)

## Susiję procesai

- [P-01 Prekių priėmimas](../procesai/P-01-priemimas-inbound.md) — QC tikrinimo žingsniai
- [P-09 Grąžinimai (RMA)](../procesai/P-09-grazinimai-rma.md) — grąžintų prekių tikrinimas
- [P-11 Partijų ir serijų sekimas](../procesai/P-11-partiju-seriju-sekimas.md)
