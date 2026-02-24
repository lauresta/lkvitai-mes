# LKvitai MES — Naudotojo gidas (Vadovų šaltinis)

Šiame kataloge yra **naudotojams skirti vadovai** lietuvių kalba LKvitai.MES sandėlio valdymo sistemai.

> **Vidiniai procesų dokumentai (anglų k.)** yra [`docs/process/`](../process/README.md).

---

## Struktūra

```
docs/manuals/
├── mkdocs.yml          ← MkDocs konfigūracija
├── README.md           ← šis failas
└── docs/
    ├── index.md        ← pagrindinis puslapis su visu procesų sąrašu (oglavlenie)
    ├── roles/          ← rolių aprašymai
    │   ├── sandelio-operatorius.md
    │   ├── kokybes-inspektorius.md
    │   └── gamybos-vadovas.md
    ├── procesai/       ← proceso vadovai P-01..P-15
    │   ├── P-01-priemimas-inbound.md
    │   ├── P-02-padeti-i-lokacija-putaway.md
    │   └── ...
    └── trikciu-salinimas/
        ├── index.md
        └── daznos-klaidos.md
```

## Kaip naudoti

### Lokalus kūrimas

```bash
pip install mkdocs-material
cd docs/manuals
mkdocs serve
# Atidaryk http://127.0.0.1:8000
```

### Statinis eksportas

```bash
cd docs/manuals
mkdocs build
# Išvestis: docs/manuals/site/
```

## Taisyklės

- Visi naudotojo žingsniai **lietuvių kalba**
- Kiekvienas UI mygtukas / meniu punktas pateikiamas **angliškai skliausteliuose**: pvz. `(UI: Create)`, `(UI: Admin > Lots)`
- Nekurk žingsnių be pagrindo iš sistemos (neišgalvok)
- Procesų numeriai `P-01..P-15` nekinta — jie atitinka `docs/process/processes/` katalogo struktūrą

## CI/CD ir test aplinka

### Lokalios komandos

```bash
cd docs/manuals
mkdocs serve
mkdocs build
```

- `mkdocs serve` paleidzia lokalu perziuros serveri.
- `mkdocs build` sugeneruoja statini turini i `docs/manuals/site/`.

### Test aplinka

- Manuals svetaine test aplinkoje pasiekiama adresu: `http://<test-host>:5002/`.
