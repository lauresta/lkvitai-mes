---
module: warehouse
title: Intrastat atvykimų ataskaita
routes: ["/valuation/intrastat"]
roles: ["InventoryAccountant", "WarehouseManager", "WarehouseAdmin"]
status: veikia
---

# Intrastat atvykimų ataskaita

**Būsena:** veikia

## Kam skirta
Sandėlio modulio ekranas (UI: Intrastat arrivals) skirtas parengti mėnesio Intrastat atvykimų duomenis: ES įsigijimai pagal sąskaitos datą, sugrupuoti pagal prekės kodą ir išsiuntimo šalį su verte EUR, neto mase ir kiekiu. Duomenis sandėlis skaičiuoja pats iš pirkimo dokumentų, o ne iš Agnum. Ataskaitą galima parsisiųsti CSV failu.

## Prieš pradedant
- Prieiga: InventoryAccountant, WarehouseManager, WarehouseAdmin.
- Pirkimo dokumentas turi būti su PVM režimu (UI: EU acquisition) (arba tiekėjas ES tipo) ir užpildytais Intrastat laukais, žr. [P-16](P-16-pirkimo-dokumentas.md).
- Kiekviena atsargų prekė turi turėti 8 skaitmenų prekės kodą (UI: Commodity code (CN)) ir neto svorį vienetui (UI: Net weight per unit, kg) – (UI: Admin > Items) > (UI: Edit), žr. [P-32](P-32-prekiu-pagrindiniai-duomenys.md). Be jų ES įsigijimo užregistruoti neleidžiama.

## Žingsniai
### 1. Parodyti mėnesio ataskaitą
1. Meniu pasirinkite (UI: Finance > Intrastat arrivals). Ataskaita atsidaro iškart einamam mėnesiui.
2. Lauke (UI: Month) pasirinkite mėnesį ir spauskite (UI: Show).
3. Lentelėje matote (UI: Commodity code), (UI: Dispatch country), (UI: Value, EUR), (UI: Net mass, kg), (UI: Quantity) ir eilutę (UI: Total). Kiekis pateikiamas prekės bazės vienetais.
4. Jei kai kurios eilutės praleistos (trūksta prekės kodo, šalies ar vertės), virš lentelės rodoma geltona žinutė „N lines are left out: commodity code, dispatch country or value is missing.“ ir išvardytos praleistos eilutės. Papildykite duomenis ir paspauskite (UI: Show) dar kartą.

![Intrastat atvykimų ataskaita](../img/warehouse/intrastat-arrivals.png)

### 2. Parsisiųsti CSV
Spauskite (UI: Download CSV). Failas vadinasi `intrastat-arrivals-YYYY-MM.csv`.

## Dažnos klaidos
- „No EU acquisitions in this month.“ – mėnesį pagal sąskaitos datą nėra ES įsigijimų.
- Pranešimas apie praleistas eilutes – papildykite prekės kortelę (CN kodas, svoris) arba dokumento (UI: Country of dispatch).
- Rodoma tik sąskaitos tipo dokumentų: kreditinės sąskaitos į šią ataskaitą neįtraukiamos.

## Dar neįgyvendinta
Papildomas Intrastat vienetas (papildoma matavimo vienetų kolona) neįgyvendintas – rodomas tik kiekis bazės vienetais.

## Susiję procesai
- [P-16 Pirkimo dokumentas](P-16-pirkimo-dokumentas.md)
- [P-32 Prekių pagrindiniai duomenys](P-32-prekiu-pagrindiniai-duomenys.md)
- [P-22 Mėnesio uždarymas](P-22-menesio-uzdarymas.md)
