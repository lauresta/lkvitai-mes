# P-08 — Agnum integracija ir derinimas

**Proceso numeris:** P-08
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Kasdieninis atsargų vertės eksportas į Agnum apskaitos sistemą ir neatitikimų derinimas (Agnum Integration & Reconciliation)

---

## Tikslas

Automatiškai eksportuoti sandėlio atsargų finansinę vertę į Agnum apskaitos sistemą kiekvieną dieną 23:00 ir suderinti skirtumus.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Inventoriaus buhalteris | Konfigūruoja eksportą, peržiūri derinimo ataskaitas, paleisti rankinį eksportą |
| Sandėlio vadovas | Bendrai peržiūri derinimo rezultatus |
| Sistema (automatinis) | Vykdo eksportą kiekvieną dieną 23:00 |

---

## Prieš pradedant (preconditions)

- Agnum sistemos prisijungimo duomenys sukonfigūruoti
- Laukų susiejimo (`Mapping`) konfigūracija atlikta
- P-07 vertinimo duomenys atnaujinti

---

## Žingsniai

### Konfigūracijos patikrinimas

1. Eik į `Finance` → `Agnum Config` (UI: Finance → Agnum Config).
2. Patikrink, ar API prisijungimo duomenys teisingi.
3. Patikrink laukų susiejimą (`Field Mapping`, UI: Field Mapping).
4. Paspausk `Save` (UI: Save) jei keitei.

### Rankinis eksporto paleidimas

1. Eik į `Finance` → `Agnum Config` (UI: Finance → Agnum Config).
2. Paspausk `Schedule Export` (UI: Schedule Export) arba `Run Now` (UI: Run Now).
3. Stebėk eksporto būseną (`Export Status`, UI: Export Status).

### Derinimo ataskaitos peržiūra

1. Eik į `Finance` → `Agnum Reconcile` (UI: Finance → Agnum Reconcile).
2. Pasirink datos intervalą.
3. Paspausk `Generate Report` (UI: Generate Report).
4. Peržiūrėk skirtingus įrašus (`Discrepancies`, UI: Discrepancies).
5. Jei reikia — koreguok atsargas (P-06 arba P-07) ir pakartok eksportą.

---

## Rezultatas (expected result)

- Agnum sistemoje atnaujinti atsargų duomenys
- Derinimo ataskaita sukurta ir be neatitikimų (arba neatikimai paaiškinami)
- Eksporto įrašas patikimai išsaugotas sistemoje

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas
> **Svarbu:** Automatinis eksportas vyksta 23:00. Nekeisk atsargų duomenų nuo 22:30 iki 23:30, kad nesukelti neatitikimų.

---

## Susiję procesai

- [P-07 Atsargų vertinimas](P-07-atsargu-vertinimas-valuation.md) — duomenų šaltinis
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md)
