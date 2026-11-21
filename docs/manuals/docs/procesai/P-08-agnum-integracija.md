---
module: warehouse
title: Agnum integracija (suminė apskaita)
routes: ["/agnum/config", "/agnum/balances", "/agnum/reconcile"]
roles: ["InventoryAccountant", "WarehouseManager", "WarehouseAdmin"]
status: veikia
---

# Agnum integracija (suminė apskaita)

**Būsena:** veikia

## Kam skirta
Šis puslapis aprašo, kaip sandėlio modulis (Warehouse) dirba su apskaitos sistema Agnum dabar, ir paaiškina tris senus Agnum ekranus. Agnum atsargas laiko tik sumomis (suminė apskaita): prekių sąrašų, kiekių ir likučių tarp sistemų nebesikeičiama. Sandėlis yra vienintelis prekių, kainų, kiekių ir atsargų vertės šaltinis. Sprendimas aprašytas ADR-007.

## Prieš pradedant
- Tiekėjai kuriami Agnum; sandėlis juos nuskaito (UI: Refresh from Agnum), žr. [P-15](P-15-pagrindinis-duomenu-valdymas.md).
- Nustatymai (atsargų sąskaitos, įrašų taisyklės, siuntimas) – žr. [P-19](P-19-agnum-nustatymai-ir-eile.md).
- Prieiga prie Agnum ekranų: InventoryAccountant, WarehouseManager, WarehouseAdmin.

## Žingsniai
### 1. Kaip dokumentas patenka į Agnum
1. Sandėlyje įvedamas ir užregistruojamas pirkimo dokumentas: tiekėjas, sąskaitos numeris ir data, valiuta ir kursas, suma be PVM, PVM, suma su PVM, PVM režimas, Intrastat laukai ([P-16](P-16-pirkimo-dokumentas.md)).
2. Užregistravus dokumentas patenka į siuntimo eilę ir į Agnum nueina kaip jau apskaitytas dokumentas be prekių eilučių. Dokumento data Agnum yra tiekėjo sąskaitos data.
3. Eilė siunčia dokumentus kas minutę, jei siuntimas įjungtas ir dokumento data ne ankstesnė už pradžios datą. Būsenas ir veiksmus žr. [P-19](P-19-agnum-nustatymai-ir-eile.md).
4. Kito tiekėjo išlaidų sąskaitos ([P-18](P-18-papildomu-islaidu-saskaitos.md)) ir kreditinės sąskaitos ([P-17](P-17-tiekejo-kreditines-ir-grazinimai.md)) siunčiamos kaip atskiri dokumentai.
5. Mėnesio pabaigoje ([P-22](P-22-menesio-uzdarymas.md)) sandėlio ir Agnum sumos sulyginamos „tiltu“. Pradiniai likučiai ([P-20](P-20-pradiniai-likuciai.md)) į Agnum nesiunčiami; ES įsigijimų Intrastat duomenis sandėlis skaičiuoja pats ([P-21](P-21-intrastat-atvykimai.md)).

### 2. Ką daro buhalteris kasdien
1. Atidaro (UI: Finance > Agnum sending queue) ir patikrina, ar nėra eilučių (UI: Needs review) ar (UI: Unknown).
2. Sutvarkytą dokumentą grąžina (UI: Retry) arba, jei įvedė Agnum pats, pažymi (UI: Mark manual).
3. Peržiūri naktinio palyginimo skirtumus eilės apačioje.

## Dažnos klaidos
- Dokumentas „Held“ – siuntimas išjungtas arba data ankstesnė už pradžios datą (P-19, kortelė (UI: Sending)).
- Dokumento neužregistruoja – trūksta taisyklės sąskaitai ir PVM režimui (P-19) arba tiekėjas nesusietas su Agnum.
- Daugiau problemų – žr. [P-16](P-16-pirkimo-dokumentas.md) skyrių „Dažnos klaidos“.

## Dar neįgyvendinta
Seni Agnum ekranai dar yra meniu, bet bus pašalinti (blueprint etapas W9, laukia savininko patvirtinimo). Jų būsena: laikina. Jais naudotis nebereikia ir nerekomenduojama: Agnum tiekėjo API prekių ir likučių nebeteikia.
- (UI: Finance > Agnum Config), `/agnum/config`, (UI: Agnum Export Configuration) – senas likučių eksporto nustatymas: eksporto apimtis, cron tvarkaraštis, API adresas ir raktas, atitikmenys, eksporto istorija.
- (UI: Finance > Agnum Balances), `/agnum/balances`, (UI: Agnum Balances) – senas „virtualių“ Agnum likučių ekranas su paskirstymu į vietas.
- (UI: Finance > Agnum Reconcile), `/agnum/reconcile`, (UI: Agnum Reconciliation) – senas palyginimas pagal SKU.
Kodas dar registruoja senus kasdienius importo ir eksporto darbus; jie bus pašalinti kartu su ekranais.
Nauji ekranai: [P-19](P-19-agnum-nustatymai-ir-eile.md).

## Susiję procesai
- [P-16 Pirkimo dokumentas](P-16-pirkimo-dokumentas.md)
- [P-19 Agnum apskaitos nustatymai ir siuntimo eilė](P-19-agnum-nustatymai-ir-eile.md)
- [P-22 Mėnesio uždarymas](P-22-menesio-uzdarymas.md)
- [P-07 Atsargų vertinimas](P-07-atsargu-vertinimas-valuation.md)
