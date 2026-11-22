// Portal "Search everything" (issue #375). Loaded by PortalModuleShell.razor.
//
// The whole interaction lives in the browser: the top bar of whichever module is open asks every
// module's same-origin relay ({PathBase}/shell/search) in parallel with the user's Portal cookie and
// draws the hits grouped by kind. Nothing here goes through the Blazor circuit, so typing, arrow
// keys and Enter never wait for a server round trip.
//
// Rules: debounce before querying, ignore answers to an older query, give each module its own
// timeout, treat 401/403 (and a redirect to the sign-in page) as "no access, no group", and report
// other failures as one quiet note while still showing what the other modules returned.
//
// The 'manual' kind is searched in the browser over the MkDocs index (manual-search.js), not by a relay.
//
// With an empty box the panel shows what this user opened and searched last (kept in localStorage,
// which may be unavailable: every access is guarded and the panel works without it).

// Loaded with this module's own ?v= query (the build version) so a deploy is not hidden behind a cached
// copy: static assets are cached by the browser and Cloudflare for hours.
let manualSearch = null;
function askManuals(...args) {
    manualSearch ??= import('./manual-search.js' + new URL(import.meta.url).search);
    return manualSearch.then(m => m.askManuals(...args));
}

const KIND_ORDER = ['order', 'customer', 'item', 'supplier', 'location', 'document', 'screen', 'manual'];
const RECENT_LIMIT = 5;
const instances = new WeakMap();

/** Only same-origin absolute paths are ever opened. */
export function isSafeUrl(url) {
    return typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') && !url.includes('\\');
}

function isHit(hit) {
    return Boolean(hit) && typeof hit.kind === 'string' && hit.kind !== ''
        && typeof hit.title === 'string' && hit.title !== '' && isSafeUrl(hit.url);
}

/** Keeps well-formed hits only. */
export function sanitize(hits) {
    return Array.isArray(hits) ? hits.filter(isHit) : [];
}

function kindRank(kind) {
    const index = KIND_ORDER.indexOf(kind);
    return index < 0 ? KIND_ORDER.length : index;
}

/** Groups hits by kind in display order, best score first, at most `limit` per kind. */
export function groupHits(hits, limit) {
    const groups = new Map();
    for (const hit of hits) {
        if (!groups.has(hit.kind)) groups.set(hit.kind, []);
        const list = groups.get(hit.kind);
        if (!list.some(x => x.url === hit.url && x.title === hit.title)) list.push(hit);
    }
    return [...groups.entries()]
        .sort((a, b) => kindRank(a[0]) - kindRank(b[0]))
        .map(([kind, list]) => ({
            kind,
            // Array.prototype.sort is stable: equal scores keep the order the module returned.
            hits: [...list].sort((a, b) => (b.score ?? 0) - (a.score ?? 0)).slice(0, limit),
        }));
}

/** Puts `entry` first in `list`, drops its older copy and keeps at most RECENT_LIMIT entries. */
export function remember(list, entry, sameAs) {
    return [entry, ...list.filter(x => !sameAs(x, entry))].slice(0, RECENT_LIMIT);
}

/**
 * Per-user history in localStorage: the last opened results and the last queries.
 * Storage can be blocked, full or cleared at any time, so every access is guarded and a failure
 * simply means "no history".
 */
export function createRecentStore(user, storage) {
    const key = `cikada.shell-search.recent.v1.${user || 'anonymous'}`;
    const empty = () => ({ opened: [], queries: [] });

    function read() {
        try {
            const parsed = JSON.parse(storage.getItem(key) ?? 'null');
            return {
                opened: sanitize(parsed?.opened).slice(0, RECENT_LIMIT),
                queries: (Array.isArray(parsed?.queries) ? parsed.queries : [])
                    .filter(q => typeof q === 'string' && q.trim() !== '').slice(0, RECENT_LIMIT),
            };
        } catch {
            return empty();
        }
    }

    function write(value) {
        try {
            storage.setItem(key, JSON.stringify(value));
        } catch {
            // Private mode, blocked site data or a full quota: the search itself is unaffected.
        }
    }

    return {
        read,
        add(query, hit) {
            const current = read();
            const entry = { kind: hit.kind, title: hit.title, subtitle: hit.subtitle ?? null, url: hit.url };
            write({
                opened: remember(current.opened, entry, (a, b) => a.url === b.url),
                queries: query ? remember(current.queries, query, (a, b) => a.toLowerCase() === b.toLowerCase()) : current.queries,
            });
        },
        clear() {
            try {
                storage.removeItem(key);
            } catch {
                // Nothing to clear when storage is unavailable.
            }
        },
    };
}

function browserStorage() {
    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

const NO_STORAGE = { getItem: () => null, setItem: () => undefined, removeItem: () => undefined };

function element(tag, className, textContent) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (textContent !== undefined) node.textContent = textContent;
    return node;
}

function groupSection(id, label, count) {
    const section = element('section', 'shell-search__group');
    section.setAttribute('role', 'group');
    section.setAttribute('aria-labelledby', id);
    const heading = element('div', 'shell-search__heading', label);
    heading.id = id;
    if (count !== undefined) heading.append(element('span', 'shell-search__count mono', String(count)));
    section.append(heading);
    return section;
}

function optionNode(tag, index, title, subtitle) {
    const node = element(tag, 'shell-search__item');
    node.id = `shell-search-option-${index}`;
    node.setAttribute('role', 'option');
    node.setAttribute('aria-selected', 'false');
    node.dataset.shellSearchOption = String(index);
    node.tabIndex = -1;
    node.append(element('span', 'shell-search__title', title));
    if (typeof subtitle === 'string' && subtitle) node.append(element('span', 'shell-search__subtitle', subtitle));
    return node;
}

function hitOption(index, hit) {
    const link = optionNode('a', index, hit.title, hit.subtitle);
    link.href = hit.url;
    link.dataset.kind = hit.kind;
    return link;
}

function queryOption(index, query) {
    const node = optionNode('button', index, query);
    node.type = 'button';
    node.dataset.query = query;
    return node;
}

function hitFromNode(node) {
    return {
        kind: node.dataset.kind || 'screen',
        title: node.querySelector('.shell-search__title')?.textContent ?? '',
        subtitle: node.querySelector('.shell-search__subtitle')?.textContent ?? null,
        url: node.getAttribute('href'),
    };
}

async function askModule(endpoint, query, limit, timeoutMs, controller) {
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    try {
        const separator = endpoint.includes('?') ? '&' : '?';
        const response = await fetch(`${endpoint}${separator}q=${encodeURIComponent(query)}&limit=${limit}`, {
            credentials: 'same-origin',
            redirect: 'manual',
            headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            signal: controller.signal,
        });
        // Not signed in (401, or a redirect to the sign-in page) / no role in that module:
        // no group, and nothing to report.
        if (response.status === 401 || response.status === 403 || response.type === 'opaqueredirect') {
            return { hits: [], failed: false };
        }
        if (!response.ok) return { hits: [], failed: true };
        const hits = sanitize(await response.json());
        return { hits, failed: response.headers.get('X-Shell-Search-Partial') === '1' };
    } catch {
        // Timed out, offline, module down, or superseded by a newer query (then the answer is ignored).
        return { hits: [], failed: true };
    } finally {
        clearTimeout(timeout);
    }
}

export function init(button, panel, options) {
    if (!button || !panel || instances.has(button)) return;

    const input = panel.querySelector('[data-shell-search-input]');
    const status = panel.querySelector('[data-shell-search-status]');
    const results = panel.querySelector('[data-shell-search-results]');
    const scopes = panel.querySelector('[data-shell-search-scopes]');
    const closeButton = panel.querySelector('[data-shell-search-close]');
    const hint = button.querySelector('[data-shell-search-hint]');
    if (!input || !status || !results) return;

    const endpoints = Array.isArray(options?.endpoints) ? options.endpoints.filter(isSafeUrl) : [];
    const manualsIndexUrl = isSafeUrl(options?.manualsIndexUrl) ? options.manualsIndexUrl : null;
    const minLength = options?.minLength ?? 2;
    const debounceMs = options?.debounceMs ?? 250;
    const timeoutMs = options?.timeoutMs ?? 5000;
    const limit = options?.limit ?? 8;
    const text = options?.text ?? {};
    const kindLabels = text.kinds ?? {};
    const recent = createRecentStore(options?.user, options?.storage ?? browserStorage() ?? NO_STORAGE);

    let sequence = 0;
    let timer = null;
    let controllers = [];
    let active = -1;
    let scope = '';
    let lastHits = [];

    if (hint) hint.textContent = /Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent || '') ? '⌘K' : 'Ctrl K';

    const optionNodes = () => [...results.querySelectorAll('[data-shell-search-option]')];
    const isOpen = () => !panel.hidden;
    const kindLabel = kind => kindLabels[kind] ?? text.otherKind ?? kind;

    function setStatus(message) {
        status.textContent = message || '';
        status.hidden = !message;
    }

    function setActive(index, scroll) {
        const nodes = optionNodes();
        active = nodes.length === 0 ? -1 : Math.max(-1, Math.min(index, nodes.length - 1));
        nodes.forEach((node, i) => {
            node.classList.toggle('is-active', i === active);
            node.setAttribute('aria-selected', i === active ? 'true' : 'false');
        });
        if (active < 0) {
            input.removeAttribute('aria-activedescendant');
            return;
        }
        input.setAttribute('aria-activedescendant', nodes[active].id);
        if (scroll) nodes[active].scrollIntoView({ block: 'nearest' });
    }

    function cancelPending() {
        sequence++;
        if (timer) { clearTimeout(timer); timer = null; }
        controllers.forEach(c => c.abort());
        controllers = [];
    }

    function scopeChip(kind, label) {
        const chip = element('button', 'shell-search__scope', label);
        chip.type = 'button';
        chip.dataset.shellSearchScope = kind;
        chip.setAttribute('aria-pressed', kind === scope ? 'true' : 'false');
        return chip;
    }

    /** Chips: "All" plus one per kind that has results. Hidden when there is nothing to choose. */
    function renderScopes(kinds) {
        if (!scopes) return;
        if (!kinds.includes(scope)) scope = '';
        scopes.replaceChildren();
        scopes.hidden = kinds.length < 2;
        if (scopes.hidden) return;
        scopes.append(scopeChip('', text.scopeAll ?? ''), ...kinds.map(kind => scopeChip(kind, kindLabel(kind))));
    }

    function render(hits) {
        lastHits = hits;
        // Keep the row the user moved to when another module answers and the list is redrawn.
        const previous = active >= 0 ? optionNodes()[active]?.getAttribute('href') : null;
        const groups = groupHits(hits, limit);
        renderScopes(groups.map(group => group.kind));
        results.replaceChildren();
        let index = 0;
        for (const group of groups.filter(g => scope === '' || g.kind === scope)) {
            const section = groupSection(`shell-search-kind-${group.kind}`, kindLabel(group.kind), group.hits.length);
            for (const hit of group.hits) section.append(hitOption(index++, hit));
            results.append(section);
        }
        const kept = previous ? optionNodes().findIndex(node => node.getAttribute('href') === previous) : -1;
        setActive(index > 0 ? Math.max(kept, 0) : -1, false);
        return index;
    }

    /** Empty box: what this user opened and searched last, plus a way to forget it. */
    function renderRecent() {
        lastHits = [];
        renderScopes([]);
        results.replaceChildren();
        const { opened, queries } = recent.read();
        let index = 0;
        if (opened.length > 0) {
            const section = groupSection('shell-search-recent-opened', text.recentOpened ?? '');
            for (const hit of opened) section.append(hitOption(index++, hit));
            results.append(section);
        }
        if (queries.length > 0) {
            const section = groupSection('shell-search-recent-queries', text.recentQueries ?? '');
            for (const query of queries) section.append(queryOption(index++, query));
            results.append(section);
        }
        if (index > 0) {
            const clear = element('button', 'link-button shell-search__clear', text.recentClear ?? '');
            clear.type = 'button';
            clear.dataset.shellSearchClear = '';
            results.append(clear);
        }
        setActive(-1, false);
        setStatus(text.hint);
    }

    function finish(shown, failed) {
        panel.removeAttribute('aria-busy');
        if (shown === 0) setStatus(failed ? `${text.empty} ${text.partial}` : text.empty);
        else setStatus(failed ? text.partial : text.keys);
    }

    function search(raw) {
        cancelPending();
        const query = raw.trim();
        if (query.length < minLength) {
            panel.removeAttribute('aria-busy');
            renderRecent();
            return;
        }

        const mine = sequence;
        const hits = [];
        let failed = false;
        const sources = endpoints.map(endpoint => controller => askModule(endpoint, query, limit, timeoutMs, controller));
        if (manualsIndexUrl) sources.push(() => askManuals(manualsIndexUrl, query, limit, timeoutMs));
        let pending = sources.length;
        panel.setAttribute('aria-busy', 'true');
        // Rows of the previous query stay visible until the first answer, but Enter must not open one.
        setActive(-1, false);
        setStatus(text.loading);

        const settle = answer => {
            if (mine !== sequence) return;
            pending--;
            hits.push(...answer.hits);
            failed = failed || answer.failed;
            const shown = hits.length > 0 || pending <= 0 ? render(hits) : optionNodes().length;
            if (pending <= 0) finish(shown, failed);
        };

        if (pending === 0) { finish(render(hits), false); return; }
        for (const source of sources) {
            const controller = new AbortController();
            controllers.push(controller);
            source(controller)
                .then(settle)
                // Drawing an answer failed: count this module as "could not be loaded".
                .catch(() => settle({ hits: [], failed: true }));
        }
    }

    function open() {
        if (!isOpen()) {
            panel.hidden = false;
            button.setAttribute('aria-expanded', 'true');
            if (input.value.trim().length < minLength) renderRecent();
        }
        input.focus();
        input.select();
    }

    function close(restoreFocus) {
        if (!isOpen()) return;
        cancelPending();
        panel.removeAttribute('aria-busy');
        panel.hidden = true;
        button.setAttribute('aria-expanded', 'false');
        if (restoreFocus) button.focus();
    }

    function runQuery(query) {
        input.value = query;
        input.focus();
        search(query);
    }

    function go(node) {
        if (!node) return;
        if (node.dataset.query) { runQuery(node.dataset.query); return; }
        const href = node.getAttribute('href');
        if (!isSafeUrl(href)) return;
        const query = input.value.trim();
        recent.add(query.length >= minLength ? query : '', hitFromNode(node));
        close(false);
        // A full load, not a Blazor in-app navigation: the hit may live in another module, and a
        // list opened with ?search= reads the query only when the page starts.
        window.location.assign(href);
    }

    const isPlainClick = event => event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
    const closest = (event, selector) => (event.target instanceof Element ? event.target.closest(selector) : null);

    const onButtonClick = () => (isOpen() ? close(true) : open());
    const onInput = () => {
        cancelPending();
        const value = input.value;
        if (value.trim().length < minLength) { search(value); return; }
        timer = setTimeout(() => { timer = null; search(value); }, debounceMs);
    };
    const onInputKeydown = event => {
        const count = optionNodes().length;
        if (event.key === 'ArrowDown') { event.preventDefault(); setActive(active + 1 >= count ? 0 : active + 1, true); }
        else if (event.key === 'ArrowUp') { event.preventDefault(); setActive(active <= 0 ? count - 1 : active - 1, true); }
        else if (event.key === 'Enter') { event.preventDefault(); go(optionNodes()[active]); }
    };
    const onPanelKeydown = event => {
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(true); }
    };
    const onResultsClick = event => {
        if (closest(event, '[data-shell-search-clear]')) {
            recent.clear();
            renderRecent();
            input.focus();
            return;
        }
        const node = closest(event, '[data-shell-search-option]');
        if (!node || !isPlainClick(event)) return;
        event.preventDefault();
        go(node);
    };
    const onResultsMove = event => {
        const node = closest(event, '[data-shell-search-option]');
        if (node) setActive(Number(node.dataset.shellSearchOption), false);
    };
    const onScopesClick = event => {
        const chip = closest(event, '[data-shell-search-scope]');
        if (!chip) return;
        scope = chip.dataset.shellSearchScope ?? '';
        render(lastHits);
        scopes.querySelector('[aria-pressed="true"]')?.focus();
    };
    const onScopesKeydown = event => {
        if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
        const chips = [...scopes.querySelectorAll('[data-shell-search-scope]')];
        const current = chips.indexOf(document.activeElement);
        if (current < 0) return;
        event.preventDefault();
        const step = event.key === 'ArrowRight' ? 1 : -1;
        chips[(current + step + chips.length) % chips.length].focus();
    };
    const onDocumentKeydown = event => {
        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 'k') {
            event.preventDefault();
            open();
        }
    };
    const onDocumentPointerDown = event => {
        if (!isOpen() || !(event.target instanceof Node)) return;
        if (!panel.contains(event.target) && !button.contains(event.target)) close(false);
    };
    const onCloseClick = () => close(true);

    const listeners = [
        [button, 'click', onButtonClick],
        [input, 'input', onInput],
        [input, 'keydown', onInputKeydown],
        [panel, 'keydown', onPanelKeydown],
        [results, 'click', onResultsClick],
        [results, 'mousemove', onResultsMove],
        [scopes, 'click', onScopesClick],
        [scopes, 'keydown', onScopesKeydown],
        [closeButton, 'click', onCloseClick],
        [document, 'keydown', onDocumentKeydown],
        [document, 'pointerdown', onDocumentPointerDown],
    ].filter(([target]) => Boolean(target));
    for (const [target, type, handler] of listeners) target.addEventListener(type, handler);

    instances.set(button, () => {
        cancelPending();
        for (const [target, type, handler] of listeners) target.removeEventListener(type, handler);
    });
}

export function dispose(button) {
    const teardown = button ? instances.get(button) : null;
    if (teardown) {
        teardown();
        instances.delete(button);
    }
}
