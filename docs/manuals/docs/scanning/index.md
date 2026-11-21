---
module: scanning
title: Skenavimo modulis (Scanning) – apžvalga
routes: ["/scan/"]
roles: []
status: laikina
---

# Skenavimo modulis (Scanning) – apžvalga

**Būsena:** laikina (zagluška)

## Kam skirta
Skenavimo modulis (Scanning) ateityje turi tapti bendru mobiliuoju brūkšninių kodų paieškos ekranu visoms sistemos sritims. Šiandien modulis yra tik tuščias karkasas. Verslo funkcijų jame nėra, naudotojas negali nuskenuoti ar surasti jokio kodo.

## Kas yra modulyje
- Vienas puslapis `/scan/`, aprašytas puslapyje [Pradžios puslapis](pradzios-puslapis.md).
- Serverio dalyje (API) yra tik vienas veikiantis adresas `GET /api/scan/ping`, kuris patvirtina, kad modulis paleistas. Jis atsako be prisijungimo.
- Duomenų bazės, užklausų ir skenavimo apdorojimo kodo nėra. Aplankuose Application, Contracts ir Infrastructure yra tik tušti žymekliai (`_Placeholder`, `_Module`).

## Prieš pradedant
Rolių ir teisių modulis neturi. Puslapis viešas: prisijungti nereikia. Kode pažymėta, kad kai bus parengtas bendras rolių modelis, puslapis pereis prie Portalo prisijungimo ir reikalaus skaitytuvo tapatybės.

## Dar neįgyvendinta
- Skenavimas, kodų paieška, rezultatų rodymas.
- Rolės ir prisijungimas prie puslapio.
- Dabartinis gamybos skenavimas (pagal GT dokumentus) yra ne šiame modulyje, o gamybos modulyje; ten jis taip pat tik maketas (žr. [Gamybos ekranai su pavyzdiniais duomenimis](../shopfloor/gamyba-pavyzdiniai-ekranai.md)).
