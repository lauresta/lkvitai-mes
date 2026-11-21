---
module: mcp
title: Ką galima klausti – užsakymai, klientai, kainos, atsargos
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: ["SalesAdmin", "WarehouseManager", "WarehouseAdmin", "Operator", "QCInspector"]
status: veikia
---

# Ką galima klausti – užsakymai, klientai, kainos, atsargos

**Būsena:** veikia

## Kam skirta
AI asistentas (Cikada MCP) turi skaitymo įrankius, kurie visada įjungti ir nieko nekeičia. Šiame puslapyje kiekvienas įrankis aprašytas žmonių kalba su pavyzdiniais klausimais lietuviškai. Įrankių pavadinimų žinoti nereikia – asistentas pats pasirenka tinkamą.

## Prieš pradedant
Prisijunkite pagal [Prisijungimas](prisijungimas.md). Kurios teisės reikalingos, nurodyta prie kiekvieno įrankio.

## Užsakymai
### Užsakymų paieška (`orders_search`)
Ieško užsakymų (naujausi pirma) pagal numerį, klientą, adresą ar gaminio tekstą; galima filtruoti pagal būseną, saloną ir skolą. Grąžina numerį, datą, klientą, būseną, saloną, kainą, skolą (iki 100 užsakymų puslapyje).
- „Parodyk užsakymus su skola Kaune.“
- „Rask užsakymus, kuriuose minimas Petraitis.“
- „Kokie užsakymai VLN-1234?“

### Užsakymo informacija (`order_get`)
Vieno užsakymo visa informacija pagal numerį: antraštė, visos eilutės su matmenimis, spalva, kiekiu ir kaina, sumos (nustatyta, apskaičiuota, nuolaida, apmokėta, skola) ir priskirti darbuotojai.
- „Parodyk užsakymą VV-123 su visomis eilutėmis.“
- „Kiek už užsakymą VV-123 sumokėta ir kiek liko skolos?“

### Būsenų ir salonų sąrašas (`orders_filters`)
Parodo tikslius būsenų ir salonų pavadinimus, kuriuos galima naudoti filtre (pvz., „Įvestas“).
- „Kokios užsakymų būsenos ir salonai yra sistemoje?“

## Klientai ir kainos
### Klientų paieška ir kortelė (`customers_search`, `customer_get`)
Warehouse klientų kartoteka: kodas, pavadinimas, būsena, kainų grupė, adresai, mokėjimo sąlygos, kredito limitas. Reikia `SalesAdmin`, `WarehouseManager` arba `WarehouseAdmin`.
- „Rask klientą UAB Pavyzdys ir parodyk jo kredito limitą.“
- „Kokia šio kliento kainų grupė?“

### Kainų grupės (`price_groups_list`)
- „Išvardink kainų grupes.“

### Prekės ir kainos (`items_search`)
Prekė pagal SKU, pavadinimą ar #žymą: pardavimo kaina, pirkimo kaina, turimas ir prieinamas kiekis; nurodžius kainų grupę – ir tos grupės kaina. Reikia `Operator`, `QCInspector`, `WarehouseManager` arba `WarehouseAdmin`.
- „Kiek kainuoja prekė ABC-100 didmeniniam klientui ir kiek jos yra sandėlyje?“

### Atsargos pagal vietą (`stock_get`)
Atsargos kiekvienoje sandėlio vietoje: turima, rezervuota, prieinama. Duomenys gali atsilikti kelias sekundes.
- „Kur sandėlyje yra prekės ABC-100?“

## Pagalba
### Kas aš esu (`whoami`)
Parodo prisijungusį naudotoją ir jo roles; naudinga, kai gaunamas 403.
- „Kokias roles turiu?“

## Paruošti užklausų šablonai
Pokalbio kliente matysite paruoštas užklausas (ChatGPT / Claude rodo jas kaip šablonus): `production_status`, `debt_review`, `management_report`, `daily_orders_review`, `take_new_order` (pastarasis – tik įjungus užsakymų įvedimą). Aprašyti [vadovų ataskaitų puslapyje](ataskaitos.md) ir [užsakymų įvedimo puslapyje](uzsakymu-ivedimas.md). `daily_orders_review` („Dienos užsakymų apžvalga“): paprašo parinkti salono filtrą, sudaro lenteles „Nauji“, „Pradelsta skola“, „Reikia dėmesio“ ir skambučių sąrašą; nieko nekeičia.

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| 403 | Trūksta rolės | Paklauskite „Kokias roles turiu?“ ir kreipkitės į administratorių |
| Paieška pagal būseną ar saloną negrąžina užsakymų | Būsenos ar salono pavadinimas turi sutapti tiksliai (pvz., „Įvestas“) | Pirma paprašykite būsenų ir salonų sąrašo |
