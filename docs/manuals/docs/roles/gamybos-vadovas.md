---
module: warehouse
title: Sandėlio vadovas ir buhalteris
routes: []
roles: ["WarehouseManager", "WarehouseAdmin", "InventoryAccountant", "CFO", "Auditor"]
status: veikia
---

# Sandėlio vadovas ir buhalteris

**Būsena:** veikia

## Aprašymas
Sandėlio modulyje (Warehouse) rolė `WarehouseManager` planuoja ir tvirtina operacijas, koreguoja atsargas ir dirba su finansų ekranais; rolė `WarehouseAdmin` turi visas teises ir vienintelė valdo administravimo ekranus. Rolė `InventoryAccountant` (buhalteris) turi teises tik į finansų ekranus ir su jais susijusius veiksmus. Šioje rolės apraše kartu aprašytos ir rolės `CFO` bei `Auditor`.

## Ką gali ir ko negali
- Sandėlio vadovas: visos operatoriaus ir kokybės inspektoriaus funkcijos; atsargų koregavimas (UI: Stock > Adjustments); perkėlimų tvirtinimas; pardavimo užsakymų tvirtinimas ir perdavimas komplektuoti; bangų kūrimas ir komplektuotojų priskyrimas; atsekamumo ir atitikties ataskaitos; kreditinių ir pirkimo dokumentų registravimas, rankinis kursas, užregistravimo panaikinimas; viso mėnesio uždarymas.
- Buhalteris (InventoryAccountant): Agnum nustatymai ir siuntimo eilė, nukainojimas, pradiniai likučiai, Intrastat, mėnesio uždarymas, tiekėjų atnaujinimas iš Agnum, rankinis valiutos kursas ir užregistravimo panaikinimas.
- CFO: patvirtina inventorizacijos korekcijas, viršijančias 1000.
- Auditor: žiūri atitikties skyrių (UI: Reports > Lot Traceability), (UI: Reports > Compliance Dashboard).
- WarehouseAdmin: viskas, įskaitant (UI: Admin) skiltį – vartotojus, roles, API raktus, nustatymus. Žr. [P-14](../procesai/P-14-sistemos-administravimas.md) ir [P-30](../procesai/P-30-vartotojai-roles-api-raktai.md).

## Pagrindiniai meniu punktai
- Visa (UI: Stock), (UI: Inbound), (UI: Outbound), (UI: Operations) sritis.
- Finansų skiltis (UI: Finance): (UI: Valuation), (UI: Opening balances), (UI: Intrastat arrivals), (UI: Month end), (UI: Agnum Accounting), (UI: Agnum sending queue), (UI: Additional cost invoices). Meniu taip pat rodo senus (UI: Agnum Config), (UI: Agnum Balances), (UI: Agnum Reconcile) – jie bus pašalinti.
- (UI: Reports) ir (UI: Analytics).

## Susiję procesai
- [P-04 Perkėlimas](../procesai/P-04-perkelimas-transfer.md) – tvirtina perkėlimus.
- [P-05 Inventorizacija](../procesai/P-05-inventorizacija-cycle-count.md) – planuoja ir tvirtina.
- [P-06 Atsargų koregavimas](../procesai/P-06-atsargu-koregavimas-adjustments.md) – koreguoja atsargas.
- [P-07 Atsargų vertinimas](../procesai/P-07-atsargu-vertinimas-valuation.md) – nukainojimas.
- [P-08 Agnum integracija](../procesai/P-08-agnum-integracija.md), [P-19 Agnum nustatymai ir siuntimo eilė](../procesai/P-19-agnum-nustatymai-ir-eile.md)
- [P-16 Pirkimo dokumentas](../procesai/P-16-pirkimo-dokumentas.md), [P-17](../procesai/P-17-tiekejo-kreditines-ir-grazinimai.md), [P-18](../procesai/P-18-papildomu-islaidu-saskaitos.md)
- [P-20 Pradiniai likučiai](../procesai/P-20-pradiniai-likuciai.md), [P-21 Intrastat](../procesai/P-21-intrastat-atvykimai.md), [P-22 Mėnesio uždarymas](../procesai/P-22-menesio-uzdarymas.md)
- [P-13 Ataskaitos](../procesai/P-13-ataskaitos-analize-reports.md), [P-28 Atsekamumas ir atitiktis](../procesai/P-28-atsekamumas-ir-atitiktis.md)
