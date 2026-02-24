# LKvitai MES — Naudotojo gidas

Sveiki atvykę į **LKvitai.MES** sandėlio valdymo sistemos naudotojo vadovą.

Ši sistema skirta sandėlio operacijų valdymui: prekių priėmimui, sandėliavimui, išsiuntimui, inventorizacijai ir ataskaitoms.

> **Kalba:** Visi vadovai pateikiami lietuvių kalba. UI mygtukų ir meniu pavadinimai nurodomi angliškai skliausteliuose, pvz. `(UI: Create)`.

---

## Procesų turinys (Oglavlenie)

Žemiau pateikiamas visų sistemos procesų sąrašas. Procesai numeruojami `P-01..P-15` ir atitinka sistemos veikimo sritis.

### Įeinančios operacijos (Inbound)

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-01](procesai/P-01-priemimas-inbound.md) | **Prekių priėmimas** | Tiekėjų siuntų priėmimas, kokybės patikrinimas, atsargų užregistravimas sistemoje |
| [P-02](procesai/P-02-padeti-i-lokacija-putaway.md) | **Padėjimas į vietą (Putaway)** | Priimtų prekių išdėstymas į sandėlio vietas po priėmimo |

### Išeinančios operacijos (Outbound)

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-03](procesai/P-03-israsymas-isuntimas-outbound.md) | **Išsiuntimas** | Pardavimo orderių vykdymas: rezervacija, krovimas, pakavimas ir išsiuntimas |
| [P-09](procesai/P-09-grazinimai-rma.md) | **Grąžinimai (RMA)** | Kliento grąžinamų prekių priėmimas, patikrinimas ir pakartotinis sandėliavimas arba nurašymas |
| [P-10](procesai/P-10-krzyminis-perkrovimas-crossdock.md) | **Kryžminis perkrovimas (Cross-Dock)** | Tiesioginis prekių pervežimas iš priėmimo dokso į išsiuntimo dokso be sandėliavimo |

### Sandėlio operacijos

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-04](procesai/P-04-perkelimas-transfer.md) | **Perkėlimas (Transfer)** | Atsargų perkėlimas tarp sandėlio vietų ar zonų |
| [P-05](procesai/P-05-inventorizacija-cycle-count.md) | **Inventorizacija (Cycle Count)** | Periodinė fizinių atsargų patikra ir neatitikimų taisymas |
| [P-06](procesai/P-06-atsargu-koregavimas-adjustments.md) | **Atsargų koregavimas** | Rankinis kiekių koregavimas dėl žalos, nuostolių ar radimų |

### Finansai

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-07](procesai/P-07-atsargu-vertinimas-valuation.md) | **Atsargų vertinimas (Valuation)** | Atsargų finansinis vertinimas: savikainos keitimas, papildomų kaštų priskyrimas, nurašymai |
| [P-08](procesai/P-08-agnum-integracija.md) | **Agnum integracija** | Kasdieninis atsargų vertės eksportas į Agnum apskaitos sistemą ir derinimas |

### Atsekamumo ir atitikties valdymas

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-11](procesai/P-11-partiju-seriju-sekimas.md) | **Partijų ir serijų sekimas** | Partijų (lot) ir serijos numerių sekimas per visą tiekimo grandinę |

### Sandėlio vizualizacija

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-12](procesai/P-12-sandelio-zemelapis-visualization.md) | **Sandėlio žemėlapis** | 2D/3D sandėlio žemėlapio peržiūra, vietų turinio paieška |

### Ataskaitos ir analizė

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-13](procesai/P-13-ataskaitos-analize-reports.md) | **Ataskaitos ir analizė** | Atsargų, judėjimų, priėmimo, išsiuntimų istorijos ir KPI ataskaitos |

### Administravimas

| Nr. | Procesas | Aprašymas |
|-----|---------|-----------|
| [P-14](procesai/P-14-sistemos-administravimas.md) | **Sistemos administravimas** | Vartotojų valdymas, API raktai, GDPR, atsarginės kopijos, auditai |
| [P-15](procesai/P-15-pagrindinis-duomenu-valdymas.md) | **Pagrindinių duomenų valdymas** | Prekių, tiekėjų, vietų, kategorijų, matavimo vienetų kūrimas ir priežiūra |

---

## Rolės

Prieš pradedant, pasitikrink savo rolę sistemoje:

- [Sandėlio operatorius](roles/sandelio-operatorius.md)
- [Kokybės inspektorius](roles/kokybes-inspektorius.md)
- [Gamybos vadovas](roles/gamybos-vadovas.md)

---

## Pagalba

- [Trikčių šalinimas](trikciu-salinimas/index.md)
- [Dažnos klaidos](trikciu-salinimas/daznos-klaidos.md)

Test CI trigger: 2026-02-24.

CI retrigger note: 2026-02-24.
