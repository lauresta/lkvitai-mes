---
module: warehouse
title: Dažnos klaidos ir sprendimai (Sandėlis)
routes: []
roles: []
status: veikia
---

# Dažnos klaidos ir sprendimai

**Būsena:** veikia

## Prisijungimas {#prisijungimas}
Sandėlio modulis (Warehouse) neturi savo prisijungimo lango: neprisijungus naršyklė nukreipia į portalo prisijungimo puslapį, o po prisijungimo grąžina į prašytą ekraną.
1. Jei prisijungti nepavyksta – patikrinkite vartotojo vardą ir slaptažodį portalo prisijungimo lange.
2. Jei užmiršote slaptažodį – kreipkitės į sistemos administratorių. Sandėlio ekranas (UI: Admin > Users) slaptažodžio ir vartotojo vardo keisti neleidžia (žr. [P-30](../procesai/P-30-vartotojai-roles-api-raktai.md)).
3. Jei prisijungę matote „Access denied. Your account does not have permission to open this page.“ – paskyrai trūksta rolės šiam ekranui. Spauskite (UI: Back to dashboard) ir paprašykite administratoriaus priskirti rolę.

## Atsargų kiekiai nesutampa {#atsargu-kiekiai}
Fizinis kiekis skiriasi nuo sistemos kiekio.
1. Atidarykite (UI: Stock > Location Balance) ir patikrinkite konkrečią vietą, žr. [P-24](../procesai/P-24-atsargu-perziura.md).
2. Patikrinkite rezervacijas: (UI: Stock > Reservations), žr. [P-25](../procesai/P-25-rezervacijos.md). Rezervuotas kiekis į prieinamą neįskaičiuojamas.
3. Jei prekės priimtos, bet dar nepadėtos į lentyną, jos yra vietoje RECEIVING arba QC_HOLD – žr. [P-01](../procesai/P-01-priemimas-inbound.md) ir [P-02](../procesai/P-02-padeti-i-lokacija-putaway.md).
4. Jei nesutapimas tikras, vadovas gali pakoreguoti atsargas: (UI: Stock > Adjustments), [P-06](../procesai/P-06-atsargu-koregavimas-adjustments.md), arba atlikti inventorizaciją [P-05](../procesai/P-05-inventorizacija-cycle-count.md).
5. Jei skaičiai rodomi seni – palaukite kelias sekundes ir perkraukite puslapį (žr. toliau „Projekcijų vėlavimas“).

## Spausdintuvas nereaguoja {#spausdintuvas}
Etikečių spausdintuvas nespausdina.
1. Patikrinkite, ar spausdintuvas įjungtas ir pasiekiamas tinkle.
2. Ekrane (UI: Outbound > Labels) peržiūrėkite eilę (UI: Queue): nepavykusią eilutę kartokite mygtuku (UI: Retry). Jei buvo sukurtas tik PDF, atsisiųskite jį (UI: Get PDF). Žr. [P-23](../procesai/P-23-etikeciu-stotele.md).
3. Jei problema kartojasi – kreipkitės į IT pagalbos tarnybą.

## Projekcijų vėlavimas {#projekcijos}
Duomenys rodomi seni arba ekrane matoma žyma (UI: Stale).
1. Sandėlio suvestinės atsinaujina asinchroniškai; kelių sekundžių vėlavimas yra normalus.
2. Jei vėlavimas ilgesnis kaip 30 sekundžių (raudona žyma) – kreipkitės į administratorių.
3. Administratorius ekrane (UI: Operations > Projections) mato atsilikimą ir gali paleisti (UI: Verify) bei prireikus (UI: Rebuild). Žr. [P-27](../procesai/P-27-pradzia-ir-sistemos-busena.md).

## Rezervacijos negali būti atšauktos {#rezervacijos}
Ekrane (UI: Stock > Reservations) rezervacijos atšaukimo mygtuko nėra.
1. Rezervacija, susieta su pardavimo užsakymu, atšaukiama atšaukiant užsakymą: (UI: Outbound > Sales Orders) > užsakymo kortelė > (UI: Cancel Order). Išsiųsto užsakymo atšaukti negalima – daromas grąžinimas ([P-09](../procesai/P-09-grazinimai-rma.md)).
2. Rezervacijos būsena Picking reiškia, kad komplektavimas jau prasidėjo; pirmiausia užbaikite ar sustabdykite komplektavimą ([P-26](../procesai/P-26-bangos-ir-komplektavimas.md)).
3. Jei rezervacija užstrigusi (pvz., Picking ilgiau kaip 2 valandas – žymė „N in picking > 2h“ pradžios skydelyje), informuokite sandėlio vadovą ar administratorių.

## Pirkimo dokumento neužregistruoja (UI: Post) {#pirkimo-dokumentas}
Dokumento kortelėje virš mygtuko (UI: Post) rodomas raudonas sąrašas. Pagrindinės priežastys ir sprendimai (visas sąrašas – [P-16](../procesai/P-16-pirkimo-dokumentas.md)):
1. „Lines and additional costs do not add up to the net amount.“ – eilučių ir išlaidų suma turi tiksliai sutapti su (UI: Net amount); pataisykite per (UI: Edit).
2. „No Agnum posting rule is set for this stock account and VAT treatment.“ – buhalteris turi pridėti taisyklę (UI: Finance > Agnum Accounting) > (UI: Postings), žr. [P-19](../procesai/P-19-agnum-nustatymai-ir-eile.md).
3. „The supplier cannot be posted to Agnum.“ – tiekėjas nesusietas su Agnum arba nėra kreditorius; ekrane (UI: Admin > Suppliers) paspauskite (UI: Refresh from Agnum), jei neišsprendžia – tiekėją reikia sutvarkyti Agnum.
4. „Every stock item of an EU acquisition needs an 8-digit commodity code and a net weight.“ – papildykite prekių kortelę (UI: Admin > Items) > (UI: Edit), žr. [P-32](../procesai/P-32-prekiu-pagrindiniai-duomenys.md).
5. „The exchange rate for today is not published yet.“ / „There is no exchange rate for the document date.“ – palaukite paskelbimo (Lietuvos banko kursai) arba buhalteris įveda rankinį kursą su priežastimi.
6. „The month of this document is closed …“ – mėnesį turi atidaryti buhalteris ([P-22](../procesai/P-22-menesio-uzdarymas.md)).

## Dokumentas nepatenka į Agnum {#agnum-eile}
Užregistruotas dokumentas Agnum nepasirodo.
1. Atidarykite (UI: Finance > Agnum sending queue) ir raskite dokumentą. Žr. [P-19](../procesai/P-19-agnum-nustatymai-ir-eile.md).
2. „Held: sending is off or the date is before the start date“ – kortelėje (UI: Sending) įjunkite siuntimą arba patikrinkite (UI: Start date).
3. Būsena Unknown – tinklo klaida; sistema pati pasitikrins, ar Agnum jau turi dokumentą. Galima paspausti (UI: Send now).
4. Būsena Needs review – perskaitykite klaidą, ištaisykite ir spauskite (UI: Retry); jei buhalteris dokumentą įvedė Agnum pats – (UI: Mark manual).
5. „Agnum lists could not be loaded“ nustatymų ekrane – Agnum paslauga nepasiekiama; kortelėje (UI: Sending) spauskite (UI: Check connection) ir informuokite administratorių.

## Mėnesio negalima uždaryti {#menesio-uzdarymas}
Ekrane (UI: Finance > Month end) mygtukas (UI: Close month) neaktyvus ir rodomas sąrašas „The month cannot be closed yet:“. Priežastys: mėnuo dar nepasibaigė; yra neišsiųstų dokumentų Agnum eilėje; tilte į Agnum liko nepaaiškintas skirtumas; ataskaitos likutis (UI: Residual) nelygus nuliui. Sprendimai – [P-22](../procesai/P-22-menesio-uzdarymas.md).

## Kita
Matote klaidą su `Trace ID`.
1. Užsirašykite `Trace ID` (pvz. `abc-123-def`).
2. Perduokite jį IT pagalbos tarnybai – pagal jį randama klaida žurnaluose.
