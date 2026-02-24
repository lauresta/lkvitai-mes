# Sandėlio operatorius

**Rolė sistemoje:** `Operator` (UI: Warehouse Operator)

---

## Aprašymas

Sandėlio operatorius atsakingas už fizines sandėlio operacijas: prekių priėmimą, padėjimą į vietas, krovimą, pakavimą ir atsargų patikrą.

## Prieigos teisės

| Sritis | Prieiga |
|--------|---------|
| Atsargų peržiūra | ✅ Skaitoma |
| Prekių priėmimas | ✅ Vykdymas (kartu su QC inspektoriumi) |
| Padėjimas į vietą | ✅ Vykdymas |
| Krovimo užduotys | ✅ Vykdymas |
| Atsargų koregavimas | ❌ Tik vadovas |
| Administravimas | ❌ |

## Pagrindiniai meniu punktai (UI)

Prisijungęs operatorius mato:

- `Stock` → `Available Stock` (UI: Stock → Available Stock)
- `Stock` → `Location Balance` (UI: Stock → Location Balance)
- `Inbound` → `Inbound Shipments` (UI: Inbound → Inbound Shipments)
- `Inbound` → `Putaway` (UI: Inbound → Putaway)
- `Outbound` → `Picking Tasks` (UI: Outbound → Picking Tasks)
- `Operations` → `Warehouse Map` (UI: Operations → Warehouse Map)

## Susiję procesai

- [P-01 Prekių priėmimas](../procesai/P-01-priemimas-inbound.md)
- [P-02 Padėjimas į vietą](../procesai/P-02-padeti-i-lokacija-putaway.md)
- [P-03 Išsiuntimas](../procesai/P-03-israsymas-isuntimas-outbound.md)
- [P-04 Perkėlimas](../procesai/P-04-perkelimas-transfer.md)
- [P-05 Inventorizacija](../procesai/P-05-inventorizacija-cycle-count.md)
