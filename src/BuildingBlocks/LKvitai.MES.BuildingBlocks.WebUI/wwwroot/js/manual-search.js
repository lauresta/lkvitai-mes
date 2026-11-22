// Universal search, kind "manual": a browser-side search over the MkDocs (lunr) index the manuals
// site publishes at /manuals/search/search_index.json. The manuals are served under the same origin
// as the apps (behind the portal cookie), so the index is fetched with the user's cookie and
// searched here; nothing goes through the Blazor circuit or an API.
//
// The matcher mirrors BuildingBlocks SearchText (C#): case- and diacritics-insensitive, so "priemimas"
// finds "Priėmimas". Any failure to load the index (401, 404, offline, a redirect to sign-in,
// malformed JSON) means "no manual hits" and is never reported.

export const MANUAL_KIND = 'manual';
export const MANUAL_LIMIT = 5;
const MANUALS_ROOT = '/manuals/';
const SNIPPET_LENGTH = 120;
const TEXT_WEIGHT = 0.4;

/** Lower-cases and strips combining marks: "Tiekėjų" -> "tiekeju". */
export function fold(value) {
    if (typeof value !== 'string' || value === '') return '';
    return value.normalize('NFD').replace(/\p{M}+/gu, '').toLowerCase().normalize('NFC');
}

/** 1 exact, 0.9 prefix, 0.75 start of a later word, 0.5 anywhere, 0 no match (same scale as SearchText.Score). */
export function score(text, query) {
    const needle = fold(query).trim();
    if (needle === '') return 0;
    const haystack = fold(text).trim();
    let index = haystack.indexOf(needle);
    if (index < 0) return 0;
    if (haystack.length === needle.length) return 1;
    if (index === 0) return 0.9;
    while (index >= 0) {
        if (!/[\p{L}\p{N}]/u.test(haystack[index - 1])) return 0.75;
        index = haystack.indexOf(needle, index + 1);
    }
    return 0.5;
}

/** A page location from the index ("sales/orders/#new") that may be appended to /manuals/ - or null. */
export function manualUrl(location) {
    if (typeof location !== 'string') return null;
    const trimmed = location.replace(/^\/+/, '');
    if (/[\\:%]/.test(trimmed) || trimmed.split(/[/#?]/).includes('..')) return null;
    return MANUALS_ROOT + trimmed;
}

/** Keeps the well-formed documents of a MkDocs search_index.json; anything else yields an empty list. */
export function parseIndex(json) {
    const docs = Array.isArray(json?.docs) ? json.docs : [];
    return docs
        .filter(doc => doc && typeof doc.location === 'string' && typeof doc.title === 'string' && doc.title !== '')
        .map(doc => ({
            location: doc.location,
            title: doc.title,
            text: typeof doc.text === 'string' ? doc.text : '',
            url: manualUrl(doc.location),
        }))
        .filter(doc => doc.url !== null);
}

function snippet(text, query) {
    const plain = text.replace(/\s+/g, ' ').trim();
    if (plain === '') return null;
    const at = fold(plain).indexOf(fold(query).trim());
    const start = Math.max(0, at - 30);
    const part = plain.slice(start, start + SNIPPET_LENGTH).trim();
    return (start > 0 ? '… ' : '') + part;
}

function subtitleOf(doc, pageTitles, query) {
    const page = pageTitles.get(doc.location.split('#')[0]);
    return page && page !== doc.title ? page : snippet(doc.text, query);
}

/** At most `limit` hits (kind "manual"), best first; sections show their page title as subtitle. */
export function searchManuals(docs, query, limit = MANUAL_LIMIT) {
    if (!Array.isArray(docs) || fold(query).trim() === '') return [];
    const pageTitles = new Map(docs.filter(doc => !doc.location.includes('#')).map(doc => [doc.location, doc.title]));
    return docs
        .map(doc => ({ doc, value: Math.max(score(doc.title, query), score(doc.text, query) * TEXT_WEIGHT) }))
        .filter(match => match.value > 0)
        .sort((a, b) => b.value - a.value)
        .slice(0, limit)
        .map(({ doc, value }) => ({
            kind: MANUAL_KIND,
            title: doc.title,
            subtitle: subtitleOf(doc, pageTitles, query),
            url: doc.url,
            score: value,
        }));
}

let cache = null;

/** Test hook: forget the downloaded index. */
export function resetManualIndex() {
    cache = null;
}

async function loadIndex(indexUrl, timeoutMs) {
    // Own controller: the index is shared by all queries, so a newer keystroke must not abort it.
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    try {
        const response = await fetch(indexUrl, {
            credentials: 'same-origin',
            redirect: 'manual',
            headers: { Accept: 'application/json' },
            signal: controller.signal,
        });
        // 401/403/404, or a redirect to the sign-in page: the manuals are simply not available.
        if (!response.ok || response.type === 'opaqueredirect') return null;
        return parseIndex(await response.json());
    } finally {
        clearTimeout(timeout);
    }
}

/** Same answer shape as a module relay, but a failure is never reported (the manuals are optional). */
export async function askManuals(indexUrl, query, limit, timeoutMs) {
    try {
        cache ??= loadIndex(indexUrl, timeoutMs);
        const docs = await cache;
        if (docs === null) cache = null;
        return { hits: docs ? searchManuals(docs, query, Math.min(limit, MANUAL_LIMIT)) : [], failed: false };
    } catch {
        cache = null;
        return { hits: [], failed: false };
    }
}
