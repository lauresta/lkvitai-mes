---
module: mcp
title: AI asistentas (Cikada MCP) – apžvalga
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: ["SalesAdmin", "Reports", "WarehouseManager", "WarehouseAdmin", "Operator", "QCInspector"]
status: veikia
---

# AI asistentas (Cikada MCP) – apžvalga

**Būsena:** veikia (užsakymų įvedimo įrankiai pagal nutylėjimą išjungti)

## Kam skirta
AI asistentas „Cikada MCP“ leidžia ChatGPT arba Claude pokalbyje užduoti klausimus apie Cikada.MES duomenis lietuviškai: rasti užsakymus, pažiūrėti skolas, kainas ir atsargas, gauti vadovų ataskaitas, perskaityti banko pavedimus ir (jei įjungta) įvesti užsakymus. Asistentas veikia kaip jūs: kiekvienas veiksmas atliekamas su jūsų paskyros rolėmis ir senosios sistemos teisėmis.

## Prieš pradedant
- Reikia Lauresta SSO paskyros (`sso.lauresta.com`) ir Cikada rolių (žr. [Kam kokios teisės reikia](#kam-kokios-teises-reikia)).
- ChatGPT arba Claude paskyros, kurioje leidžiama jungti savo MCP serverį. Prisijungimo žingsniai: [Prisijungimas](prisijungimas.md).
- Adresas: darbinėje aplinkoje `https://cikada.lauresta.com/mcp`, testinėje – `https://cikada-test.lauresta.com/mcp`.

## Puslapiai
| Puslapis | Apie ką |
|---|---|
| [Prisijungimas iš ChatGPT ir Claude](prisijungimas.md) | Kaip prijungti asistentą |
| [Ką galima klausti: užsakymai, klientai, kainos, atsargos](uzsakymai-klientai-atsargos.md) | Skaitymo įrankiai su pavyzdžiais |
| [Vadovų ataskaitos ir sąrašai](ataskaitos.md) | Gamyba, darbai, mokėjimai, skolos |
| [Banko pavedimai ir išrašų įkėlimas](bankas.md) | Peržiūra ir importas |
| [Užsakymų įvedimas per asistentą](uzsakymu-ivedimas.md) | Pagal nutylėjimą išjungta |
| [Sandėlis per asistentą](sandelis.md) | Vietos, atsargų savikaina, pirkimo dokumentai, perkėlimai |

## Kam kokios teisės reikia
Asistentas naudoja jūsų tapatybę, todėl jis negali daugiau nei jūs ekrane. Pagal kodą:

| Sritis | Reikalinga teisė |
|---|---|
| Užsakymai (paieška, informacija), įrankis whoami | Prisijungęs naudotojas, susietas su senosios sistemos SQL prisijungimu. Įrankio `whoami` aprašas teigia, kad užsakymams ir užsakymų įvedimui reikia rolės `SalesAdmin` – tai neįrašyta Sales API kode, todėl nesėkmės atveju pasitikrinkite per `whoami`. |
| Klientai (Warehouse klientų kartoteka) | `SalesAdmin`, `WarehouseManager` arba `WarehouseAdmin` |
| Prekės, kainos, atsargos | `Operator`, `QCInspector`, `WarehouseManager` arba `WarehouseAdmin` |
| Vadovų ataskaitos ir sąrašai (gamyba, darbai, mokėjimai, skolos ir kt.) | `SalesAdmin` arba `Reports` ir jūsų senosios sistemos SQL teisės |
| Banko pavedimai (sąrašas, peržiūra, importas) | Finansų prieiga: jūsų SQL prisijungimas įtrauktas į leidžiamų sąrašą; rolės nėra |
| Užsakymų įvedimas | Įjungtas serverio jungiklis ir senosios sistemos SQL teisės įrašyti |
| Sandėlio įvedimas (vietos, pirkimo dokumentai, priėmimas, perkėlimai) | Įjungtas serverio jungiklis; `QCInspector`, `WarehouseManager` arba `WarehouseAdmin` (vietos kūrimui – `WarehouseManager` arba `WarehouseAdmin`) |
| Instrukcijų ekranų paveikslėliai (`docs_image`) | Bet kuris prisijungęs naudotojas |

Jei asistentas atsako 403 – trūksta rolės. Paprašykite asistento: „Parodyk, kas aš esu“ (įrankis `whoami`): jis išvardija jūsų roles.

## Dar neįgyvendinta
- Užsakymų įvedimo įrankiai išjungti pagal nutylėjimą (žr. [atskirą puslapį](uzsakymu-ivedimas.md)).
- Asistentas negali skaityti pridėtų failų pats: banko išrašą perskaito ChatGPT / Claude ir perduoda eilutes asistentui.
- Gamybos (ne testinės) aplinkos adresas kode nenurodytas; naudokite administratoriaus pateiktą adresą.
