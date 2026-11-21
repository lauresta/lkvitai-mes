---
module: mcp
title: Vadovų ataskaitos ir sąrašai per AI asistentą
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: ["SalesAdmin", "Reports"]
status: veikia
---

# Vadovų ataskaitos ir sąrašai per AI asistentą

**Būsena:** veikia

## Kam skirta
AI asistentas (Cikada MCP) gali pateikti gyvas vadovų ataskaitas iš senosios (Mango) sistemos: gamybą, darbų sukauptas sumas, gautus mokėjimus ir einamuosius sąrašus (gamybos eilė, skolos, sustabdyti gaminiai). Duomenys skaičiuojami pagal senosios sistemos taisykles; istorija nuo 2020 m. Asistentas paprastai pats nubraižo diagramą iš lentelės.

## Prieš pradedant
- Cikada rolė `SalesAdmin` arba `Reports` ir senosios sistemos SQL prisijungimas, kuriam leidžiama skaityti šias ataskaitas. Trūkstant teisės asistentas atsako: „Reports need the Cikada role SalesAdmin or Reports, and a legacy SQL login allowed to read them.“
- Prisijungta pagal [Prisijungimas](prisijungimas.md).

## Ataskaitos
### Gamyba (`report_production`)
Kiek gaminių pasiekė būseną laikotarpiu (kiekvienas gaminys skaičiuojamas, kai pirmą kartą pasiekia būseną). Grupuoti galima pagal tipą, vienetą, seriją, būseną, laikotarpį (dieną, mėnesį, metus). „Kiek“ skaičiuojama tik „(vnt)“ tipais – „(kvm)“ ir „(plotis)“ tipų nesumuokite.
- „Kiek roletų pagaminta praėjusį mėnesį?“
- „Kiek vidutiniškai per darbo dieną gaminame apsauginių?“

### Darbai (`report_work`)
Sukauptos sumos (EUR) pagal darbuotoją ir pareigas (Vadybininkas, Konsultantas, Matuotojas, Montuotojas, Transportas). Tai asmeniniai atlyginimo duomenys: rodykite tik tam, kas paprašė.
- „Kiek darbų sukaupta pagal pareigas spalį?“

### Gauti mokėjimai (`report_payments`)
Gauti pinigai pagal mokėjimo būdą (kasa, ppk, bankas) ir filialą (Vilnius, Kaunas, Šiauliai, Klaipėda, Klaipėda mažmena, Ryga) pagal mokėjimo datą. Tai tik sumos; atskiriems pavedimams naudokite [bankinių pavedimų sąrašą](bankas.md).
- „Kiek pinigų gauta pagal filialus ir mokėjimo būdus šį mėnesį?“

### Tikslios filtrų reikšmės (`report_dimensions`)
Parodo galimus tipus, serijas, būsenas, pareigas, filialus.

## Einamieji sąrašai
| Klausimas | Įrankis |
|---|---|
| „Kas dabar gamyboje ir kiek laukia?“ | `production_queue` (būsena × tipas, dienos būsenoje; naudinga ribota „maxOrderAgeDays“, pvz. 180) |
| „Kokios medžiagos populiariausios?“ | `materials_popularity` |
| „Kokie gaminiai sustabdyti ir kodėl?“ | `stopped_products` (senojoje sistemoje sustabdymo priežastis nesaugoma – tik pastabos) |
| „Kas skolingas?“ | `unpaid_orders` (skola, dienos nuo išdavimo, „pradelsta“ pagal senąją taisyklę: didmena po 30 d., mažmena po 15 d.) |
| „Kas pagaminta, bet neišduota ar nesumontuota?“ | `made_not_delivered` |
| „Didžiausi skolininkai?“ | `customer_debt` (mažmena laikoma vienu klientu kiekviename filiale) |
| „Užsakymai su nurodytu gaminiu ar medžiaga per laikotarpį?“ | `orders_find` |

## Paruošti šablonai
- `production_status` („Gamybos būklė šiandien“; pasirinktinai filialas): gamybos eilė, pagaminta per 14 dienų, sustabdyti, pagaminti bet neišduoti, ką spręsti šiandien.
- `debt_review` („Skolų apžvalga“): didžiausi skolininkai, pradelsti išduoti užsakymai pagal filialą, diagrama.
- `management_report` („Vadovų ataskaita (mėnuo)“; pasirinktinai mėnuo, pvz. 2026-10): gamyba, mokėjimai, darbai, palyginimas su ankstesniu mėnesiu ir pernai, diagramos, trys išvados.

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| „Reports need the Cikada role SalesAdmin or Reports…“ | Trūksta rolės arba SQL teisės | Kreipkitės į administratorių |
| „TRUNCATED at 1000“ | Sąrašas ilgesnis nei 1000 eilučių | Siaurinkite filtrus |
| Ataskaita tuščia | Filtras nesutampa su tikslia reikšme | Paprašykite „report_dimensions“ reikšmių |
