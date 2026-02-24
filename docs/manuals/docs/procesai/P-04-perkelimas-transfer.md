# P-04 — Perkėlimas (Internal Stock Transfer)

**Proceso numeris:** P-04
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Vidinės atsargų perkėlimas tarp sandėlio vietų (Internal Stock Transfer)

---

## Tikslas

Perkelti fizines atsargas iš vienos sandėlio vietos į kitą — tarp zonų, sekcijų arba loginių sandėlių.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio operatorius | Vykdo patvirtintą perkėlimą |
| Sandėlio vadovas | Sukuria ir tvirtina perkėlimo užklausą |

---

## Prieš pradedant (preconditions)

- Perkėlimo šaltinis ir tikslo vieta egzistuoja sistemoje
- Atsargos yra šaltinio vietoje
- Jei konfigūruoti patvirtinimo taisyklės (`Approval Rules`) — vadovas turi patvirtinti

---

## Žingsniai

### 1. Sukurti perkėlimo užklausą (sandėlio vadovas)

1. Eik į `Operations` → `Transfers` (UI: Operations → Transfers).
2. Paspausk `+ Create` (UI: Create).
3. Užpildyk:
   - `From Location` (UI: From Location) — išvykimo vieta
   - `To Location` (UI: To Location) — tikslo vieta
   - Prekė (`Item`, UI: Item) ir kiekis (`Qty`, UI: Qty)
4. Paspausk `Save` (UI: Save).
5. Jei reikalingas patvirtinimas — vadovas patvirtina užklausą.

### 2. Vykdyti perkėlimą (operatorius)

1. Eik į `Operations` → `Transfers` (UI: Operations → Transfers).
2. Rask patvirtintą perkėlimą.
3. Paspausk `Execute` (UI: Execute) arba atidaryk detales ir paspausk `Execute Transfer` (UI: Execute Transfer).
4. Fiziškai perkelk prekes.
5. Patvirtink vykdymą.

### 3. Patikrinti rezultatą

1. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance).
2. Patikrink šaltinio ir tikslo vietas — kiekiai turi būti atnaujinti.

---

## Rezultatas (expected result)

- Perkėlimo statusas = `Completed` (UI: Completed)
- Šaltinio vietos likutis sumažėjo
- Tikslo vietos likutis padidėjo
- `Available Stock` atnaujintas (≤5 sek.)

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-02 Padėjimas į vietą](P-02-padeti-i-lokacija-putaway.md)
- [P-12 Sandėlio žemėlapis](P-12-sandelio-zemelapis-visualization.md) — vietos paieška
