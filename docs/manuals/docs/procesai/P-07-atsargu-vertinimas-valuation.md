# P-07 — Atsargų vertinimas (Inventory Valuation & Costing)

**Proceso numeris:** P-07
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Atsargų finansinis vertinimas: savikainos keitimas, papildomų kaštų priskyrimas, nurašymai (Inventory Valuation & Costing)

---

## Tikslas

Palaikyti teisingą sandėlyje esančių atsargų finansinę vertę: keisti savikainą, priskirti papildomus kaštus (transportas, muito mokesčiai) ir nurašyti prekes.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Inventoriaus buhalteris | Keičia savikainą, priskiria papildomus kaštus |
| Finansų direktorius (CFO) | Tvirtina nurašymus, peržiūri vertinimo suvestinę |

---

## Prieš pradedant (preconditions)

- Atsargos yra sistemoje
- Savikainos duomenys gauti iš tiekėjo (sąskaita faktūra)
- Papildomų kaštų dokumentai paruošti

---

## Žingsniai

### Savikainos keitimas (Adjust Cost)

1. Eik į `Finance` → `Valuation` → `Adjust Cost` (UI: Finance → Valuation → Adjust Cost).
2. Pasirink prekę (`Item`, UI: Item) arba vietą.
3. Įvesk naują savikainą (`New Unit Cost`, UI: New Unit Cost).
4. Pridėk pastabą.
5. Paspausk `Save` (UI: Save).

### Papildomų kaštų priskyrimas (Apply Landed Cost)

1. Eik į `Finance` → `Valuation` → `Apply Landed Cost` (UI: Finance → Valuation → Apply Landed Cost).
2. Pasirink siuntos numerį ar prekių grupę.
3. Įvesk papildomų kaštų sumą (pvz. transportas).
4. Pasirink paskirstymo metodą (`Allocation Method`, UI: Allocation Method).
5. Paspausk `Apply` (UI: Apply).

### Vertės nurašymas (Write Down)

1. Eik į `Finance` → `Valuation` → `Write Down` (UI: Finance → Valuation → Write Down).
2. Pasirink prekes, kurias nurašyti.
3. Įvesk nurašymo sumą ir priežastį.
4. Paspausk `Submit for Approval` (UI: Submit for Approval).
5. CFO peržiūri ir patvirtina.

### Vertinimo suvestinės peržiūra

1. Eik į `Finance` → `Valuation` (UI: Finance → Valuation).
2. Matai bendrą sandėlio vertę pagal vietą ir prekę.

---

## Rezultatas (expected result)

- Finansiniai įrašai atnaujinti sistemoje
- Suvestinė rodoma `Finance` → `Valuation` (UI: Finance → Valuation)
- Duomenys paruošti eksportui į Agnum (P-08)

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-08 Agnum integracija](P-08-agnum-integracija.md) — eksportas į apskaitą
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md) — atsargų vertės ataskaitos
