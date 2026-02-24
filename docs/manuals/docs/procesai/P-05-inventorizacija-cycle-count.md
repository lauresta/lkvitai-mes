# P-05 — Inventorizacija (Cycle Count)

**Proceso numeris:** P-05
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Periodinė atsargų inventorizacija ir neatitikimų šalinimas (Cycle Count / Stock Reconciliation)

---

## Tikslas

Periodiškai patikrinti fizines atsargas pagal sistemos duomenis ir ištaisyti neatitikimus.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio operatorius | Atlieka fizinį skaičiavimą (skenavimą) |
| Sandėlio vadovas | Planuoja inventorizaciją, tvirtina neatitikimus |

---

## Prieš pradedant (preconditions)

- Sandėlio vietos sukonfigūruotos
- Operatoriai žino, kurias vietas tikrinti
- Vadovas sukūrė inventorizacijos užduotį

---

## Žingsniai

### 1. Suplanuoti inventorizaciją (sandėlio vadovas)

1. Eik į `Operations` → `Cycle Counts` (UI: Operations → Cycle Counts).
2. Paspausk `+ Schedule` (UI: Schedule).
3. Pasirink vietas, kurias reikia tikrinti.
4. Nustatyk tikrinimo datą.
5. Paspausk `Save` (UI: Save).

### 2. Atlikti fizinį skaičiavimą (operatorius)

1. Eik į `Operations` → `Cycle Counts` (UI: Operations → Cycle Counts).
2. Rask savo inventorizacijos užduotį ir paspausk `Execute` (UI: Execute).
3. Kiekvienai vietai ir prekei:
   - Nuskenavus barkodą arba pasirinkus prekę — įvesk faktinį kiekį (`Counted Qty`, UI: Counted Qty).
4. Po visų vietų — paspausk `Submit Count` (UI: Submit Count).

### 3. Peržiūrėti neatitikimus (sandėlio vadovas)

1. Eik į inventorizacijos detales ir pasirink `Discrepancies` (UI: Discrepancies).
2. Matai sąrašą vietų, kur sistemos kiekis skiriasi nuo fizinio.
3. Kiekvienam neatitikimui pasirink priežastį (`Reason Code`, UI: Reason Code).
4. Patvirtink koregavimą — paspausk `Approve` (UI: Approve).
5. Sistema automatiškai sukuria atsargų koregavimą.

### 4. Užbaigti inventorizaciją (sandėlio vadovas)

1. Grįžk į inventorizacijos sąrašą.
2. Paspausk `Complete` (UI: Complete).

---

## Rezultatas (expected result)

- Inventorizacijos statusas = `Completed` (UI: Completed)
- Patvirtinti neatikimai ištaisyti `Location Balance` ir `Available Stock` skiltyse
- Istorija prieinama per `Reports` → `Stock Movements` (UI: Reports → Stock Movements)

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-06 Atsargų koregavimas](P-06-atsargu-koregavimas-adjustments.md) — rankinis koregavimas be inventorizacijos
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md)
