---
module: sales
title: Nutrūkęs išsaugojimas ir atkūrimas
routes: ["/sales/entry/{UžsakymoId}"]
roles: []
status: veikia
---

# Nutrūkęs išsaugojimas ir atkūrimas

**Būsena:** veikia

## Kam skirta
Pardavimų modulio užsakymo įvedimo ekrane rodomi geltoni pranešimai, kai ryšys nutrūko išsaugant užsakymą, gaminį, mokėjimą ar patvirtinimą. Kad nebūtų sukurta dviguba operacija, sistema išsaugo ką tik siųstus duomenis naršyklės seanse ir leidžia saugiai patikrinti arba pakartoti tą pačią operaciją.

## Prieš pradedant
- Neuždarykite naršyklės skirtuko, kol išsaugojimo rezultatas neaiškus. Naršyklės seanso saugykla turi būti leidžiama.
- Prisijunkite ta pačia paskyra, kuria išsaugojimas buvo pradėtas.

## Žingsniai
### 1. Patikrinti nutrūkusį išsaugojimą
1. Pamatę pranešimą „Išsaugojimas nutruko. Įvesti duomenys išsaugoti. Prieš redaguodami šį užsakymą patikrinkite ankstesnio išsaugojimo rezultatą.“ paspauskite (UI: Patikrinti / kartoti išsaugojimą).
2. Sistema patikrina, ar operacija jau įvykdyta, ir parodo dabartines reikšmes („Išsaugotas rezultatas atkurtas. Rodomos dabartinės reikšmės.“). Jei rezultato nerasta, operacija pakartojama ta pačia komanda, todėl dubliavimosi nebūna.
3. Kol rezultatas neaiškus, ekrano laukai užrakinti.

### 2. Ankstesnis nepatikrintas išsaugojimas kitam užsakymui
1. Pranešimas „Reikia patikrinti ankstesnį išsaugojimą. Tai neblokuoja šio užsakymo.“ – paspauskite (UI: Patikrinti ankstesnį išsaugojimą).
2. Rastą užsakymą atidarykite nuoroda (UI: Atidaryti jo užsakymą) arba (UI: Atidaryti tą užsakymą).
3. Jei nebaigtas naujo užsakymo sukūrimas, paspauskite (UI: Atidaryti užsakymus ir atkurti nebaigtą naują užsakymą) arba (UI: Kiti užsakymai).

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| „Ryšys nutrūko. Įvesti duomenys išsaugoti; pakartokite tą patį išsaugojimą, kad atkurtumėte rezultatą.“ | Ryšio klaida | Paspauskite (UI: Patikrinti / kartoti išsaugojimą) |
| „Duomenų bazė nepatvirtino išsaugojimo…“ | Nėra patvirtinimo iš SQL | Naudokite „Patikrinti / kartoti išsaugojimą“ |
| „Naršyklei nepavyko išsaugoti atkūrimo duomenų. Niekas neišsiųsta.“ | Naršyklės saugykla uždrausta | Leiskite seanso saugyklą ir neuždarykite formos |
| „Seansą reikia atnaujinti. Prisijunkite kitame skirtuke ir bandykite dar kartą.“ | Pasibaigė prisijungimas | Prisijunkite per portalą kitame skirtuke, grįžkite |
| „Per daug išsaugojimų reikia atkurti.“ | Susikaupė daug neišspręstų operacijų | Išspręskite ankstesnius |

## Dar neįgyvendinta
Nėra – visi čia aprašyti mygtukai turi veiksmus. Atkūrimas veikia tik toje pačioje naršyklėje ir tuo pačiu seansu.
