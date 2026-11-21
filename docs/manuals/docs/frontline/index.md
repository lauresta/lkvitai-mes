---
module: frontline
title: Frontline modulis (audinių likučiai)
routes: ["/", "/fabric", "/fabric/lookup", "/fabric/low-stock"]
roles: []
status: dalinai
---

# Frontline modulis (audinių likučiai)

**Būsena:** dalinai

## Kam skirta
Frontline modulis (testinėje aplinkoje adresas `/frontline`) skirtas greitai patikrinti audinių likučius: pardavimo salėje telefonu (vieno audinio paieška) ir pirkimų ar operacijų darbuotojams kompiuteriu (mažų likučių sąrašas). Modulio antraštėje rodomas ženklas „FA“ ir pavadinimas „Fabric availability“.

## Ekranai
Modulis turi du ekranus. Jų pavadinimai ekrane yra angliški (UI: Lookup, UI: Low stock).

| Ekranas | Adresas | Kam skirtas | Būsena |
|---------|---------|-------------|--------|
| [Audinio patikra](audinio-patikra.md) (UI: Fabric availability) | `/`, `/fabric`, `/fabric/lookup` | Vieno audinio likutis, plotis, atvežimas, alternatyvos | dalinai |
| [Mažų likučių sąrašas](mazu-likuciu-sarasas.md) (UI: Low-stock materials) | `/fabric/low-stock` | Visi audiniai, kurių likutis mažas | dalinai |

Tarp ekranų perjungiama antraštėje esančiu perjungikliu (UI: Lookup) ir (UI: Low stock).

## Prieigos teisės
Frontline ekranai ir jų API atidaromi be prisijungimo: kodas tyčia nenaudoja `[Authorize]`, kad pardavimo salės telefonai galėtų atidaryti patikrą nuskenavus QR kodą. Jei vartotojas jau prisijungęs prie Portalo, viršutinėje juostoje matomas jo vardas, kitaip rodoma „Signed in“ ir raidė „U“.

## Bendra viršutinė juosta
Viršuje rodoma bendra Cikada.MES juosta: geltona aplinkos juosta, prekės ženklas, paieškos mygtukas, aplinka ir versija, kalbos jungiklis EN/LT ir atsijungimo mygtukas. Jos aprašymas pateiktas Portalo skyriuje [Darbo pradžia](../portal/darbo-pradzia.md).

## Duomenų šaltinis
Duomenys imami iš senosios duomenų bazės procedūrų `dbo.mes_Fabric_GetMobileCard` (patikra) ir `dbo.mes_Fabric_GetLowStockList` (sąrašas). Jei serveryje nenurodytas ryšys su duomenų baze (`ConnectionStrings:LKvitaiDb`), API automatiškai naudoja vidinį bandomųjų duomenų rinkinį su keliais pavyzdiniais audiniais. Iš kodo negalima nustatyti, kuris režimas įjungtas konkrečioje aplinkoje; jei matote tik kelis žinomus pavyzdinius kodus, tikėtina, kad veikia bandomasis režimas.

## Dar neįgyvendinta
- Mygtukas (UI: Reserve) audinio kortelėje išjungtas, ant jo užrašas „SOON“.
- Sąrašo eilutės mygtukai (UI: Replace), (UI: Notify), (UI: Reserve) neturi jokio veiksmo.
- Nuorodos į konkretų audinį (`fabric?code=...`) puslapį atidaro, bet kodo automatiškai neįveda.
- Nuotraukų įkėlimo ar redagavimo ekrano nėra: nuotraukos tik rodomos.
