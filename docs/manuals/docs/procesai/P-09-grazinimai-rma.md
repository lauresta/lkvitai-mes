# P-09 — Grąžinimai (RMA)

**Proceso numeris:** P-09
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Kliento grąžinamų prekių priėmimas ir apdorojimas (Returns / RMA — Return Merchandise Authorization)

---

## Tikslas

Priimti kliento grąžinamas prekes, patikrinti jų būklę ir nuspręsti: grąžinti į sandėlį arba nurašyti.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Grąžinimų tarnautojas | Kuria RMA įrašą, priima grąžinamas prekes |
| Kokybės inspektorius | Tikrina grąžintų prekių kokybę |
| Sandėlio vadovas | Tvirtina sprendimą: grąžinti į sandėlį ar nurašyti |

---

## Prieš pradedant (preconditions)

- Pirminis pardavimo orderis egzistuoja sistemoje (P-03)
- Klientas pateikė grąžinimo pageidavimą

---

## Žingsniai

### 1. Sukurti RMA įrašą

1. Eik į `Outbound` → `RMAs` (UI: Outbound → RMAs).
2. Paspausk `+ Create RMA` (UI: Create RMA).
3. Nurodyk:
   - Pradinį pardavimo orderį (`Sales Order`, UI: Sales Order)
   - Grąžinamas prekes ir kiekius
   - Grąžinimo priežastį
4. Paspausk `Save` (UI: Save).

### 2. Priimti grąžinamas prekes

1. Fiziškai priimk prekes.
2. RMA įraše pažymėk kaip gautas (`Mark as Received`, UI: Mark as Received).

### 3. Kokybės patikrinimas

1. Kokybės inspektorius atlieka tikrinimą.
2. Jei prekė tinkama — patvirtina (`Approve`, UI: Approve) — prekė grąžinama į sandėlį.
3. Jei prekė pažeista — atmeta (`Reject`, UI: Reject) — prekė nurašoma.

### 4. Patikrinti atsargas

1. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance).
2. Patikrink, ar grąžintos prekės atsispindi teisingai.

---

## Rezultatas (expected result)

- RMA statusas = `Processed` (UI: Processed)
- Tinkamos prekės grąžintos į sandėlį (atsargos padidėjusios)
- Pažeistos prekės nurašytos

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md) — analogiškas procesas
- [P-03 Išsiuntimas](P-03-israsymas-isuntimas-outbound.md) — pirminis orderis
- [P-06 Atsargų koregavimas](P-06-atsargu-koregavimas-adjustments.md) — nurašymo procesas
