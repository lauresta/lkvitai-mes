// Run: node --test tests/BuildingBlocks/js
import assert from 'node:assert/strict';
import { afterEach, describe, it } from 'node:test';
import {
    MANUAL_LIMIT, askManuals, fold, manualUrl, parseIndex, resetManualIndex, score, searchManuals,
} from '../../../src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/wwwroot/js/manual-search.js';

const INDEX = {
    config: { lang: ['lt'] },
    docs: [
        { location: '', title: 'Naudotojo gidas', text: 'Pradžia' },
        { location: 'warehouse/priemimas/', title: 'Priėmimas', text: 'Prekių priėmimas į sandėlį.' },
        { location: 'warehouse/priemimas/#naujas-priemimas', title: 'Naujas priėmimas', text: 'Spauskite Naujas.' },
        { location: 'sales/uzsakymai/', title: 'Užsakymų sąrašas', text: 'Užsakymai ir priėmimo data.' },
    ],
};

describe('fold and score', () => {
    it('ignores case and diacritics', () => {
        assert.equal(fold('Tiekėjų ŠVIESA'), 'tiekeju sviesa');
        assert.equal(fold(undefined), '');
    });

    it('ranks exact, prefix, later word and inner matches', () => {
        assert.equal(score('Priėmimas', 'priemimas'), 1);
        assert.equal(score('Priėmimas prekių', 'priem'), 0.9);
        assert.equal(score('Naujas priėmimas', 'priem'), 0.75);
        assert.equal(score('Atpriėmimas', 'priem'), 0.5);
        assert.equal(score('Užsakymai', 'priem'), 0);
        assert.equal(score('Užsakymai', '  '), 0);
    });
});

describe('manualUrl', () => {
    it('roots locations under /manuals/', () => {
        assert.equal(manualUrl('warehouse/priemimas/#a'), '/manuals/warehouse/priemimas/#a');
        assert.equal(manualUrl('/sales/'), '/manuals/sales/');
        assert.equal(manualUrl(''), '/manuals/');
    });

    it('refuses locations that could leave the manuals or change origin', () => {
        for (const bad of ['../x', 'a/../b', 'https://evil.test/', 'a\\b', '%2e%2e/x', 42, null]) {
            assert.equal(manualUrl(bad), null, String(bad));
        }
    });
});

describe('parseIndex', () => {
    it('keeps well-formed documents and drops the rest', () => {
        const docs = parseIndex({ docs: [...INDEX.docs, { location: 5, title: 'x' }, { location: 'a/', title: '' }, null, { location: '../b', title: 'Bad' }] });
        assert.equal(docs.length, 4);
        assert.equal(docs[1].url, '/manuals/warehouse/priemimas/');
    });

    it('returns an empty list for anything that is not an index', () => {
        for (const junk of [null, undefined, 'x', {}, { docs: 'x' }]) assert.deepEqual(parseIndex(junk), []);
    });
});

describe('searchManuals', () => {
    const docs = parseIndex(INDEX);

    it('finds pages without diacritics, best title match first', () => {
        const hits = searchManuals(docs, 'priemimas');
        assert.equal(hits[0].title, 'Priėmimas');
        assert.equal(hits[0].kind, 'manual');
        assert.equal(hits[0].url, '/manuals/warehouse/priemimas/');
        assert.ok(hits.some(hit => hit.url.endsWith('#naujas-priemimas')));
    });

    it('shows the page title under a section hit', () => {
        const section = searchManuals(docs, 'naujas priemimas').find(hit => hit.url.includes('#'));
        assert.equal(section.subtitle, 'Priėmimas');
    });

    it('matches body text with a lower score than titles', () => {
        const hits = searchManuals(docs, 'sandėlį');
        assert.equal(hits.length, 1);
        assert.ok(hits[0].score < 0.5);
    });

    it('returns at most the limit and nothing for an empty or unknown query', () => {
        const many = Array.from({ length: 20 }, (_, i) => ({ location: `p${i}/`, title: `Priėmimas ${i}`, text: '', url: `/manuals/p${i}/` }));
        assert.equal(searchManuals(many, 'priem').length, MANUAL_LIMIT);
        assert.deepEqual(searchManuals(docs, ' '), []);
        assert.deepEqual(searchManuals(docs, 'zzzz'), []);
    });
});

describe('askManuals', () => {
    const realFetch = globalThis.fetch;
    afterEach(() => {
        globalThis.fetch = realFetch;
        resetManualIndex();
    });

    const reply = (status, body, type = 'basic') => ({ ok: status >= 200 && status < 300, status, type, json: async () => body });

    it('downloads the index once and answers from it', async () => {
        let calls = 0;
        globalThis.fetch = async () => { calls++; return reply(200, INDEX); };
        const first = await askManuals('/manuals/search/search_index.json', 'priemimas', 8, 1000);
        const second = await askManuals('/manuals/search/search_index.json', 'uzsakymu', 8, 1000);
        assert.equal(calls, 1);
        assert.equal(first.failed, false);
        assert.equal(first.hits[0].title, 'Priėmimas');
        assert.equal(second.hits[0].url, '/manuals/sales/uzsakymai/');
    });

    it('fails silently on 401, 404, a sign-in redirect, bad JSON and network errors', async () => {
        const failures = [
            async () => reply(401, null),
            async () => reply(404, null),
            async () => reply(0, null, 'opaqueredirect'),
            async () => ({ ok: true, status: 200, type: 'basic', json: async () => { throw new SyntaxError('bad'); } }),
            async () => { throw new TypeError('offline'); },
        ];
        for (const failure of failures) {
            resetManualIndex();
            globalThis.fetch = failure;
            assert.deepEqual(await askManuals('/manuals/search/search_index.json', 'priem', 8, 1000), { hits: [], failed: false });
        }
    });

    it('retries after a failure instead of caching it', async () => {
        globalThis.fetch = async () => reply(404, null);
        await askManuals('/manuals/search/search_index.json', 'priem', 8, 1000);
        globalThis.fetch = async () => reply(200, INDEX);
        const answer = await askManuals('/manuals/search/search_index.json', 'priem', 8, 1000);
        assert.ok(answer.hits.length > 0);
    });
});
