import assert from 'node:assert/strict';
import { test } from 'node:test';
import { chunkPage, MAX_SECTION_CHARS, pageUrlPath, parseFrontMatter } from '../src/docs-chunker.js';
import { DocsSearch, fold } from '../src/docs-search.js';
import { fakeJwt, rpc, running, testConfig } from './helpers.js';

const PAGE = `---
module: shopfloor            # warehouse | sales
title: Operatoriaus darbų eilė
routes: ["/shopfloor/operator-work-queue", "/shopfloor/x"]
roles: ["Operator", "ShopfloorManager"]
status: veikia
---

# Operatoriaus darbų eilė

**Būsena:** veikia

Įvadas prieš pirmą skyrių.

## Kam skirta
Operatorius mato savo darbų eilę.

![Darbų eilės vaizdas](../img/shopfloor/queue.png)

## Žingsniai
### 1. Atidaryti eilę
Paspauskite (UI: Open queue).
### 2. Pradėti darbą
Paspauskite (UI: Start).
`;

test('front matter: scalars, inline lists, comments; absent front matter is fine', () => {
  const { meta, body } = parseFrontMatter(PAGE);
  assert.equal(meta.module, 'shopfloor');
  assert.equal(meta.title, 'Operatoriaus darbų eilė');
  assert.deepEqual(meta.routes, ['/shopfloor/operator-work-queue', '/shopfloor/x']);
  assert.deepEqual(meta.roles, ['Operator', 'ShopfloorManager']);
  assert.ok(body.startsWith('# Operatoriaus'));
  const none = parseFrontMatter('# Antraštė\n\ntekstas');
  assert.deepEqual(none.meta, { routes: [], roles: [] });
});

test('chunkPage splits at ##, keeps text before the first ## as its own chunk, strips images but keeps alt and (UI: ...)', () => {
  const chunks = chunkPage('shopfloor/darbu-eile.md', PAGE);
  assert.deepEqual(chunks.map((c) => c.heading_path), [[], ['Kam skirta'], ['Žingsniai']]);
  assert.equal(chunks[0]?.id, 'shopfloor/darbu-eile#intro');
  assert.match(chunks[0]?.text ?? '', /Įvadas prieš pirmą skyrių/);
  assert.equal(chunks[1]?.id, 'shopfloor/darbu-eile#kam-skirta');
  assert.match(chunks[1]?.text ?? '', /Darbų eilės vaizdas/);
  assert.doesNotMatch(chunks[1]?.text ?? '', /!\[|\.png/);
  assert.equal(chunks[1]?.module, 'shopfloor');
  assert.equal(chunks[1]?.status, 'veikia');
  assert.deepEqual(chunks[1]?.routes, ['/shopfloor/operator-work-queue', '/shopfloor/x']);
  assert.equal(chunks[1]?.url_path, 'shopfloor/darbu-eile/#kam-skirta');
  assert.match(chunks[2]?.text ?? '', /\(UI: Open queue\)/);
  // stable ids
  assert.deepEqual(chunkPage('shopfloor/darbu-eile.md', PAGE).map((c) => c.id), chunks.map((c) => c.id));
});

test('a long ## section is split at ###', () => {
  const filler = 'Ilgas sakinys apie sandėlį. '.repeat(40);
  const page = `# P\n\n## Žingsniai\nĮvadas.\n### 1. Pirmas\n${filler}\n### 2. Antras\n${filler}\n`;
  assert.ok(filler.length * 2 > MAX_SECTION_CHARS);
  const chunks = chunkPage('x/p.md', page);
  assert.deepEqual(chunks.map((c) => c.heading_path), [['Žingsniai'], ['Žingsniai', '1. Pirmas'], ['Žingsniai', '2. Antras']]);
  assert.deepEqual(chunks.map((c) => c.id), ['x/p#zingsniai', 'x/p#zingsniai--1-pirmas', 'x/p#zingsniai--2-antras']);
  assert.equal(chunks[1]?.url_path, 'x/p/#1-pirmas');
});

test('page without front matter: title from # heading, module from folder, status from the status line, {#anchor} honoured, fences ignored', () => {
  const page = '# Dažnos klaidos\n\n**Būsena:** laikina (zagluška)\n\n## Prisijungimas {#prisijungimas}\nNegaliu prisijungti.\n\n```\n## ne antraštė\n```\n';
  const chunks = chunkPage('trikciu-salinimas/daznos-klaidos.md', page);
  assert.equal(chunks.length, 2);
  assert.equal(chunks[1]?.page_title, 'Dažnos klaidos');
  assert.equal(chunks[1]?.module, 'warehouse');
  assert.equal(chunks[1]?.status, 'laikina');
  assert.equal(chunks[1]?.id, 'trikciu-salinimas/daznos-klaidos#prisijungimas');
  assert.equal(chunks[1]?.url_path, 'trikciu-salinimas/daznos-klaidos/#prisijungimas');
  assert.match(chunks[1]?.text ?? '', /## ne antraštė/);
  assert.equal(pageUrlPath('index'), '');
  assert.equal(pageUrlPath('roles/index'), 'roles/');
});

const sample = () =>
  new DocsSearch({
    generated_at: '',
    git_sha: '',
    page_count: 3,
    chunk_count: 4,
    chunks: [
      ...chunkPage('procesai/p-01.md', '# Prekių priėmimas\n\n## Kam skirta\nSkirta kroviniams.\n\n## Žingsniai\nPaspauskite (UI: Create Inbound) ir suskaičiuokite sandėlyje padėtas prekes.'),
      ...chunkPage('procesai/p-02.md', '# Sandėlio žemėlapis\n\n## Kam skirta\nNaujų prekių ieškojimas žemėlapyje.'),
      ...chunkPage('sales/uzsakymai.md', '---\nmodule: sales\n---\n# Užsakymai\n\n## Kam skirta\nPardavimo užsakymų sąrašas.'),
    ],
  });

test('search folds diacritics and tolerates Lithuanian endings', () => {
  assert.equal(fold('Sandėlys ĄČĘĖĮŠŲŪŽ'), 'sandelys aceeisuuz');
  const s = sample();
  assert.ok(s.search('sandelys').total > 0); // exact fold of "sandėlys"? only "sandėlyje"/"Sandėlio" exist: prefix "sandel"
  assert.ok(s.search('sandėlio').hits.some((h) => h.chunk.page === 'procesai/p-02'));
  assert.ok(s.search('uzsakymas').hits.some((h) => h.chunk.page === 'sales/uzsakymai'));
});

test('search: module filter, a title hit outranks a body hit, snippet, empty query', () => {
  const s = sample();
  assert.deepEqual([...new Set(s.search('užsakymų', { module: 'sales' }).hits.map((h) => h.chunk.module))], ['sales']);
  assert.equal(s.search('prekių', { module: 'sales' }).total, 0);
  const ranked = s.search('žemėlapis').hits;
  assert.equal(ranked[0]?.chunk.page, 'procesai/p-02'); // title hit
  const both = s.search('prekių');
  assert.ok(both.hits.length >= 2);
  assert.equal(both.hits[0]?.chunk.page, 'procesai/p-01'); // "Prekių priėmimas" in title and body beats the lone body mention
  assert.match(s.search('Create Inbound').hits[0]?.snippet ?? '', /Create Inbound/);
  assert.deepEqual(s.search('   '), { hits: [], total: 0 });
  assert.equal(s.search('prekes', { limit: 1 }).hits.length, 1);
});

test('docs_search / docs_get through the MCP endpoint: absolute url, validation, unknown id, no role needed', async () => {
  const stand = await running(testConfig({ MANUALS_BASE_URL: 'https://manuals.example.com/' }), { docs: sample() });
  try {
    const token = fakeJwt();
    const found = await rpc(stand.base, token, 'tools/call', { name: 'docs_search', arguments: { query: 'sandėlyje prekes', limit: 3 } });
    const result = found.body.result;
    assert.equal(result.isError, undefined);
    const hit = result.structuredContent.results[0];
    assert.equal(hit.id, 'procesai/p-01#zingsniai');
    assert.equal(hit.url, 'https://manuals.example.com/procesai/p-01/#zingsniai');
    assert.ok(hit.snippet.length > 0);
    assert.ok(result.structuredContent.total >= 1);

    const got = await rpc(stand.base, token, 'tools/call', { name: 'docs_get', arguments: { id: hit.id } });
    assert.match(got.body.result.structuredContent.chunks[0].text, /Create Inbound/);
    const page = await rpc(stand.base, token, 'tools/call', { name: 'docs_get', arguments: { page: 'procesai/p-01' } });
    assert.deepEqual(page.body.result.structuredContent.chunks.map((c: { id: string }) => c.id), ['procesai/p-01#kam-skirta', 'procesai/p-01#zingsniai']);

    const unknown = await rpc(stand.base, token, 'tools/call', { name: 'docs_get', arguments: { id: 'nera#nera' } });
    assert.equal(unknown.body.result.isError, true);
    assert.equal(unknown.body.result.structuredContent.error.code, 'NOT_FOUND');
    const neither = await rpc(stand.base, token, 'tools/call', { name: 'docs_get', arguments: {} });
    assert.equal(neither.body.result.structuredContent.error.code, 'INVALID_ARGUMENTS');
    const empty = await rpc(stand.base, token, 'tools/call', { name: 'docs_search', arguments: { query: '  ' } });
    assert.equal(empty.body.result.structuredContent.error.code, 'INVALID_ARGUMENTS');
    const tooMany = await rpc(stand.base, token, 'tools/call', { name: 'docs_search', arguments: { query: 'x', limit: 16 } });
    assert.equal(tooMany.body.result.structuredContent.error.code, 'INVALID_ARGUMENTS');
  } finally {
    await stand.close();
  }
});
