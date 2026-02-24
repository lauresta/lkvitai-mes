# P-11 — Partijų ir serijų numerių sekimas (Lot & Serial Traceability)

**Proceso numeris:** P-11
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Partijų (lot) ir serijų numerių sekimas per visą tiekimo grandinę (Lot & Serial Number Traceability)

---

## Tikslas

Stebėti, iš kurio tiekėjo atėjo kuri partija, kokioje vietoje sandėliuota, į kokius orderius išsiųsta — atsekamumo tikslais (atšaukimų, kokybės patikrinimų metu).

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Kokybės inspektorius | Sukuria ir tvarko partijų numerius |
| Atitikties pareigūnas | Atlieka atsekamumo užklausas, eksportuoja ataskaitas |
| Sandėlio vadovas | Konfigūruoja partijų ir serijų taisykles |

---

## Prieš pradedant (preconditions)

- Partijų valdymas įjungtas prekės konfigūracijoje
- Partijos numeris buvo priskirtas priėmimo metu (P-01)

---

## Žingsniai

### Partijų sukūrimas ir tvarkymas (kokybės inspektorius)

1. Eik į `Admin` → `Lots` (UI: Admin → Lots).
2. Paspausk `+ Create` (UI: Create).
3. Įvesk partijos numerį (`Lot Number`, UI: Lot Number), prekę, galiojimo datą.
4. Paspausk `Save` (UI: Save).

### Atsekamumo užklausa

1. Eik į `Reports` → `Traceability` (UI: Reports → Traceability).
2. Įvesk partijos numerį arba serijų numerį.
3. Sistema rodo visą grandinę: tiekėjas → priėmimas → sandėliavimo vieta → orderis → išsiuntimas.

### Detali partijos atsekamumo peržiūra

1. Eik į `Reports` → `Lot Traceability` (UI: Reports → Lot Traceability).
2. Pasirink partijos numerį.
3. Matai išsamią judėjimų istoriją.

### Serijų numerių konfigūracija

1. Eik į `Admin` → `Serial Numbers` (UI: Admin → Serial Numbers).
2. Sukonfigūruok serijų numerių taisykles.

---

## Rezultatas (expected result)

- Pilna partijos grandinė matoma ataskaitose
- Atsekamumo ataskaita gali būti eksportuojama

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md) — partijos priskyrimas
- [P-03 Išsiuntimas](P-03-israsymas-isuntimas-outbound.md) — partijos judėjimas
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md)
