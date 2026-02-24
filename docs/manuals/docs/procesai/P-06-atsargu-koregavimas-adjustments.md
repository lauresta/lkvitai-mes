# P-06 — Atsargų koregavimas (Stock Adjustments)

**Proceso numeris:** P-06
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Rankinis atsargų kiekių koregavimas (Stock Adjustments & Write-offs)

---

## Tikslas

Ištaisyti atsargų kiekių neatitikimus dėl žalos, praradimų, suradimų ar kitų priežasčių, neatliekant pilnos inventorizacijos.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio vadovas | Turi išskirtinę teisę koreguoti atsargas |

---

## Prieš pradedant (preconditions)

- Priežasties kodai (`Reason Codes`) sukonfigūruoti sistemoje (P-14)
- Vadovas žino koregavimo priežastį

---

## Žingsniai

### 1. Atidaryti koregavimo formą

1. Eik į `Stock` → `Adjustments` (UI: Stock → Adjustments).
2. Paspausk `+ New Adjustment` (UI: New Adjustment).

### 2. Užpildyti koregavimo duomenis

1. Pasirink sandėlio vietą (`Location`, UI: Location).
2. Pasirink prekę (`Item`, UI: Item).
3. Įvesk koregavimo kiekį:
   - Teigiamas skaičius = kiekio didinimas (surastos prekės)
   - Neigiamas skaičius = kiekio mažinimas (žalos, praradimo atveju)
4. Pasirink priežasties kodą (`Reason Code`, UI: Reason Code).
5. Jei reikia — pridėk pastabą (`Notes`, UI: Notes).
6. Paspausk `Submit` (UI: Submit).

### 3. Patikrinti rezultatą

1. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance).
2. Patikrink atnaujintą kiekį.
3. Koregavimo istoriją žiūrėk: `Reports` → `Stock Movements` (UI: Reports → Stock Movements).

---

## Rezultatas (expected result)

- Atsargų kiekis atnaujintas `Location Balance` ir `Available Stock` skiltyse (≤5 sek.)
- Koregavimo įrašas (`StockMovement` tipo `ADJUSTMENT`) sukurtas su priežasties kodu

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas
> **Svarbu:** Koregavimas negrąžinamas be naujo koregavimo. Prieš tvirtinant — du kartus patikrink kiekius.

---

## Susiję procesai

- [P-05 Inventorizacija](P-05-inventorizacija-cycle-count.md) — formalus neatitikimų taisymo procesas
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md) — judėjimų istorija
