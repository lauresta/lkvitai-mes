---
module: warehouse
title: Kokybės inspektorius
routes: []
roles: ["QCInspector"]
status: veikia
---

# Kokybės inspektorius

**Būsena:** veikia

## Aprašymas
Sandėlio modulyje (Warehouse) rolė `QCInspector` priima atvežtas prekes, tikrina jų kokybę (UI: Pass) / (UI: Fail), tvarko karantino prekes, padeda prekes į vietas ir gali registruoti pirkimo dokumentus bei kreditines sąskaitas. Partijų kūrimo ekrano nėra: partijos atsiranda priimant prekes su (UI: Lot Number).

## Ką gali ir ko negali
- Gali: kurti ir taisyti pirkimo dokumentus, priimti prekes, užregistruoti dokumentą (UI: Post) ir kreditinę sąskaitą, uždaryti siuntą su trūkumu (UI: Close with shortage), atlikti kokybės patikrą ir tvarkyti karantiną, padėti prekes į vietas, priimti ir grąžinti į atsargas grąžintas prekes (RMA), kurti papildomų išlaidų sąskaitas.
- Negali: įvesti rankinio valiutos kurso ir panaikinti dokumento užregistravimo (tai daro InventoryAccountant, WarehouseManager, WarehouseAdmin); koreguoti atsargų; administruoti sistemą.

## Pagrindiniai meniu punktai
- (UI: Inbound > Inbound Shipments), (UI: Inbound > Receiving QC), (UI: Inbound > Putaway).
- (UI: Finance > Additional cost invoices) – kito tiekėjo išlaidų sąskaitos.
- (UI: Reports > Traceability) – atsekamumo ataskaita.
- (UI: Admin > Lots) – partijų sąrašas (tik peržiūra; Admin skiltis rodoma meniu visiems, bet rašymo teisių nereikia).

## Susiję procesai
- [P-01 Prekių priėmimas ir kokybės patikra](../procesai/P-01-priemimas-inbound.md)
- [P-16 Pirkimo dokumentas](../procesai/P-16-pirkimo-dokumentas.md)
- [P-17 Tiekėjo kreditinės sąskaitos](../procesai/P-17-tiekejo-kreditines-ir-grazinimai.md)
- [P-18 Papildomų išlaidų sąskaitos](../procesai/P-18-papildomu-islaidu-saskaitos.md)
- [P-09 Grąžinimai (RMA)](../procesai/P-09-grazinimai-rma.md)
- [P-11 Partijų ir serijų numeriai](../procesai/P-11-partiju-seriju-sekimas.md)
