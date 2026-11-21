# Cikada.MES – naudotojo vadovas

Čia aprašyti visi Cikada.MES (LKvitai.MES) moduliai: ką kiekvienas ekranas daro, kokiais žingsniais atliekamas darbas ir kas dar neįgyvendinta.

> **Kalba.** Vadovai parašyti lietuviškai. Mygtukų, meniu ir laukų pavadinimai pateikiami taip, kaip jie matomi ekrane, pvz. `(UI: Create Inbound)`.

## Kaip skaityti būseną

Kiekvieno puslapio pradžioje nurodyta būsena:

- **veikia** – ekranas veikia nuo pradžios iki galo;
- **dalinai** – dalis funkcijų veikia, puslapyje išvardyta, kas ne;
- **laikina (zagluška)** – ekranas yra, bet rodo pavyzdinius duomenis arba mygtukai dar nieko nedaro.

Tie patys vadovai pasiekiami ir per AI asistentą (Cikada MCP): asistentas ieško šiuose puslapiuose ir atsako pagal juos.

## Portalas ir darbo pradžia

Prisijungimas, kalbos keitimas, modulių plytelės.

| Puslapis | Būsena |
|---|---|
| [Portalas](portal/index.md) | dalinai |
| [Darbo pradžia Portale](portal/darbo-pradzia.md) | veikia |
| [Plytelių valdymas (Portal CMS)](portal/plyteliu-valdymas.md) | veikia |
| [Portalo pradžios puslapis](portal/pradzios-puslapis.md) | dalinai |

## Pardavimai (Sales)

Užsakymų sąrašas, užsakymo informacija ir užsakymų įvedimas.

| Puslapis | Būsena |
|---|---|
| [Pardavimų modulis – apžvalga](sales/index.md) | dalinai |
| [Nutrūkęs išsaugojimas ir atkūrimas](sales/uzsakymo-atkurimas.md) | veikia |
| [Gaminiai ir priedai užsakyme](sales/uzsakymo-gaminiai.md) | dalinai |
| [Užsakymo informacija](sales/uzsakymo-informacija.md) | dalinai |
| [Naujo užsakymo kūrimas ir užsakymo kortelė](sales/uzsakymo-ivedimas.md) | dalinai |
| [Mokėjimai, patvirtinimas gamybai ir anuliavimas](sales/uzsakymo-mokejimai-patvirtinimas.md) | dalinai |
| [Užsakymų sąrašas](sales/uzsakymu-sarasas.md) | dalinai |

## Gamyba (Shopfloor)

Gamybos eilė, gamybos užduotys, dokumentai, roletai, darbų sekos.

| Puslapis | Būsena |
|---|---|
| [Gamybos modulis (Shopfloor) – apžvalga](shopfloor/index.md) | dalinai |
| [Skydelis](shopfloor/dashboard.md) | laikina (zagluška) |
| [Gamyba – Dokumentai ir Roletai šablonas](shopfloor/gamyba-dokumentai.md) | dalinai |
| [Gamyba – Paruošta gamybai ir išdavimas į GU](shopfloor/gamyba-eile-ir-isdavimas.md) | veikia |
| [Gamyba – GT dokumentai, gaminio istorija, remontai](shopfloor/gamyba-gt-istorija-remontai.md) | dalinai |
| [Gamyba – ekranai su pavyzdiniais duomenimis](shopfloor/gamyba-pavyzdiniai-ekranai.md) | laikina (zagluška) |
| [Gamyba – Gamybos užduotys (GU)](shopfloor/gamyba-uzduotys.md) | veikia |
| [Operatoriaus darbų eilė](shopfloor/operator-work-queue.md) | laikina (zagluška) |
| [Senos sistemos tipai ir aprėptis](shopfloor/produktu-tipu-sasajos.md) | veikia |
| [Roletai (gamyba be popieriaus)](shopfloor/roletai.md) | veikia |
| [Gamyba – Trapecijos dokumentas](shopfloor/trapecijos-dokumentas.md) | veikia |
| [Darbų sekų redaktorius](shopfloor/workflow-editor.md) | veikia |
| [Gamybos šeimos (darbų sekų šablonai)](shopfloor/workflows.md) | veikia |
| [Žinynai – gamybos centrai ir darbo vietos](shopfloor/zinynai.md) | veikia |

## Sandėlis (Warehouse) – procesai

Priėmimas, sandėliavimas, išsiuntimas, inventorizacija, vertinimas, Agnum, administravimas.

| Puslapis | Būsena |
|---|---|
| [Prekių priėmimas ir kokybės patikra (Inbound)](procesai/P-01-priemimas-inbound.md) | veikia |
| [Padėjimas į sandėlio vietą (Putaway)](procesai/P-02-padeti-i-lokacija-putaway.md) | veikia |
| [Pardavimo užsakymai, pakavimas ir išsiuntimas (Outbound)](procesai/P-03-israsymas-isuntimas-outbound.md) | dalinai |
| [Atsargų perkėlimas tarp vietų (Transfers)](procesai/P-04-perkelimas-transfer.md) | veikia |
| [Inventorizacija (Cycle Counts)](procesai/P-05-inventorizacija-cycle-count.md) | dalinai |
| [Atsargų koregavimas (Stock Adjustments)](procesai/P-06-atsargu-koregavimas-adjustments.md) | veikia |
| [Atsargų vertinimas ir nukainojimas](procesai/P-07-atsargu-vertinimas-valuation.md) | veikia |
| [Agnum integracija (suminė apskaita)](procesai/P-08-agnum-integracija.md) | veikia |
| [Klientų grąžinimai (RMA)](procesai/P-09-grazinimai-rma.md) | dalinai |
| [Tiesioginis perkrovimas (Cross-Dock)](procesai/P-10-krzyminis-perkrovimas-crossdock.md) | dalinai |
| [Partijų ir serijų numeriai](procesai/P-11-partiju-seriju-sekimas.md) | veikia |
| [Sandėlio žemėlapis (3D ir 2D)](procesai/P-12-sandelio-zemelapis-visualization.md) | veikia |
| [Sandėlio ataskaitos](procesai/P-13-ataskaitos-analize-reports.md) | veikia |
| [Sistemos administravimas - nustatymai, priežasčių kodai, patvirtinimo taisyklės](procesai/P-14-sistemos-administravimas.md) | dalinai |
| [Pagrindinių duomenų valdymas - tiekėjai, klientai, kainų grupės, importas](procesai/P-15-pagrindinis-duomenu-valdymas.md) | veikia |
| [Pirkimo dokumentas (tiekėjo sąskaita, valiuta, PVM, užregistravimas)](procesai/P-16-pirkimo-dokumentas.md) | veikia |
| [Tiekėjo kreditinės sąskaitos ir prekių grąžinimas tiekėjui](procesai/P-17-tiekejo-kreditines-ir-grazinimai.md) | dalinai |
| [Kito tiekėjo papildomų išlaidų sąskaitos](procesai/P-18-papildomu-islaidu-saskaitos.md) | veikia |
| [Agnum apskaitos nustatymai ir siuntimo eilė](procesai/P-19-agnum-nustatymai-ir-eile.md) | veikia |
| [Pradiniai likučiai iš CSV failo](procesai/P-20-pradiniai-likuciai.md) | veikia |
| [Intrastat atvykimų ataskaita](procesai/P-21-intrastat-atvykimai.md) | veikia |
| [Mėnesio uždarymas: atsargų vertės ataskaita, tiltas į Agnum, aktai](procesai/P-22-menesio-uzdarymas.md) | veikia |
| [Etikečių stotelė (Labels)](procesai/P-23-etikeciu-stotele.md) | veikia |
| [Atsargų peržiūra (Available Stock, Stock Dashboard, Location Balance)](procesai/P-24-atsargu-perziura.md) | veikia |
| [Rezervacijos (Reservations)](procesai/P-25-rezervacijos.md) | veikia |
| [Bangos ir komplektavimo užduotys (Wave Picking, Picking Tasks)](procesai/P-26-bangos-ir-komplektavimas.md) | dalinai |
| [Pradžia ir sistemos būsena](procesai/P-27-pradzia-ir-sistemos-busena.md) | veikia |
| [Atsekamumas ir atitiktis](procesai/P-28-atsekamumas-ir-atitiktis.md) | dalinai |
| [Analitika (užsakymų vykdymas ir kokybė)](procesai/P-29-analitika.md) | dalinai |
| [Vartotojai, rolės ir API raktai](procesai/P-30-vartotojai-roles-api-raktai.md) | dalinai |
| [Auditas, duomenų saugojimas, GDPR, atsarginės kopijos ir avarinio atkūrimo bandymai](procesai/P-31-auditas-saugojimas-gdpr-kopijos.md) | dalinai |
| [Prekių pagrindiniai duomenys - prekės, kategorijos, matavimo vienetai, partijos, serijos numeriai](procesai/P-32-prekiu-pagrindiniai-duomenys.md) | veikia |
| [Sandėliai, sandėlio vietos, slotai ir sandėlio maketas](procesai/P-33-sandeliai-vietos-slotai-maketas.md) | veikia |

## Sandėlis (Warehouse) – rolės

Kas ką gali daryti sandėlio modulyje.

| Puslapis | Būsena |
|---|---|
| [Sandėlio vadovas ir buhalteris](roles/gamybos-vadovas.md) | veikia |
| [Kokybės inspektorius](roles/kokybes-inspektorius.md) | veikia |
| [Sandėlio operatorius](roles/sandelio-operatorius.md) | veikia |

## Sandėlis (Warehouse) – trikčių šalinimas

Dažnos klaidos ir ką daryti.

| Puslapis | Būsena |
|---|---|
| [Trikčių šalinimas (Sandėlis)](trikciu-salinimas/index.md) | veikia |
| [Dažnos klaidos ir sprendimai (Sandėlis)](trikciu-salinimas/daznos-klaidos.md) | veikia |

## Frontline (audinių likučiai)

Audinio likučio patikra salone ir mažų likučių sąrašas.

| Puslapis | Būsena |
|---|---|
| [Frontline modulis (audinių likučiai)](frontline/index.md) | dalinai |
| [Audinio patikra (Fabric availability)](frontline/audinio-patikra.md) | dalinai |
| [Mažų likučių sąrašas (Low-stock materials)](frontline/mazu-likuciu-sarasas.md) | dalinai |

## Finansai (Finance)

Bankiniai pavedimai.

| Puslapis | Būsena |
|---|---|
| [Finansų modulis – apžvalga](finance/index.md) | dalinai |
| [Banko pavedimų įkėlimas iš Excel](finance/bankiniai-pavedimai-ikelimas.md) | veikia |
| [Bankiniai pavedimai](finance/bankiniai-pavedimai.md) | veikia |

## Skenavimas (Scanning)

Modulio karkasas.

| Puslapis | Būsena |
|---|---|
| [Skenavimo modulis (Scanning) – apžvalga](scanning/index.md) | laikina (zagluška) |
| [Skenavimo modulio pradžios puslapis](scanning/pradzios-puslapis.md) | laikina (zagluška) |

## AI asistentas (Cikada MCP)

Darbas su Cikada iš ChatGPT arba Claude.

| Puslapis | Būsena |
|---|---|
| [AI asistentas (Cikada MCP) – apžvalga](mcp/index.md) | veikia |
| [Vadovų ataskaitos ir sąrašai per AI asistentą](mcp/ataskaitos.md) | veikia |
| [Banko pavedimai ir išrašų įkėlimas per AI asistentą](mcp/bankas.md) | veikia |
| [Prisijungimas iš ChatGPT ir Claude](mcp/prisijungimas.md) | veikia |
| [Ką galima klausti – užsakymai, klientai, kainos, atsargos](mcp/uzsakymai-klientai-atsargos.md) | veikia |
| [Užsakymų įvedimas per AI asistentą (pagal nutylėjimą išjungta)](mcp/uzsakymu-ivedimas.md) | dalinai |

