# P-13 — Ataskaitos ir analizė (Reports & Analytics)

**Proceso numeris:** P-13
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Atsargų, judėjimų ir vykdymo ataskaitų bei KPI peržiūra (Reporting & Analytics)

---

## Tikslas

Gauti aktualią informaciją apie sandėlio atsargas, judėjimus, priėmimus, išsiuntimus ir efektyvumą.

---

## Kam skirta (rolės)

| Rolė | Prieiga |
|------|---------|
| Sandėlio vadovas | Visos operatyvinės ataskaitos |
| Inventoriaus buhalteris | Atsargų likučių ir vertinimo ataskaitos |
| Finansų direktorius (CFO) | Pasenusių atsargų ataskaita |
| Atitikties pareigūnas / auditorius | Atitikties audito ataskaita |

---

## Prieš pradedant (preconditions)

- Sistemos duomenys atnaujinti (projekcijų vėlavimas ≤5 sek.)

---

## Žingsniai

### Atsargų likučio peržiūra

1. Eik į `Stock` → `Available Stock` (UI: Stock → Available Stock) — prieinamas kiekis.
2. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance) — detalus kiekis pagal vietą.

### Prekių judėjimų istorija

1. Eik į `Reports` → `Stock Movements` (UI: Reports → Stock Movements).
2. Filtruok pagal datą, prekę, vietą.

### Priėmimų istorija

1. Eik į `Reports` → `Receiving History` (UI: Reports → Receiving History).

### Krovimų istorija

1. Eik į `Reports` → `Pick History` (UI: Reports → Pick History).

### Išsiuntimų istorija

1. Eik į `Reports` → `Dispatch History` (UI: Reports → Dispatch History).

### Atitikties auditų ataskaita

1. Eik į `Reports` → `Compliance Audit` (UI: Reports → Compliance Audit).

### Vykdymo KPI

1. Eik į `Analytics` → `Fulfillment KPIs` (UI: Analytics → Fulfillment KPIs).
2. Eik į `Analytics` → `Quality Analytics` (UI: Analytics → Quality Analytics).

### Projekcijų būsena (administratorius)

1. Eik į `Operations` → `Projections` (UI: Operations → Projections).
2. Matai projekcijų vėlavimą ir galimybę paleisti atstatymą (`Rebuild`, UI: Rebuild).

---

## Rezultatas (expected result)

- Ataskaita pateikta ekrane
- Galima eksportuoti (CSV, Excel) — funkcionalumas priklauso nuo versijos

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas kiekvienos ataskaitos pavyzdžiu
> **Pastaba:** Jei duomenys rodomi kaip `Refreshing...` — palaukite iki 5 sekundžių.

---

## Susiję procesai

- Visi procesai (P-01..P-12) generuoja duomenis ataskaitoms
- [P-08 Agnum integracija](P-08-agnum-integracija.md) — finansinių duomenų eksportas
