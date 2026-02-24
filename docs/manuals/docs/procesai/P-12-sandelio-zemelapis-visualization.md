# P-12 — Sandėlio žemėlapis ir vietų paieška (Warehouse Visualization)

**Proceso numeris:** P-12
**Atnaujinta:** 2026-02-24
**Versija:** 1.0 (šablonas)

---

## Pavadinimas

2D/3D sandėlio žemėlapio peržiūra ir vietų turinio paieška (Warehouse Visualization & Location Discovery)

---

## Tikslas

Vizualiai peržiūrėti sandėlio planą, rasti konkrečios prekės vietą, patikrinti sandėliavimo vietos turinį.

---

## Kam skirta (rolės)

| Rolė | Veiksmai |
|------|---------|
| Sandėlio operatorius | Ieško prekių vietų vizualiai |
| Sandėlio vadovas | Peržiūri bendrą sandėlio išnaudojimą |

---

## Prieš pradedant (preconditions)

- Sandėlio maketas sukonfigūruotas sistemoje (P-15 / P-14 — Layout Editor)

---

## Žingsniai

### Atidaryti sandėlio žemėlapį

1. Eik į `Operations` → `Warehouse Map` (UI: Operations → Warehouse Map).
2. Sistema rodo 3D vaizdą pagal nutylėjimą.
3. Jei nori 2D vaizdą — pasirink `2D View` (UI: 2D View).

### Rasti prekę

1. Žemėlapyje naudok paieškos lauką (`Search`, UI: Search).
2. Įvesk prekės pavadinimą ar SKU kodą.
3. Sistema paryškina vietas, kur ta prekė yra.

### Peržiūrėti vietos turinį

1. Paspausk ant konkrečios vietos (bin) žemėlapyje.
2. Sistema rodo: kokios prekės, koks kiekis, kokia rezervacija.

### Peržiūrėti vietos detales

1. Paspausk `View Details` (UI: View Details) arba perkelk adresą į `/warehouse/locations/{Id}`.
2. Matai išsamią informaciją: HU sąrašas, atsargų balansas.

---

## Rezultatas (expected result)

- Vizualiai matomas sandėlio išdėstymas
- Randama prekės vieta be fizinio sandėlio apžiūros

---

## Pastabos / ekrano nuotraukos

> TODO: pridėk ekrano nuotraukas (3D vaizdo pavyzdys)

---

## Susiję procesai

- [P-02 Padėjimas į vietą](P-02-padeti-i-lokacija-putaway.md)
- [P-04 Perkėlimas](P-04-perkelimas-transfer.md)
- [P-15 Pagrindinių duomenų valdymas](P-15-pagrindinis-duomenu-valdymas.md) — maketo konfigūracija
