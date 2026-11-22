---
module: mcp
title: Sandėlis per AI asistentą – vietos, pirkimo dokumentai, perkėlimai
routes: ["https://cikada.lauresta.com/mcp", "https://cikada-test.lauresta.com/mcp"]
roles: ["Operator", "QCInspector", "WarehouseManager", "WarehouseAdmin", "InventoryAccountant", "CFO"]
status: dalinai
---

# Sandėlis per AI asistentą – vietos, pirkimo dokumentai, perkėlimai

**Būsena:** dalinai (skaitymo įrankiai veikia visada; rašymo įrankiai serveryje išjungti, kol administratorius jų neįjungia)

## Kam skirta
AI asistentas (Cikada MCP) atsako į sandėlio klausimus: kur guli prekė, kas guli vietoje, kiek kainuoja atsargos, kokie pirkimo dokumentai ir perkėlimai. Kai įjungti rašymo įrankiai, jis taip pat sukuria sandėlio vietą, įveda tiekėjo sąskaitą (pirkimo dokumentą), priima prekes ir perkelia atsargas. Asistentas veikia jūsų vardu ir su jūsų rolėmis.

## Prieš pradedant
- Skaitymui reikia sandėlio rolės (`Operator` ar aukštesnės). Atsargų savikainai (`item_cost`) – vadovo, apskaitos ar `CFO` rolės.
- Rašymui reikia serverio nustatymo `MCP_ENABLE_WAREHOUSE_ENTRY=true` (aplinkos kintamasis `CIKADA_MCP_WAREHOUSE_ENTRY`). Kol jis neįjungtas, asistentas pasako, kad sandėlio įvedimas šiame serveryje neįjungtas.
- Pirkimo dokumentams, priėmimui ir padėjimui į vietą reikia `QCInspector`, `WarehouseManager` arba `WarehouseAdmin`; vietos kūrimui ir perkėlimo tvirtinimui – `WarehouseManager` arba `WarehouseAdmin`.

## Ką galima klausti
| Klausimas | Įrankis |
|---|---|
| „Kur guli prekė RM-0001?“ | `stock_get` |
| „Kas guli vietoje A1-01-02?“ | `location_stock` |
| „Kokios vietos yra A1 stelaže?“ | `locations_search` |
| „Kiek kainuoja audinių atsargos?“ | `item_cost` |
| „Po kiek parduodame ir po kiek pirkome šią prekę?“ | `items_search` |
| „Parodyk šio tiekėjo pirkimo dokumentus“ | `suppliers_search`, `inbound_shipments_list`, `inbound_shipment_get` |
| „Kokie perkėlimai laukia?“ | `transfers_list`, `transfer_get` |

## Žingsniai
### 1. Sukurti sandėlio vietą
1. Parašykite, kokią vietą sukurti (kodą, sandėlį, jei reikia – zonos tipą ir talpą).
2. Asistentas suranda sandėlį (`warehouses_list`), parodo, ką įrašys, ir laukia aiškaus „Taip“.
3. Po patvirtinimo iškviečia `location_create`. Brūkšninis kodas pagal nutylėjimą lygus vietos kodui.

### 2. Įvesti tiekėjo sąskaitą
1. Pateikite sąskaitą (tekstą, lentelę, PDF ar nuotrauką) arba išvardinkite eilutes.
2. Asistentas suranda tiekėją (`suppliers_search`) ir kiekvieną prekę (`items_search`); numerių ir kainų neišgalvoja – klausia, kai neaišku.
3. Parodo visą dokumentą: tiekėją, sąskaitos numerį ir datą, eilutes, sumas. Patikrinkite ir parašykite „Taip“.
4. Asistentas iškviečia `inbound_shipment_create`. Atsargos dar nepasikeičia.

### 3. Priimti prekes ir padėti į vietą
1. Paprašykite priimti konkrečią eilutę ir kiekį. Asistentas perskaito dokumentą (`inbound_shipment_get`) ir po jūsų „Taip“ iškviečia `inbound_shipment_receive`. Prekės patenka į `RECEIVING` (arba `QC_HOLD`, jei prekei reikia kokybės kontrolės).
2. Paprašykite padėti prekes į vietą. Po „Taip“ asistentas iškviečia `putaway_move` (iš `RECEIVING` į fizinę vietą).
3. Kai sąskaitos sumos teisingos, paprašykite užregistruoti dokumentą apskaitoje: `inbound_shipment_post`. Šio veiksmo čia atšaukti negalima – klaida taisoma kreditiniu dokumentu.

### 4. Perkelti atsargas tarp vietų
1. Parašykite, ką, kiek, iš kur ir į kur perkelti.
2. Po „Taip“ asistentas sukuria perkėlimą (`transfer_create`); atsargos dar nejuda.
3. Tada jį vykdo per `transfer_advance`: pateikia (submit), jei reikia – tvirtina (approve), ir įvykdo (execute). Tik įvykdžius atsargos persikelia.

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| „There is no tool location_create.“ | Rašymo įrankiai serveryje išjungti | Kreipkitės į administratorių |
| „Warehouse API refused: your Cikada account does not have the role for this (403)“ | Trūksta rolės | Pasitikrinkite per `whoami` |
| „Location code '…' already exists.“ | Toks vietos kodas jau yra | Pasirinkite kitą kodą |
| „Putaway source location must be RECEIVING.“ | `putaway_move` perkelia tik iš `RECEIVING` | Kitoms vietoms naudokite perkėlimą |
| Perkėlimo vykdymas atmestas dėl atsargų | Šaltinio vietoje nepakanka laisvo kiekio | Patikrinkite `stock_get` ir pataisykite kiekį |

## Susiję
- [AI asistento apžvalga](index.md)
- [Ką galima klausti: užsakymai, klientai, kainos, atsargos](uzsakymai-klientai-atsargos.md)
