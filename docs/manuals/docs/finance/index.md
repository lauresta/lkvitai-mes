---
module: finance
title: Finansų modulis – apžvalga
routes: ["/finance/", "/finance/bank-payments"]
roles: []
status: dalinai
---

# Finansų modulis – apžvalga

**Būsena:** dalinai

## Kam skirta
Finansų modulis (Finance, adresas `/finance`) šiuo metu turi vieną veikiantį ekraną – [Bankiniai pavedimai](bankiniai-pavedimai.md): senosios sistemos banko pavedimų lentelės peržiūra ir naujų pavedimų įkėlimas iš Excel. Šiuos pavedimus vėliau galima priskirti užsakymų mokėjimams [Pardavimų modulyje](../sales/uzsakymo-mokejimai-patvirtinimas.md).

## Prieš pradedant
- Reikia prisijungimo prie Cikada portalo ir paskyros, susietos su senosios sistemos SQL prisijungimu, kurio vardas įtrauktas į Finansų prieigos sąrašą (nustatymas `Finance:AllowedLegacyLogins`). Rolės pavadinimo kode nėra – prieiga nustatoma būtent pagal šį sąrašą. Be prieigos rodoma: „Šiai paskyrai Finansų prieiga nesuteikta.“
- Visi veiksmai vykdomi su jūsų pačių senosios sistemos SQL teisėmis.

## Ekranai
| Ekranas | Adresas | Būsena |
|---|---|---|
| [Bankiniai pavedimai](bankiniai-pavedimai.md) | `/finance/` ir `/finance/bank-payments` (tas pats ekranas) | veikia |
| [Pavedimų įkėlimas iš Excel](bankiniai-pavedimai-ikelimas.md) | to paties ekrano dalis | veikia |

## Dar neįgyvendinta
Kitų finansų ekranų nėra. Adresas `/finance/` atidaro tą patį banko pavedimų ekraną. Programos resursuose yra užuomina „Finansų modulis ruošiamas. Finansiniai duomenys ir veiksmai dar neprijungti“, tačiau ji nerodoma jokiame ekrane.

Pavedimų įkėlimą ir peržiūrą galima atlikti ir per dirbtinio intelekto asistentą (ekrano nuotrauka, PDF ar Excel): žr. [AI asistentas – banko išrašų įkėlimas](../mcp/bankas.md).
