---
module: mcp
title: Prisijungimas iš ChatGPT ir Claude
routes: ["https://cikada-test.lauresta.com/mcp"]
roles: []
status: veikia
---

# Prisijungimas iš ChatGPT ir Claude

**Būsena:** veikia

## Kam skirta
Šis puslapis aprašo, kaip prijungti Cikada MCP asistentą prie ChatGPT arba Claude, kad pokalbyje galėtumėte klausti apie užsakymus, skolas, atsargas ir ataskaitas. Prisijungiama vieną kartą kiekvienam naudotojui; slaptažodžio asistentui neperduodate – prisijungiate savo Lauresta SSO paskyra.

## Prieš pradedant
- Turite Lauresta SSO paskyrą (`sso.lauresta.com`) ir Cikada roles ([apžvalga](index.md)).
- Žinote serverio adresą: testinėje aplinkoje `https://cikada-test.lauresta.com/mcp`.
- Prisijungimas (OAuth su PKCE) naudoja viešą klientą be paslapties: nieko papildomai suvesti nereikia.

## Žingsniai
### 1. ChatGPT
1. ChatGPT: (UI: Settings → Apps & Connectors → Advanced settings) ir įjunkite (UI: Developer mode). Plus / Pro / Business planuose jis būtinas savo MCP serveriui; Enterprise / Edu planuose jungtį paskelbia administratorius.
2. (UI: Settings → Apps & Connectors) → (UI: Create) (arba „Add custom connector“).
3. Pavadinimas: `Cikada`. MCP server URL: `https://cikada-test.lauresta.com/mcp`. Authentication: (UI: OAuth). Kliento ID ir paslapties laukų nepildykite – ChatGPT užsiregistruoja pats.
4. Paspauskite (UI: Create). Atsidariusiame lange prisijunkite `sso.lauresta.com` ir patvirtinkite prieigą.
5. Pokalbyje pasirinkite Cikada jungtį (Developer mode įrankių meniu) ir klauskite, pvz.: „Parodyk šiandienos užsakymus Vilniuje su skola.“

### 2. Claude (claude.ai ir Claude Desktop)
1. (UI: Settings → Connectors) → (UI: Add custom connector).
2. URL: `https://cikada-test.lauresta.com/mcp`.
3. Paspauskite (UI: Connect) ir prisijunkite `sso.lauresta.com`.

### 3. Claude Code (komandinė eilutė)
1. Vykdykite `claude mcp add --transport http cikada https://cikada-test.lauresta.com/mcp`.
2. Atidarykite `/mcp`, pasirinkite `cikada` ir (UI: Authenticate).

### 4. Pasitikrinti
Pokalbyje paprašykite: „Kas aš esu sistemoje?“ – asistentas iškvies `whoami` ir parodys jūsų vardą bei roles.

## Dažnos klaidos
| Pranešimas | Priežastis | Ką daryti |
|---|---|---|
| Serveris atsako 401 | Nėra galiojančio prisijungimo | Pakartokite prisijungimą (Authenticate / Connect) |
| Asistentas rašo „403“ | Trūksta Cikada rolės arba SQL susiejimo | Paprašykite „Kas aš esu?“ ir kreipkitės į administratorių |
| Jungties nematyti ChatGPT | Developer mode išjungtas | Įjunkite Developer mode |
| „Order entry is not deployed on this environment yet“ | Aplinkoje nėra užsakymų įvedimo API | Naudokite tik skaitymo funkcijas |
