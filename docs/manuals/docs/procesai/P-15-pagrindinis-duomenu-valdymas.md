# P-15 — Pagrindinių duomenų valdymas (Master Data Management)

**Proceso numeris:** P-15
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

Pagrindinių duomenų kūrimas ir priežiūra: prekės, tiekėjai, vietos, kategorijos, matavimo vienetai (Master Data Management)

---

## Tikslas

Sukurti ir palaikyti referencinius duomenis, nuo kurių priklauso visi kiti sandėlio procesai.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio vadovas | Kuria ir redaguoja prekes, vietas, kategorijas |
| Sistemos administratorius | Pilnas CRUD, matavimo vienetai, sandėlio maketas |

---

## Prieš pradedant (preconditions)

- Administratoriaus arba vadovo prieigos teisės

---

## Žingsniai

### Prekių (SKU) kūrimas

1. Eik į `Admin` → `Items` (UI: Admin → Items).
2. Paspausk `+ Create` (UI: Create).
3. Užpildyk:
   - `Item Code` (UI: Item Code) — unikalus kodas
   - `Name` (UI: Name)
   - `Category` (UI: Category)
   - `Unit of Measure` (UI: Unit of Measure)
   - Ar valdoma pagal partijas (`Lot Tracking`, UI: Lot Tracking)
   - Ar valdoma pagal serijos numerius (`Serial Tracking`, UI: Serial Tracking)
4. Paspausk `Save` (UI: Save).

### Tiekėjų kūrimas

1. Eik į `Admin` → `Suppliers` (UI: Admin → Suppliers).
2. Paspausk `+ Create` (UI: Create).
3. Užpildyk pavadinimą, kontaktus.
4. Paspausk `Save` (UI: Save).

### Tiekėjo-prekės susiejimas

1. Eik į `Admin` → `Supplier Mappings` (UI: Admin → Supplier Mappings).
2. Sukurk susiejimą: tiekėjas → prekė → tiekėjo SKU kodas.

### Sandėlio vietų kūrimas

1. Eik į `Admin` → `Locations` (UI: Admin → Locations).
2. Paspausk `+ Create` (UI: Create).
3. Įvesk vietos kodą, zoną, tipą.
4. Paspausk `Save` (UI: Save).

### Matavimo vienetų konfigūracija

1. Eik į `Admin` → `Units of Measure` (UI: Admin → Units of Measure).
2. Paspausk `+ Create` (UI: Create).
3. Sukonfigūruok bazinį vienetą ir konversijų koeficientus.

### Sandėlio maketo redagavimas

1. Eik į `Admin` → `Layout Editor` (UI: Admin → Layout Editor).
2. Vizualiai redaguok sandėlio zonas ir aisles.

### Hrominis importas (masinis įkėlimas)

1. Eik į `Admin` → `Import Wizard` (UI: Admin → Import Wizard).
2. Parsisiųsk CSV šabloną.
3. Užpildyk duomenis ir įkelk failą (`Upload`, UI: Upload).
4. Stebėk importo būseną.

---

## Rezultatas (expected result)

- Duomenys prieinami visiems procesams iš karto po sukūrimo
- Prekių sąrašas matomas `Stock → Available Stock` (UI: Stock → Available Stock)

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas

---

## Susiję procesai

- [P-01 Prekių priėmimas](P-01-priemimas-inbound.md) — reikalauja sukonfigūruotų prekių ir tiekėjų
- [P-03 Išsiuntimas](P-03-israsymas-isuntimas-outbound.md) — reikalauja sukonfigūruotų klientų
- [P-14 Sistemos administravimas](P-14-sistemos-administravimas.md)
