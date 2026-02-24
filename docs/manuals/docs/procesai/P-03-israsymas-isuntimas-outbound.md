# P-03 — Išsiuntimas (Outbound Order Fulfillment)

**Proceso numeris:** P-03
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Pardavimo orderių vykdymas: nuo rezervacijos iki išsiuntimo (Outbound Order Fulfillment)

---

## Tikslas

Įvykdyti kliento pardavimo orderį: rezervuoti atsargas, atlikti krovimą, supakuoti ir išsiųsti prekes.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Pardavimų administratorius | Kuria pardavimo orderius |
| Sandėlio vadovas | Patvirtina rezervaciją, paleidžia krovimą |
| Krovimo operatorius | Vykdo krovimo užduotis |
| Pakavimo operatorius | Pakuoja ir spausdina etiketes |
| Išsiuntimo tarnautojas | Patvirtina išsiuntimą |

---

## Prieš pradedant (preconditions)

- Klientas užregistruotas sistemoje
- Prekės (SKU) egzistuoja ir yra sandėlyje (`Available Stock`)
- Sandėlio vietos sukonfigūruotos

---

## Žingsniai

### 1. Sukurti pardavimo orderį (pardavimų administratorius)

1. Eik į `Outbound` → `Sales Orders` (UI: Outbound → Sales Orders).
2. Paspausk `+ Create` (UI: Create).
3. Pasirink klientą (`Customer`, UI: Customer).
4. Pridėk orderio eilutes: prekę, kiekį, matavimo vienetą.
5. Paspausk `Save` (UI: Save).

### 2. Rezervuoti atsargas (pardavimų administratorius)

1. Atsidaryk orderį ir paspausk `Reserve` (UI: Reserve).
2. Sistema sukuria **SOFT** rezervaciją iš prieinamų atsargų.
3. Patikrink rezervacijos statusą: `Stock` → `Reservations` (UI: Stock → Reservations).

### 3. Paleisti krovimą (sandėlio vadovas)

1. Eik į `Outbound` → `Allocations` (UI: Outbound → Allocations).
2. Rask orderį ir paspausk `Release for Picking` (UI: Release for Picking).
3. Sistema konvertuoja SOFT rezervaciją į **HARD** (griežtą) rezervaciją.

### 4. Vykdyti krovimą (krovimo operatorius)

**Paprasto krovimo būdas:**
1. Eik į `Outbound` → `Picking Tasks` (UI: Outbound → Picking Tasks).
2. Pasirink savo krovimo užduotį.
3. Eik į nurodytą sandėlio vietą, pasiimk nurodytą kiekį.
4. Nuskenavus prekę — paspausk `Complete Task` (UI: Complete Task).

**Bangų krovimo būdas (Wave Picking):**
1. Eik į `Outbound` → `Wave Picking` (UI: Outbound → Wave Picking).
2. Pasirink bangą arba sukurk naują (`+ Create Wave`, UI: Create Wave).
3. Priskink operatoriui (`Assign`, UI: Assign) ir paleisk (`Start`, UI: Start).
4. Vykdyk krovimą pagal nurodymus.

### 5. Pakuoti (pakavimo operatorius)

1. Eik prie pakavimo stoties per: `Outbound` → atsidaryk orderį → `Pack` (UI: Pack) arba tiesiogiai `/warehouse/outbound/pack/{OrderId}`.
2. Patikrink pakavimo sąrašą.
3. Spausdink pakuotės etiketę: `Outbound` → `Labels` (UI: Outbound → Labels).

### 6. Patvirtinti išsiuntimą (išsiuntimo tarnautojas)

1. Eik į `Outbound` → `Dispatch` (UI: Outbound → Dispatch).
2. Rask siuntą ir patikrink visus duomenis.
3. Paspausk `Dispatch` (UI: Dispatch).

---

## Rezultatas (expected result)

- Pardavimo orderio statusas = `Dispatched` (UI: Dispatched)
- Atsargos išrašytos iš `Available Stock` ir `Location Balance`
- Siuntos įrašas sukurtas su siuntimo data

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas kiekvienam žingsniui

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md) — atsargų papildymas
- [P-09 Grąžinimai (RMA)](P-09-grazinimai-rma.md) — grąžinamų prekių valdymas
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md) — išsiuntimų istorija
