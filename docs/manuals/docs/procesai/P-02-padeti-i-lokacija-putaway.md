# P-02 — Padėjimas į vietą (Putaway)

**Proceso numeris:** P-02
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Priimtų prekių padėjimas į sandėlio saugojimo vietas (Putaway & Location Assignment)

---

## Tikslas

Po prekių priėmimo (P-01) perkelti sandėliavimo vienetus (HU) iš priėmimo doko į jų saugojimo vietas sandėlyje.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio operatorius | Vykdo padėjimo užduotis |
| Sandėlio vadovas | Sukuria rankines padėjimo užduotis |

---

## Prieš pradedant (preconditions)

- Siunta priimta ir kokybės tikrinimas atliktas (P-01 baigtas)
- Sandėlio vietos (bins) sukonfigūruotos sistemoje (P-15)
- Sandėliavimo vienetas (HU) turi barkodą

---

## Žingsniai

### 1. Atidaryti padėjimo užduotis (operatorius)

1. Eik į `Inbound` → `Putaway` (UI: Inbound → Putaway).
2. Matai sąrašą laukiančių padėjimo užduočių.
3. Pasirink užduotį, kurią nori vykdyti — paspausk ant jos.

### 2. Vykdyti padėjimo užduotį (operatorius)

1. Nuskenavus sandėliavimo vieneto (HU) barkodą — sistema nurodo tikslą (`Target Location`, UI: Target Location).
2. Fiziškai perkelk prekę į nurodytą vietą.
3. Nuskenavus tikslinę vietą — paspausk `Complete` (UI: Complete).

### 3. Patikrinti rezultatą

1. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance).
2. Įvesk sandėlio vietą ir patikrink, ar prekės rodomos teisingai.

---

## Rezultatas (expected result)

- Sandėliavimo vienetas (HU) užfiksuotas tikslinėje vietoje
- Atsargų likutis atnaujintas abiejose vietose (išvykimo ir atvykimo) — `Location Balance`
- Padėjimo užduoties statusas = `Completed` (UI: Completed)

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md) — pirmtakas
- [P-04 Perkėlimas](P-04-perkelimas-transfer.md) — rankinis perkėlimas tarp vietų
- [P-12 Sandėlio žemėlapis](P-12-sandelio-zemelapis-visualization.md) — vietų paieška
