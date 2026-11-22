// Portal "Search everything" (issue #375). Loaded by PortalModuleShell.razor.
//
// The whole interaction lives in the browser: the top bar of whichever module is open asks every
// module's same-origin relay ({PathBase}/shell/search) in parallel with the user's Portal cookie and
// draws the hits grouped by kind. Nothing here goes through the Blazor circuit, so typing, arrow
// keys and Enter never wait for a server round trip.
//
// Rules: debounce before querying, ignore answers to an older query, give each module its own
// timeout, treat 401/403 as "no access, no group", and report other failures as one quiet note
// while still showing what the other modules returned.

const KIND_ORDER = ['order', 'customer', 'item', 'supplier', 'screen'];
const instances = new WeakMap();

/** Only same-origin absolute paths are ever opened. */
export function isSafeUrl(url) {
    return typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') && !url.includes('\\');
}

/** Keeps well-formed hits only. */
export function sanitize(hits) {
    if (!Array.isArray(hits)) return [];
    return hits.filter(h => h && typeof h.kind === 'string' && h.kind
        && typeof h.title === 'string' && h.title && isSafeUrl(h.url));
}

/** Groups hits by kind in display order, best score first, at most `limit` per kind. */
export function groupHits(hits, limit) {
    const groups = new Map();
    for (const hit of hits) {
        if (!groups.has(hit.kind)) groups.set(hit.kind, []);
        const list = groups.get(hit.kind);
        if (!list.some(x => x.url === hit.url && x.title === hit.title)) list.push(hit);
    }
    const rank = kind => { const i = KIND_ORDER.indexOf(kind); return i < 0 ? KIND_ORDER.length : i; };
    return [...groups.entries()]
        .sort((a, b) => rank(a[0]) - rank(b[0]))
        .map(([kind, list]) => ({
            kind,
            // Array.prototype.sort is stable: equal scores keep the order the module returned.
            hits: [...list].sort((a, b) => (b.score ?? 0) - (a.score ?? 0)).slice(0, limit),
        }));
}

export function init(button, panel, options) {
    if (!button || !panel || instances.has(button)) return;

    const input = panel.querySelector('[data-shell-search-input]');
    const status = panel.querySelector('[data-shell-search-status]');
    const results = panel.querySelector('[data-shell-search-results]');
    const closeButton = panel.querySelector('[data-shell-search-close]');
    const hint = button.querySelector('[data-shell-search-hint]');
    if (!input || !status || !results) return;

    const endpoints = Array.isArray(options?.endpoints) ? options.endpoints.filter(isSafeUrl) : [];
    const minLength = options?.minLength ?? 2;
    const debounceMs = options?.debounceMs ?? 250;
    const timeoutMs = options?.timeoutMs ?? 2000;
    const limit = options?.limit ?? 8;
    const text = options?.text ?? {};
    const kindLabels = text.kinds ?? {};

    let sequence = 0;
    let timer = null;
    let controllers = [];
    let active = -1;

    if (hint) hint.textContent = /Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent || '') ? '⌘K' : 'Ctrl K';

    const optionNodes = () => [...results.querySelectorAll('[data-shell-search-option]')];
    const isOpen = () => !panel.hidden;

    function setStatus(message) {
        status.textContent = message || '';
        status.hidden = !message;
    }

    function setActive(index, scroll) {
        const nodes = optionNodes();
        active = nodes.length === 0 ? -1 : Math.max(0, Math.min(index, nodes.length - 1));
        nodes.forEach((node, i) => {
            node.classList.toggle('is-active', i === active);
            node.setAttribute('aria-selected', i === active ? 'true' : 'false');
        });
        if (active >= 0) {
            input.setAttribute('aria-activedescendant', nodes[active].id);
            if (scroll) nodes[active].scrollIntoView({ block: 'nearest' });
        } else {
            input.removeAttribute('aria-activedescendant');
        }
    }

    function cancelPending() {
        sequence++;
        if (timer) { clearTimeout(timer); timer = null; }
        controllers.forEach(c => c.abort());
        controllers = [];
    }

    function render(hits) {
        // Keep the row the user moved to when another module answers and the list is redrawn.
        const previous = active >= 0 ? optionNodes()[active]?.getAttribute('href') : null;
        results.replaceChildren();
        let optionIndex = 0;
        for (const group of groupHits(hits, limit)) {
            const section = document.createElement('section');
            section.className = 'shell-search__group';
            const headingId = `shell-search-kind-${group.kind}`;
            section.setAttribute('role', 'group');
            section.setAttribute('aria-labelledby', headingId);

            const heading = document.createElement('div');
            heading.className = 'shell-search__heading';
            heading.id = headingId;
            heading.append(kindLabels[group.kind] ?? text.otherKind ?? group.kind);
            const count = document.createElement('span');
            count.className = 'shell-search__count mono';
            count.textContent = String(group.hits.length);
            heading.append(count);
            section.append(heading);

            for (const hit of group.hits) {
                const link = document.createElement('a');
                link.className = 'shell-search__item';
                link.id = `shell-search-option-${optionIndex}`;
                link.href = hit.url;
                link.setAttribute('role', 'option');
                link.setAttribute('aria-selected', 'false');
                link.dataset.shellSearchOption = String(optionIndex);
                link.tabIndex = -1;

                const title = document.createElement('span');
                title.className = 'shell-search__title';
                title.textContent = hit.title;
                link.append(title);
                if (typeof hit.subtitle === 'string' && hit.subtitle) {
                    const subtitle = document.createElement('span');
                    subtitle.className = 'shell-search__subtitle';
                    subtitle.textContent = hit.subtitle;
                    link.append(subtitle);
                }
                section.append(link);
                optionIndex++;
            }
            results.append(section);
        }
        const kept = previous ? optionNodes().findIndex(node => node.getAttribute('href') === previous) : -1;
        setActive(optionIndex > 0 ? Math.max(kept, 0) : -1, false);
        return optionIndex;
    }

    async function askModule(endpoint, query, mine) {
        const controller = new AbortController();
        controllers.push(controller);
        const timeout = setTimeout(() => controller.abort(), timeoutMs);
        try {
            const separator = endpoint.includes('?') ? '&' : '?';
            const response = await fetch(`${endpoint}${separator}q=${encodeURIComponent(query)}&limit=${limit}`, {
                credentials: 'same-origin',
                redirect: 'manual',
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                signal: controller.signal,
            });
            // Not signed in / no role in that module: no group, and nothing to report.
            if (response.status === 401 || response.status === 403) return { hits: [], failed: false };
            if (!response.ok) return { hits: [], failed: true };
            const hits = sanitize(await response.json());
            return { hits, failed: response.headers.get('X-Shell-Search-Partial') === '1' };
        } catch {
            // Timed out, offline, module down, or superseded by a newer query.
            return { hits: [], failed: mine === sequence };
        } finally {
            clearTimeout(timeout);
        }
    }

    function search(raw) {
        cancelPending();
        const query = raw.trim();
        if (query.length < minLength) {
            results.replaceChildren();
            setActive(-1, false);
            setStatus(text.hint);
            return;
        }

        const mine = sequence;
        const hits = [];
        let failed = false;
        let pending = endpoints.length;
        panel.setAttribute('aria-busy', 'true');
        // Rows of the previous query stay visible until the first answer, but Enter must not open one.
        setActive(-1, false);
        setStatus(text.loading);

        const settle = () => {
            if (mine !== sequence) return;
            const shown = hits.length > 0 || pending === 0 ? render(hits) : optionNodes().length;
            if (pending > 0) return;
            panel.removeAttribute('aria-busy');
            if (shown === 0) setStatus(failed ? `${text.empty} ${text.partial}` : text.empty);
            else setStatus(failed ? text.partial : text.keys);
        };

        if (pending === 0) { settle(); return; }
        for (const endpoint of endpoints) {
            askModule(endpoint, query, mine).then(answer => {
                if (mine !== sequence) return;
                pending--;
                hits.push(...answer.hits);
                failed = failed || answer.failed;
                settle();
            }).catch(() => {
                // Drawing an answer failed: finish this module as "could not be loaded".
                if (mine !== sequence) return;
                failed = true;
                panel.removeAttribute('aria-busy');
                setStatus(text.partial);
            });
        }
    }

    function open() {
        if (isOpen()) { input.focus(); input.select(); return; }
        panel.hidden = false;
        button.setAttribute('aria-expanded', 'true');
        input.focus();
        input.select();
        if (input.value.trim().length < minLength) setStatus(text.hint);
    }

    function close(restoreFocus) {
        if (!isOpen()) return;
        cancelPending();
        panel.removeAttribute('aria-busy');
        panel.hidden = true;
        button.setAttribute('aria-expanded', 'false');
        if (restoreFocus) button.focus();
    }

    function go(node) {
        const href = node?.getAttribute('href');
        if (!isSafeUrl(href)) return;
        close(false);
        // A full load, not a Blazor in-app navigation: the hit may live in another module, and a
        // list opened with ?search= reads the query only when the page starts.
        window.location.assign(href);
    }

    const onButtonClick = () => (isOpen() ? close(true) : open());
    const onInput = () => {
        cancelPending();
        const value = input.value;
        if (value.trim().length < minLength) { search(value); return; }
        timer = setTimeout(() => { timer = null; search(value); }, debounceMs);
    };
    const onInputKeydown = event => {
        const nodes = optionNodes();
        if (event.key === 'ArrowDown') { event.preventDefault(); setActive(active + 1 >= nodes.length ? 0 : active + 1, true); }
        else if (event.key === 'ArrowUp') { event.preventDefault(); setActive(active <= 0 ? nodes.length - 1 : active - 1, true); }
        else if (event.key === 'Enter') { event.preventDefault(); if (active >= 0 && nodes[active]) go(nodes[active]); }
    };
    const onPanelKeydown = event => {
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(true); }
    };
    const onResultsClick = event => {
        const node = event.target instanceof Element ? event.target.closest('[data-shell-search-option]') : null;
        if (!node || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        go(node);
    };
    const onResultsMove = event => {
        const node = event.target instanceof Element ? event.target.closest('[data-shell-search-option]') : null;
        if (node) setActive(Number(node.dataset.shellSearchOption), false);
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

    button.addEventListener('click', onButtonClick);
    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onInputKeydown);
    panel.addEventListener('keydown', onPanelKeydown);
    results.addEventListener('click', onResultsClick);
    results.addEventListener('mousemove', onResultsMove);
    closeButton?.addEventListener('click', onCloseClick);
    document.addEventListener('keydown', onDocumentKeydown);
    document.addEventListener('pointerdown', onDocumentPointerDown);

    instances.set(button, () => {
        cancelPending();
        button.removeEventListener('click', onButtonClick);
        input.removeEventListener('input', onInput);
        input.removeEventListener('keydown', onInputKeydown);
        panel.removeEventListener('keydown', onPanelKeydown);
        results.removeEventListener('click', onResultsClick);
        results.removeEventListener('mousemove', onResultsMove);
        closeButton?.removeEventListener('click', onCloseClick);
        document.removeEventListener('keydown', onDocumentKeydown);
        document.removeEventListener('pointerdown', onDocumentPointerDown);
    });
}

export function dispose(button) {
    const teardown = button ? instances.get(button) : null;
    if (teardown) {
        teardown();
        instances.delete(button);
    }
}
