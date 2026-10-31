// Local M1 order-entry page chrome and product history. No data leaves the browser.
let reference = null;
let layer = null;

function baseUrl() {
    return window.location.pathname + window.location.search;
}

function updateOffset() {
    if (!layer) return;
    const header = document.querySelector('.topbar');
    layer.style.top = `${Math.max(0, Math.ceil(header?.getBoundingClientRect().bottom ?? 0))}px`;
}

function onPopState() {
    if (!window.history.state?.salesProduct) {
        reference?.invokeMethodAsync('HandleProductHistoryExit');
    }
}

function onPickerKey(event) {
    if (!/^[1-9]$/.test(event.key) || event.ctrlKey || event.metaKey || event.altKey) return;
    if (!document.querySelector('.demo-product-catalog__type-list')) return;
    const target = event.target;
    if (target?.matches?.('input, textarea, select, [contenteditable="true"]')) return;
    event.preventDefault();
    reference?.invokeMethodAsync('SelectProductTypeByShortcut', Number(event.key));
}

export function register(dotnetReference, element) {
    unregister();
    reference = dotnetReference;
    layer = element;
    // A refresh discards the in-memory draft, so remove a stale product fragment.
    if (window.location.hash === '#sales-product') {
        window.history.replaceState(null, '', baseUrl());
    }
    updateOffset();
    window.addEventListener('resize', updateOffset);
    window.addEventListener('popstate', onPopState);
    document.addEventListener('keydown', onPickerKey);
}

export function openProduct() {
    if (window.history.state?.salesProduct) return;
    window.history.pushState({ salesProduct: true }, '', `${baseUrl()}#sales-product`);
}

export function closeProduct() {
    if (window.history.state?.salesProduct) window.history.back();
}

export function unregister() {
    window.removeEventListener('resize', updateOffset);
    window.removeEventListener('popstate', onPopState);
    document.removeEventListener('keydown', onPickerKey);
    if (window.history.state?.salesProduct) {
        window.history.replaceState(null, '', baseUrl());
    }
    reference = null;
    layer = null;
}
