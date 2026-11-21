---
module: mcp
title: Banko pavedimai ir išrašų įkėlimas per AI asistentą
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: []
status: veikia
---

# Banko pavedimai ir išrašų įkėlimas per AI asistentą

**Būsena:** veikia

## Kam skirta
AI asistentas (Cikada MCP) gali perskaityti atskirus banko pavedimus iš senosios sistemos lentelės ir padėti įkelti naują banko išrašą (Excel, CSV, PDF, ekrano nuotrauka, MT940, CAMT.053 ar kitas formatas). Veiksmų tvarka tokia pati kaip [Finansų modulio įkėlime iš Excel](../finance/bankiniai-pavedimai-ikelimas.md), tik išrašą perskaito ChatGPT / Claude.

## Prieš pradedant
- Finansų prieiga: jūsų Cikada paskyra susieta su leidžiamu senosios sistemos SQL prisijungimu (rolės pavadinimo kode nėra) – žr. [Finansų apžvalgą](../finance/index.md).
- Pasirinkta šalis: LT (Lietuva) arba LV (Latvija).

## Žingsniai
### 1. Peržiūrėti esamus pavedimus
Įrankis `bank_payments_list` grąžina Nr, datą, mokėtoją, paskirtį ir sumą; filtrai: šalis, nuo–iki datos (imtinai), tekstas, rūšiavimas, puslapis (100 eilučių). Asistentas turi perskaityti visus puslapius, prieš sakydamas, kad sąrašas pilnas.
- „Parodyk visus bankinius pavedimus iš UAB Pavyzdys šiais metais.“
- „Kokie pavedimai gauti Latvijos banke praėjusią savaitę?“

### 2. Įkelti banko išrašą
1. Pridėkite išrašą į pokalbį ir parašykite: „Įkelk šį banko išrašą į Lietuvos pavedimus – pirma tik parodyk peržiūrą.“
2. Asistentas perskaito failą (ChatGPT ar Claude galimybėmis; MCP serveris originalaus failo negauna), išrenka kiekvienos operacijos datą, mokėtoją, paskirtį ir sumą ir iškviečia `bank_payments_preview` (ne daugiau 100 eilučių per partiją). Neperskaitomi laukai paliekami tušti ir pažymimi klaida – nieko neišgalvojama.
3. Peržiūra nieko neįrašo. Ji parodo: paskutinį Nr, numatomus naujus Nr, kiekvienos eilutės būseną:
   - Naujas (New) – pažymėta importui;
   - Dublikatas (Duplicate) – tokia pat data, firma ir suma jau yra;
   - Galimas dublikatas (PossibleDuplicate) – tokia pati data ir suma, bet kita firma (pvz., OCR klaida mokėtojo pavadinime); rodomas atitinkamas Nr ir mokėtojas; pagal nutylėjimą neįkeliamas;
   - Klaida (Error) – įkelti negalima.
4. Peržiūrėkite visą lentelę. Jei norite įkelti dublikatą ar galimą dublikatą, aiškiai pasakykite kurią eilutę.
5. Pasakykite aiškiai „Taip, įkelk“ – tada asistentas iškviečia `bank_payments_import` su `confirmed = true`, peržiūros ID ir pasirinktomis eilutėmis. Rašomos tik eilutės iš tos serverio peržiūros.
6. Rezultatas: „Imported N bank payments into LT, NR x–y.“

### 3. Jei importas nepavyko
- Peržiūra galioja 15 minučių; jei praėjo ar kitas naudotojas įkėlė naują pavedimą (pasikeitė paskutinis Nr), importas atmetamas – paprašykite peržiūros iš naujo.
- Klaidos „Pavedimas jau yra, pasikeitė…“ (409): atnaujinkite ir patikrinkite.

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| „Preview expired or changed. Paste the statement again.“ | Peržiūra pasenusi | Pakartokite peržiūrą |
| „The latest NR changed. Paste the statement again.“ | Kitas įrašas pakeitė paskutinį Nr | Pakartokite peržiūrą |
| 403 | Nėra Finansų prieigos | Kreipkitės į administratorių |
| „explicit confirmation is required…“ | Nebuvo aiškaus patvirtinimo | Patvirtinkite importą žodžiais |

## Dar neįgyvendinta
- Per asistentą pavedimo ištrinti ar importo atšaukti negalima – tai galima tik [Finansų modulio ekrane](../finance/bankiniai-pavedimai.md).
- Failų nuskaitymo kokybė priklauso nuo ChatGPT / Claude; visada patikrinkite peržiūrą.
