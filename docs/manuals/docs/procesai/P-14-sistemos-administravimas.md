# P-14 — Sistemos administravimas ir atitiktis (System Administration & Compliance)

**Proceso numeris:** P-14
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Sistemos administravimas: vartotojai, prieigos, saugumas, GDPR, atsarginės kopijos ir atitiktis (System Administration & Compliance)

---

## Tikslas

Valdyti sistemos konfigūraciją, vartotojų prieigą, saugumo parametrus ir užtikrinti atitiktį teisiniams reikalavimams.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sistemos administratorius | Visos administravimo funkcijos |
| Atitikties pareigūnas / auditorius | Audito žurnalai, atitikties ataskaitos |

---

## Prieš pradedant (preconditions)

- Sistemos administratoriaus prieigos duomenys

---

## Žingsniai

### Vartotojų valdymas

1. Eik į `Admin` → `Users` (UI: Admin → Users).
2. Sukurk naują vartotoją: paspausk `+ Create` (UI: Create).
3. Priskirk rolę: `Admin` → `Roles` (UI: Admin → Roles).

### API raktų valdymas

1. Eik į `Admin` → `API Keys` (UI: Admin → API Keys).
2. Sukurk naują raktą: `+ Create` (UI: Create).
3. Rotuok raktą: paspausk `Rotate` (UI: Rotate).
4. Ištrink nebenaudojamą raktą: `Delete` (UI: Delete).

### Audito žurnalų peržiūra

1. Eik į `Admin` → `Audit Logs` (UI: Admin → Audit Logs).
2. Filtruok pagal datą, vartotoją, veiksmą.

### GDPR duomenų ištrynimas

1. Eik į `Admin` → `GDPR Erasure` (UI: Admin → GDPR Erasure).
2. Sukurk ištrynimo užklausą su asmens duomenimis.
3. Stebėk užklausos būseną.

### Atsarginės kopijos

1. Eik į `Admin` → `Backups` (UI: Admin → Backups).
2. Paspausk `Backup Now` (UI: Backup Now) rankinei kopijai.
3. Jei reikia atstatyti — pasirink kopiją ir paspausk `Restore` (UI: Restore).

### Katastrofų valdymo testai (DR Drills)

1. Eik į `Admin` → `DR Drills` (UI: Admin → DR Drills).
2. Paleisk testą: `Run Drill` (UI: Run Drill).
3. Patikrink rezultatus ir dokumentuok.

### Duomenų saugojimo politikos

1. Eik į `Admin` → `Retention Policies` (UI: Admin → Retention Policies).
2. Sukonfigūruok, kiek laiko saugoti duomenis.

### Patvirtinimo taisyklės

1. Eik į `Admin` → `Approval Rules` (UI: Admin → Approval Rules).
2. Sukonfigūruok, kuriems veiksmams reikia vadovo patvirtinimo.

---

## Rezultatas (expected result)

- Sistema tinkamai sukonfigūruota
- Prieigos teisės valdymo sistema veikia
- Audito žurnalas pilnas ir prieinamas auditoriams

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-15 Pagrindinių duomenų valdymas](P-15-pagrindinis-duomenu-valdymas.md)
- [P-13 Ataskaitos](P-13-ataskaitos-analize-reports.md) — atitikties ataskaitos
