# P-10 — Kryžminis perkrovimas (Cross-Dock)

**Proceso numeris:** P-10
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Kryžminis perkrovimas: tiesioginis pervežimas iš priėmimo į išsiuntimą (Cross-Dock Operations)

---

## Tikslas

Perkelti prekes tiesiogiai iš priėmimo doko į išsiuntimo dokso nepadedant jų į saugojimo vietas. Taikoma kai gaunamos prekės jau turi laukiantį užsakymą.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio vadovas | Identifikuoja ir tvirtina kryžminio perkrovimo galimybę |
| Išsiuntimo tarnautojas | Patvirtina galutinį išsiuntimą |

---

## Prieš pradedant (preconditions)

- Yra gaunamų prekių siunta (P-01)
- Yra laukiantis išsiuntimo orderis toms pačioms prekėms (P-03)
- Sandėlio vadovas identifikavo sutapimą

---

## Žingsniai

### 1. Sukurti kryžminio perkrovimo įrašą

1. Eik į `Outbound` → `Cross-Dock` (UI: Outbound → Cross-Dock).
2. Paspausk `+ Create` (UI: Create).
3. Susietas įeinančią siuntą ir išeinantį orderį.
4. Paspausk `Save` (UI: Save).

### 2. Vykdyti perkrovimą

1. Rask įrašą sąraše.
2. Paspausk `Update Status` (UI: Update Status) kai prekės fiziškai perkeltos.

### 3. Patvirtinti išsiuntimą

1. Eik į `Outbound` → `Dispatch` (UI: Outbound → Dispatch).
2. Patvirtink išsiuntimą kaip įprastai.

---

## Rezultatas (expected result)

- Prekės išsiųstos be sandėliavimo
- Atsargų judėjimas užfiksuotas sistemoje

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas
> **Pastaba:** Kryžminio perkrovimo susiejimo taisyklės (kaip sistema automatiškai pasiūlo sutapimus) dar dokumentuojamos.

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md)
- [P-03 Išsiuntimas](P-03-israsymas-isuntimas-outbound.md)
