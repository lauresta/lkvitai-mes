---
module: sales
title: Pardavimų modulis – apžvalga
routes: ["/sales/"]
roles: []
status: dalinai
---

# Pardavimų modulis – apžvalga

**Būsena:** dalinai

## Kam skirta
Pardavimų modulis (Sales, adresas `/sales`) skirtas pardavėjams, vadybininkams ir konsultantams: peržiūrėti užsakymų sąrašą, atidaryti vieno užsakymo informaciją, įvesti naują užsakymą ir jį papildyti gaminiais, priedais, adresu, atsakingais darbuotojais bei mokėjimais. Duomenys imami tiesiai iš senosios (Mango) SQL duomenų bazės, todėl tai, ką matote modulyje, sutampa su senąja sistema.

## Prieš pradedant
- Reikia prisijungimo prie Cikada portalo. Pardavimų modulis leidžia bet kurį prisijungusį naudotoją (konkrečios rolės pavadinimo kode nėra), bet paskyra turi būti susieta su senosios sistemos SQL prisijungimu. Jei susiejimo nėra, rodoma žinutė „Jūsų paskyra neturi galiojančio susiejimo su senosios sistemos SQL prieiga“ – kreipkitės į administratorių.
- Kokius salonus ir užsakymus matote, nustato senosios sistemos SQL teisės (matomi tik leidžiamų filialų užsakymai).
- Ataskaitų API (`/api/sales/reports`) pasiekiamas tik rolėms `SalesAdmin` arba `Reports`; šiame modulyje ekrano jam nėra – ataskaitas gaunama per dirbtinio intelekto asistentą (žr. [AI asistentas](../mcp/index.md)).

## Ekranų sąrašas
| Ekranas | Adresas | Būsena |
|---|---|---|
| [Užsakymų sąrašas](uzsakymu-sarasas.md) | `/sales/` | dalinai |
| [Užsakymo informacija](uzsakymo-informacija.md) | `/sales/orders/{Numeris}` | dalinai |
| [Naujo užsakymo kūrimas ir užsakymo kortelė](uzsakymo-ivedimas.md) | `/sales/entry/{UžsakymoId}` ir langas „Naujas užsakymas“ | dalinai |
| [Gaminiai ir priedai](uzsakymo-gaminiai.md) | užsakymo įvedimo dalis | dalinai |
| [Mokėjimai, gamybos patvirtinimas, anuliavimas](uzsakymo-mokejimai-patvirtinimas.md) | užsakymo įvedimo dalis | dalinai |
| [Nutrūkęs išsaugojimas ir atkūrimas](uzsakymo-atkurimas.md) | užsakymo įvedimo pranešimai | veikia |

## Svarbiausi apribojimai
- **Rašymas į duomenų bazę yra apribotas.** Užsakymo įvedimas veikia tik aplinkoje, kurioje įjungtas nustatymas „Enabled“, ir tik toms numerių serijoms, kurios nurodytos kaip leidžiamos (ekrane jos vadinamos „Testavimas“ serijomis). Kitų serijų užsakymus galima tik peržiūrėti; ekrane rodoma žinutė „Užsakymą galima tik peržiūrėti: šiuo metu įrašyti leidžiama tik Testavimas užsakymus“.
- Mokėjimų registravimas, gamybos patvirtinimas ir užsakymo anuliavimas turi atskirus jungiklius aplinkos nustatymuose; jei jungiklis išjungtas, mygtukas neaktyvus arba rodoma klaida.
- Mygtukai „Eksportuoti“, „Spausdinti“, „Rašyti klientui“, „Atidaryti POS“ ir datos filtras šiuo metu nieko nedaro (žr. kiekvieno ekrano skyrių „Dar neįgyvendinta“).

## Kaip modulis naudojamas kartu su kitais
- Banko pavedimus, kuriuos galima priskirti užsakymo mokėjimui, įkelia Finansų modulis: [Bankiniai pavedimai](../finance/bankiniai-pavedimai.md).
- Užsakymus galima skaityti ir (jei įjungta) įvesti per AI asistentą: [AI asistentas](../mcp/index.md).
