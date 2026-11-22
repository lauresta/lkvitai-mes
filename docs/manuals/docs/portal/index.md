---
module: portal
title: Portalas
routes: ["/", "/cms/tiles"]
roles: []
status: dalinai
---

# Portalas

**Būsena:** dalinai

## Kam skirta
Cikada.MES Portalas yra pradinis visų modulių puslapis: čia prisijungiama, pasirenkamas modulis, perjungiama kalba ir (administratoriams) tvarkomos modulių plytelės. Testinėje aplinkoje Portalas yra adresu `/portal`, o svetainės šaknis (`https://cikada-test.lauresta.com/`) nukreipia į jį.

## Puslapiai

| Puslapis | Adresas | Kam skirtas | Būsena |
|----------|---------|-------------|--------|
| [Darbo pradžia](darbo-pradzia.md) | `/login.html`, viršutinė juosta | Prisijungimas per Lauresta SSO, kalba EN/LT, aplinkos juosta, atsijungimas | veikia |
| [Pradžios puslapis](pradzios-puslapis.md) | `/` | Modulių plytelės, veiklos apžvalga, naujienos, sistemos būsena | dalinai |
| [Plytelių valdymas](plyteliu-valdymas.md) | `/cms/tiles` | Modulių plytelių kūrimas, redagavimas, slėpimas | veikia |

## Kas dar yra Portale
Portalas turi ir bendrą viršutinę juostą, kurią rodo visi moduliai (Portalas, Sales, Frontline ir kiti): aplinkos juosta, prekės ženklas su nuoroda į Portalo pradžią, paieškos mygtukas, aplinkos ir versijos ženklas, kalbos jungiklis ir vartotojo blokas. Ji aprašyta puslapyje [Darbo pradžia](darbo-pradzia.md).

## Dar neįgyvendinta
- Bendra paieška (UI: Search everything…) randa užsakymus, klientus, prekes, tiekėjus ir Sales bei Sandėlio langus. Kitų modulių duomenų, žinynų puslapių ir paskutinių paieškų dar nerodo.
- Pradžios puslapio skiltys „Operations preview“ ir „News“ gali rodyti pavyzdinius duomenis, kai tikri nepasiekiami.
- Pradžios puslapio apačios nuorodos (UI: Release notes), (UI: Documentation), (UI: Support) ir (UI: All updates) yra neaktyvios (vedama į `#`).
