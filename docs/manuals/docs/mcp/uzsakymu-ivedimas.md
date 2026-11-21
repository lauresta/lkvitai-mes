---
module: mcp
title: Užsakymų įvedimas per AI asistentą (pagal nutylėjimą išjungta)
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: ["SalesAdmin"]
status: dalinai
---

# Užsakymų įvedimas per AI asistentą (pagal nutylėjimą išjungta)

**Būsena:** dalinai (įrankiai egzistuoja, bet serveryje išjungti, kol administratorius jų neįjungia)

## Kam skirta
AI asistentas (Cikada MCP) gali padėti pardavėjui įvesti naują užsakymą ir keisti esamą: pasirinkti klientą, seriją, gaminius, kainas, adresą ir sudaryti užsakymo santrauką. Šios funkcijos rašo į senosios sistemos duomenų bazę, todėl **pagal nutylėjimą išjungtos**.

## Prieš pradedant
- Serverio nustatymas `MCP_ENABLE_ORDER_ENTRY=true` (testinėje aplinkoje – aplinkos kintamasis `CIKADA_MCP_ORDER_ENTRY`). Kol jis neįjungtas, asistentas pokalbyje sako, kad užsakymų įvedimas šiame serveryje neįjungtas, o įrankių ir šablono `take_new_order` nėra.
- Pačiame Pardavimų modulyje turi būti įjungtas užsakymų įvedimas, o serija – leidžiama rašymui (žr. [Pardavimų modulį](../sales/uzsakymo-ivedimas.md)). Kitaip asistentas gauna tokias pačias klaidas kaip ekranas.
- Rolė `SalesAdmin` ir senosios sistemos SQL prisijungimas su teise rašyti (pagal `whoami` aprašą).

## Žingsniai
### 1. Pradėti užklausą
1. Pokalbio kliente pasirinkite šabloną „Take a new order“ (`take_new_order`) ir, jei norite, nurodykite klientą bei gaminius (pvz., „2 roletai 1200x1500, balti“), arba tiesiog papasakokite.
2. Asistentas nieko neišgalvoja: visus numerius ir kainas ima iš įrankių atsakymų ir klausia, kai kas neaišku.

### 2. Ką asistentas daro prieš įrašydamas
1. Suranda klientą ir seriją (`order_entry_catalog`), pasiūlo serijos numatytąsias reikšmes (salonas, vadybininkas, konsultantas) ir paklausia užsakymo numerio ir termino.
2. Kiekvienam gaminiui suranda tipą ir spalvą (su laisvu atsargų kiekiu), perskaito tipą (`order_entry_references`), paima kainoraščio kainą (`order_entry_unit_price`) ir paskaičiuoja peržiūrą (`order_entry_product_preview`; tik skaičiavimas).
3. Parodo santrauką: tipas, matmenys, kiekis, spalva, vieneto kaina, suma, atsargų įspėjimas ir antraštė.

### 3. Patvirtinti ir įrašyti
1. Peržiūrėkite santrauką ir parašykite aiškų „Taip“.
2. Tik tada asistentas iškviečia: `order_create` (antraštė), `order_product_add` ir `order_product_update` (kiekvienas gaminys), `order_accessory_set` (priedai), `order_address_set` (adresas, jei davėte), `order_final_price_set` (tik jei nurodėte galutinę sumą).
3. Pabaigoje parodo išsaugotą užsakymą (`order_entry_get`).
4. Į gamybą patvirtina (`order_confirm`) tik jei paprašėte.

### 4. Kiti galimi veiksmai
- `order_update` – keisti antraštę; `order_product_duplicate` – kopijuoti gaminį; `order_accessory_remove` – šalinti priedą; `order_entry_addresses` – ieškoti adreso.
- Naikinantys veiksmai reikalauja aiškaus jūsų „Taip“: `order_product_remove` (pašalinti gaminį), `order_confirm` (siunčia į gamybą, čia atšaukti negalima), `order_cancel` (anuliuoja visą užsakymą, tik jei užsakymas anuliuojamas).

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| „Order entry is not deployed on this environment yet.“ | Sales API be užsakymų įvedimo | Naudokite tik skaitymą |
| „the order changed since revision …: read it again with order_entry_get…“ | Užsakymas pasikeitė (kita sesija) | Asistentas perskaito iš naujo; patvirtinkite pakeitimą |
| „Užsakymą galima tik peržiūrėti…“ arba „Išsaugoti galima tik sukonfigūruotose Testavimas serijose.“ | Serija neleidžiama rašymui | Naudokite leidžiamą seriją |
| Pakartotas veiksmas „replayed“ | Ta pati komanda jau buvo pritaikyta | Nieko nedarykite – dubliavimosi nėra |

## Dar neįgyvendinta
- Pagal nutylėjimą išjungta (serverio jungiklis).
- Kiekviena rašanti komanda gauna `commandId`, todėl pakartojus ta pati operacija nedubliuojama; kiekvienas rašymas reikalauja dabartinės užsakymo versijos (revision).
- Mokėjimų įrašymo per asistentą nėra; mokėjimus galima įrašyti tik [Pardavimų modulio ekrane](../sales/uzsakymo-mokejimai-patvirtinimas.md).
