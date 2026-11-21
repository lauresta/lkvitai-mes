---
module: warehouse
title: Sandėlio operatorius
routes: []
roles: ["Operator", "PackingOperator", "DispatchClerk"]
status: veikia
---

# Sandėlio operatorius

**Būsena:** veikia

## Aprašymas
Sandėlio modulyje (Warehouse) rolė `Operator` skirta kasdieniams sandėlio darbams: atsargų peržiūrai, perkėlimams, inventorizacijai, komplektavimo užduočių vykdymui ir ataskaitoms. Meniu rodo visiems vienodą sąrašą, bet ekranų veiksmus riboja rolės: jei trūksta teisių, veiksmas grąžina prieigos klaidą.

## Ką gali ir ko negali
- Gali: peržiūrėti atsargas ir ataskaitas, siuntų sąrašą, rezervacijas; kurti, siųsti tvirtinimui ir vykdyti perkėlimus; vykdyti inventorizaciją; vykdyti ir baigti komplektavimo užduotis; dirbti su etiketėmis ir kryžminiu perkrovimu; registruoti grąžinimus (priimti grąžintas prekes).
- Negali: priimti prekių pagal siuntą, atlikti kokybės patikrą ir padėti prekių į vietas (tam reikia rolės QCInspector, WarehouseManager ar WarehouseAdmin); koreguoti atsargų; tvirtinti perkėlimų; kurti pardavimo užsakymų; administruoti sistemą.

## Pagrindiniai meniu punktai
- (UI: Stock > Available Stock), (UI: Stock > Stock Dashboard), (UI: Stock > Location Balance), (UI: Stock > Reservations).
- (UI: Inbound > Inbound Shipments) – tik peržiūra.
- (UI: Outbound > Picking Tasks), (UI: Outbound > Wave Picking), (UI: Outbound > Labels), (UI: Outbound > Cross-Dock).
- (UI: Operations > Transfers), (UI: Operations > Cycle Counts), (UI: Operations > Warehouse Map).
- (UI: Reports > Stock Level) ir kitos istorijos ataskaitos.

## Kitos operatyvinės rolės
- `PackingOperator` – pakuoja užsakymus ekrane (UI: Outbound > Outbound Orders) > (UI: Pack), žr. [P-03](../procesai/P-03-israsymas-isuntimas-outbound.md).
- `DispatchClerk` – patvirtina išsiuntimą ekrane (UI: Outbound > Dispatch), žr. P-03. Pastaba: šioms rolėms užsakymų ir siuntų skaitymo teisė API pusėje yra Operator lygio, todėl jos dažniausiai turi turėti ir Operator.
- `SalesAdmin` – pardavimo užsakymai (UI: Outbound > Sales Orders).

## Susiję procesai
- [P-01 Prekių priėmimas](../procesai/P-01-priemimas-inbound.md)
- [P-02 Padėjimas į vietą](../procesai/P-02-padeti-i-lokacija-putaway.md)
- [P-03 Išsiuntimas](../procesai/P-03-israsymas-isuntimas-outbound.md)
- [P-04 Perkėlimas](../procesai/P-04-perkelimas-transfer.md)
- [P-05 Inventorizacija](../procesai/P-05-inventorizacija-cycle-count.md)
- [P-26 Bangos ir komplektavimas](../procesai/P-26-bangos-ir-komplektavimas.md)
- [P-24 Atsargų peržiūra](../procesai/P-24-atsargu-perziura.md)
