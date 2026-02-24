# Dažnos klaidos ir sprendimai

---

## Prisijungimas {#prisijungimas}

**Problema:** Negaliu prisijungti prie sistemos.

**Sprendimas:**
1. Patikrink, ar įvedi teisingą el. paštą ir slaptažodį.
2. Jei naudoji SSO (organizacijos paskyrą) — paspausk `(UI: Login with SSO)`.
3. Jei užmiršai slaptažodį — susisiek su sistemos administratoriumi (meniu `Admin` → `Users`, UI: Admin → Users).
4. Jei reikalingas dviejų žingsnių patvirtinimas (MFA) — įvesk kodą iš autentikatoriaus programėlės.

---

## Atsargų kiekiai nesutampa {#atsargu-kiekiai}

**Problema:** Fizinis kiekis skiriasi nuo sistemos kiekio.

**Sprendimas:**
1. Eik į `Stock` → `Location Balance` (UI: Stock → Location Balance) ir patikrink konkretų sandėlio skyrių.
2. Patikrink, ar nėra aktyvių rezervacijų: `Stock` → `Reservations` (UI: Stock → Reservations).
3. Jei nesutapimas tikras — kreipkis į sandėlio vadovą, kad atliktų atsargų koregavimą: `Stock` → `Adjustments` (UI: Stock → Adjustments).
4. Jei kiekiai rodomi seni — sistema gali atsilikti iki 5 sekundžių (`Refreshing...` indikatorius). Palaukite ir perkraukite puslapį.

---

## Spausdintuvas nereaguoja {#spausdintuvas}

**Problema:** Etikečių spausdintuvas nespausdina.

**Sprendimas:**
1. Patikrink, ar spausdintuvas įjungtas ir prijungtas prie tinklo (TCP:9100).
2. Sistema automatiškai bando 3 kartus. Jei nepavyksta — užklausa eina į rankinio apdorojimo eilę.
3. Pakartotinai spausdink iš: `Outbound` → `Labels` (UI: Outbound → Labels).
4. Jei problema kartojasi — kreipkis į IT pagalbos tarnybą.

---

## Projekcijų vėlavimas {#projekcijos}

**Problema:** Duomenys rodomi seni arba matoma žinutė `Refreshing...`.

**Sprendimas:**
1. Sistema naudoja asinchronines projekcijas su ≤5 sekundžių vėlavimu. Normalu palaukti kelias sekundes.
2. Jei vėlavimas daugiau nei 30 sekundžių — kreipkis į administratorių.
3. Administratorius gali patikrinti projekcijų būseną: `Operations` → `Projections` (UI: Operations → Projections).
4. Jei reikia — administratorius gali paleisti projekcijų atstatymą (`Rebuild`, UI: Rebuild).

---

## Rezervacijos negali būti atšauktos {#rezervacijos}

**Problema:** Bandau atšaukti rezervaciją, bet sistema neleidžia.

**Sprendimas:**
1. **HARD lock** (griežta rezervacija) negali būti atšaukta automatiškai — reikalinga vadovo intervencija.
2. Patikrink rezervacijos tipą: `Stock` → `Reservations` (UI: Stock → Reservations).
3. Jei rezervacija yra `HARD` — susisiek su sandėlio vadovu.
4. Jei praėjo daugiau nei 2 valandos ir HARD rezervacija nebuvo panaudota — tai gali būti sistemos klaida. Informuok administratorių.

> **Pastaba:** Sistema automatiškai atšaukia pasibaigusias HARD rezervacijas po 2 valandų tik tada, kai bus įdiegtas `ReservationTimeoutSaga` (planuojama funkcija).

---

## Kita

**Problema:** Matau klaidą su `Trace ID`.

**Sprendimas:**
1. Užsirašyk `Trace ID` (pvz. `abc-123-def`).
2. Perduok jį IT pagalbos tarnybai — jie galės surasti klaidos detalės žurnaluose.
