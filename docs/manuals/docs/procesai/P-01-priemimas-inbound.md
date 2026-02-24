# P-01 — Prekių priėmimas (Inbound Goods Receiving)

**Proceso numeris:** P-01
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Prekių priėmimas iš tiekėjų (Inbound Goods Receiving)

---

## Tikslas

Priimti prekes iš tiekėjo, patikrinti kokybę, užregistruoti atsargas sistemoje ir paruošti prekes sandėliavimui.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio operatorius | Registruoja priimtas prekes, sukuria sandėliavimo vienetus (HU) |
| Kokybės inspektorius | Atlieka kokybės patikrinimą, patvirtina arba atmeta siuntą |
| Sandėlio vadovas | Sukuria siuntos įrašą sistemoje |

---

## Prieš pradedant (preconditions)

- Tiekėjas užregistruotas sistemoje (žr. [P-15 Pagrindinių duomenų valdymas](P-15-pagrindinis-duomenu-valdymas.md))
- Priimamos prekės (SKU) egzistuoja sistemoje
- Sandėlio vieta (priėmimo dokas) yra sukonfigūruota
- Kokybės inspektorius yra pasiekiamas

---

## Žingsniai

### 1. Sukurti siuntos įrašą (vadovas arba operatorius)

1. Eik į `Inbound` → `Inbound Shipments` (UI: Inbound → Inbound Shipments).
2. Paspausk mygtuką `+ Create` (UI: Create).
3. Užpildyk laukus:
   - `Supplier` (UI: Supplier) — pasirink tiekėją iš sąrašo
   - `Expected Date` (UI: Expected Date) — numatyta priėmimo data
   - Pridėk siuntų eilutes: prekę (`Item`, UI: Item), kiekį (`Expected Qty`, UI: Expected Qty), matavimo vienetą (`UoM`, UI: UoM)
   - Jei prekė turi partijų (lot) valdymą — įvesk `Lot Number` (UI: Lot Number)
4. Paspausk `Save` (UI: Save).

### 2. Užregistruoti gautus kiekius (operatorius)

1. Eik į `Inbound` → `Inbound Shipments` (UI: Inbound → Inbound Shipments).
2. Rask siuntą ir ją atidaryk (paspausk ant jos pavadinimo).
3. Kiekvienai eilutei įvesk faktiškai gautą kiekį (`Received Qty`, UI: Received Qty).
4. Nurodyk priėmimo vietą (`Location`, UI: Location).
5. Paspausk `Receive Items` (UI: Receive Items).

### 3. Kokybės patikrinimas (kokybės inspektorius)

1. Eik į `Inbound` → `Receiving QC` (UI: Inbound → Receiving QC).
2. Rask sukurtą tikrinimo užduotį.
3. Atlik fizinį patikrinimą.
4. Jei kokybė tinkama — paspausk `Approve` (UI: Approve).
5. Jei kokybė netinkama — paspausk `Reject` (UI: Reject) ir nurodyk priežastį.
   - Atmettos prekės perkeliamos į karantino zoną.

### 4. Etikečių spausdinimas (operatorius)

1. Po patvirtinimo sistema automatiškai spausdina etiketes.
2. Jei etikečių nėra — eik į `Outbound` → `Labels` (UI: Outbound → Labels) ir pakartotinai spausdink.

---

## Rezultatas (expected result)

- Siuntos statusas = `Received` (UI: Received)
- Sandėliavimo vieneto (HU) barkodas nuskenavimas
- Atsargų likutis atnaujintas `Location Balance` (UI: Stock → Location Balance) skiltyje (≤5 sek.)
- Prieinamos atsargos atnaujintos `Available Stock` (UI: Stock → Available Stock) skiltyje

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas (screenshots) šioms formoms:
> - Siuntos kūrimo forma
> - QC tikrinimo eilė
> - Etiketės spausdinimo langas

---

## Susiję procesai

- [P-02 Padėjimas į vietą](P-02-padeti-i-lokacija-putaway.md) — kitas žingsnis po priėmimo
- [P-11 Partijų ir serijų sekimas](P-11-partiju-seriju-sekimas.md) — partijų valdymas
- [P-09 Grąžinimai (RMA)](P-09-grazinimai-rma.md) — grąžinamų prekių priėmimas
