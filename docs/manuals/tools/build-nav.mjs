// Regenerates docs/index.md and the `nav:` block of mkdocs.yml from the front matter of the pages.
// Usage: node docs/manuals/tools/build-nav.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const docs = path.join(root, 'docs');

const sections = [
  { dir: 'portal', name: 'Portalas ir darbo pradžia', intro: 'Prisijungimas, kalbos keitimas, modulių plytelės.' },
  { dir: 'sales', name: 'Pardavimai (Sales)', intro: 'Užsakymų sąrašas, užsakymo informacija ir užsakymų įvedimas.' },
  { dir: 'shopfloor', name: 'Gamyba (Shopfloor)', intro: 'Gamybos eilė, gamybos užduotys, dokumentai, roletai, darbų sekos.' },
  { dir: 'procesai', name: 'Sandėlis (Warehouse) – procesai', intro: 'Priėmimas, sandėliavimas, išsiuntimas, inventorizacija, vertinimas, Agnum, administravimas.' },
  { dir: 'roles', name: 'Sandėlis (Warehouse) – rolės', intro: 'Kas ką gali daryti sandėlio modulyje.' },
  { dir: 'trikciu-salinimas', name: 'Sandėlis (Warehouse) – trikčių šalinimas', intro: 'Dažnos klaidos ir ką daryti.' },
  { dir: 'frontline', name: 'Frontline (audinių likučiai)', intro: 'Audinio likučio patikra salone ir mažų likučių sąrašas.' },
  { dir: 'finance', name: 'Finansai (Finance)', intro: 'Bankiniai pavedimai.' },
  { dir: 'scanning', name: 'Skenavimas (Scanning)', intro: 'Modulio karkasas.' },
  { dir: 'mcp', name: 'AI asistentas (Cikada MCP)', intro: 'Darbas su Cikada iš ChatGPT arba Claude.' },
];
const statusText = { veikia: 'veikia', dalinai: 'dalinai', laikina: 'laikina (zagluška)' };

function meta(file) {
  const text = fs.readFileSync(file, 'utf8').replace(/^﻿/, '');
  const m = /^---\r?\n([\s\S]*?)\r?\n---/.exec(text);
  const get = (k) => {
    const r = m && new RegExp('^' + k + ':\\s*(.*)$', 'm').exec(m[1]);
    return r ? r[1].trim().replace(/^["']|["']$/g, '') : '';
  };
  const h1 = /^#\s+(.+)$/m.exec(text);
  return { title: get('title') || (h1 ? h1[1].trim() : path.basename(file, '.md')), status: get('status') };
}

function pages(dir) {
  const full = path.join(docs, dir);
  if (!fs.existsSync(full)) return [];
  return fs.readdirSync(full).filter((f) => f.endsWith('.md'))
    .sort((a, b) => (a === 'index.md' ? -1 : b === 'index.md' ? 1 : a.localeCompare(b)))
    .map((f) => ({ rel: dir + '/' + f, file: f, ...meta(path.join(full, f)) }));
}

const yq = (s) => '"' + s.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
let nav = 'nav:\n  - Pagrindinis: index.md\n';
let index = `# Cikada.MES – naudotojo vadovas

Čia aprašyti visi Cikada.MES (LKvitai.MES) moduliai: ką kiekvienas ekranas daro, kokiais žingsniais atliekamas darbas ir kas dar neįgyvendinta.

> **Kalba.** Vadovai parašyti lietuviškai. Mygtukų, meniu ir laukų pavadinimai pateikiami taip, kaip jie matomi ekrane, pvz. \`(UI: Create Inbound)\`.

## Kaip skaityti būseną

Kiekvieno puslapio pradžioje nurodyta būsena:

- **veikia** – ekranas veikia nuo pradžios iki galo;
- **dalinai** – dalis funkcijų veikia, puslapyje išvardyta, kas ne;
- **laikina (zagluška)** – ekranas yra, bet rodo pavyzdinius duomenis arba mygtukai dar nieko nedaro.

Tie patys vadovai pasiekiami ir per AI asistentą (Cikada MCP): asistentas ieško šiuose puslapiuose ir atsako pagal juos.

`;
const counts = { veikia: 0, dalinai: 0, laikina: 0 };
for (const s of sections) {
  const list = pages(s.dir);
  if (!list.length) continue;
  nav += `\n  - ${yq(s.name)}:\n` + list.map((p) => `    - ${yq(p.title)}: ${p.rel}\n`).join('');
  index += `## ${s.name}\n\n${s.intro}\n\n| Puslapis | Būsena |\n|---|---|\n`;
  for (const p of list) {
    if (counts[p.status] !== undefined) counts[p.status]++;
    index += `| [${p.title.replace(/\|/g, '\\|')}](${p.rel}) | ${statusText[p.status] || '-'} |\n`;
  }
  index += '\n';
}
fs.writeFileSync(path.join(docs, 'index.md'), index);

const ymlPath = path.join(root, 'mkdocs.yml');
const yml = fs.readFileSync(ymlPath, 'utf8');
const nl = yml.includes('\r\n') ? '\r\n' : '\n';
const lines = yml.split(/\r?\n/);
const start = lines.findIndex((l) => /^nav:/.test(l));
let end = lines.findIndex((l, i) => i > start && /^\S/.test(l));
if (end < 0) end = lines.length;
lines.splice(start, end - start, ...nav.trimEnd().split('\n'), '');
fs.writeFileSync(ymlPath, lines.join(nl));
console.log(JSON.stringify(counts));
