---
module: shopfloor
title: Gamybos modulis (Shopfloor) – apžvalga
routes: ["/shopfloor/"]
roles: []
status: dalinai
---

# Gamybos modulis (Shopfloor) – apžvalga

**Būsena:** dalinai

## Kam skirta
Gamybos modulis (Shopfloor) skirtas gamybos vadovams, technologams ir operatoriams. Dalis ekranų jau dirba su tikrais duomenimis (gamybos eilė iš senosios Access duomenų bazės, gamybos užduotys GU, Roletai be popieriaus, darbų sekų redaktorius, žinynai). Kiti ekranai kol kas rodo tik pavyzdinius duomenis (zagluška). Ši apžvalga nurodo, kas veikia, o kas ne.

## Prieš pradedant
- Modulį atidarykite per Portalą: adresas `/shopfloor`. Prisijungus pagal Portalo paskyrą. Neprisijungusį naudotoją sistema nukreipia į prisijungimo puslapį.
- Kode nėra atskirų rolių, kurios riboja ekranų atidarymą: visi prisijungę naudotojai mato visus meniu punktus. Teises apriboja pati gamybos duomenų bazė (senosios Access sistemos meniu teisės) ir kelios rolės (nurodytos prie atitinkamų puslapių).
- Gamybos eilės, užduočių ir dokumentų ekranai veikia tik tada, kai serveryje įjungtas nustatymas `Shopfloor:ProductionQueue:Enabled`. Bandymų stende (cikada-test) jis įjungtas kartu su `Shopfloor:ProductionTaskWrite:Enabled`. Gamybinėje aplinkoje nustatymų reikšmės iš kodo nematyti.

## Ekranų sąrašas ir būsena
Kairiajame meniu (UI: Gamyba) yra keturi skyriai: (UI: Darbų sekos), (UI: Žinynai), (UI: Sąsajos), (UI: Vykdymas). Pradžios puslapis yra (UI: Skydelis).

| Ekranas | Kelias | Būsena | Aprašas |
|---|---|---|---|
| Skydelis | `/shopfloor/dashboard` | laikina | [Skydelis](dashboard.md) |
| Gamyba: Paruošta gamybai ir išdavimas į GU | `/shopfloor/gamyba` | veikia | [Paruošta gamybai](gamyba-eile-ir-isdavimas.md) |
| Gamyba: Gamybos užduotys (GU) | `/shopfloor/gamyba` | veikia | [Gamybos užduotys](gamyba-uzduotys.md) |
| Gamyba: GT dokumentai, gaminio istorija, remontai | `/shopfloor/gamyba` | dalinai | [GT, istorija, remontai](gamyba-gt-istorija-remontai.md) |
| Gamyba: Dokumentai ir Roletai šablonas | `/shopfloor/gamyba` | dalinai | [Gamybos dokumentai](gamyba-dokumentai.md) |
| Gamyba: Trapecijos dokumentas | `/shopfloor/gamyba` | veikia | [Trapecijos dokumentas](trapecijos-dokumentas.md) |
| Gamyba: Skenavimas, Sustabdymai, Gatava produkcija, Technologija, Etikečių eilė | `/shopfloor/gamyba` | laikina | [Pavyzdiniai ekranai](gamyba-pavyzdiniai-ekranai.md) |
| Roletai (be popieriaus) | `/shopfloor/roletai` | veikia | [Roletai](roletai.md) |
| Operatoriaus eilė | `/shopfloor/operator-work-queue` | laikina | [Operatoriaus eilė](operator-work-queue.md) |
| Gamybos šeimos | `/shopfloor/workflows` | veikia | [Gamybos šeimos](workflows.md) |
| Darbų sekų redaktorius | `/shopfloor/workflow-editor` | veikia | [Redaktorius](workflow-editor.md) |
| Gamybos centrai ir darbo vietos | `/shopfloor/ref/work-centers`, `/shopfloor/ref/stations` | veikia | [Žinynai](zinynai.md) |
| Senos sistemos tipai ir aprėptis | `/shopfloor/map/product-types` | veikia | [Tipų sąsajos](produktu-tipu-sasajos.md) |

Gamyba (`/shopfloor/gamyba`) yra vienas didelis ekranas su skirtukais viršuje. Skirtukai (UI: Paruošta gamybai), (UI: Gamybos užduotys), (UI: GT dokumentai), (UI: Dokumentai) ir dalis (UI: Sustabdymai ir taisymai) dirba su tikrais duomenimis. Kiti skirtukai rodo pavyzdinius duomenis ir ekrano viršuje parodo pranešimą apie tai.

## Kaip skirstomi darbai
- Gamybos šeima (darbų seka) aprašo užduočių seką su darbo vieta ir trukme. Šeimos kuriamos ir paskelbiamos puslapyje (UI: Gamybos šeimos), schema braižoma redaktoriuje.
- Senos Access sistemos gaminių tipai susiejami su šeimomis puslapyje (UI: Senos sistemos tipai ir aprėptis).
- Kasdienis gamybos darbas (eilė, GU, dokumentai, Roletai) vyksta ekrane Gamyba ir puslapyje Roletai. Jie skaito bendrą senąją Access duomenų bazę.

## Dar neįgyvendinta
- Skydelis ir Operatoriaus eilė rodo tik pavyzdinius duomenis.
- Gamybos darbų sekos (šeimos) kol kas nesujungtos su gamybos eilės vykdymu: jos tik aprašomos ir susiejamos su senais tipais.
- Skenavimas, sustabdymai, gatava produkcija, technologija ir etikečių eilė ekrane Gamyba yra tik maketas.
- Kas konkrečiai nebuvo nustatyta iš kodo: kokios Access meniu teisės reikalingos kiekvienam veiksmui (kode matomas tik užduočių meniu numeris 49 ir gatavos produkcijos 41).
