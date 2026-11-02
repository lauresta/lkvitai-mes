// Order-entry page chrome and product history. Persistence is handled by the scoped workspace client.
let reference = null;
let layer = null;
let tabFocus = false;

function baseUrl() {
    return window.location.pathname + window.location.search;
}

function updateOffset() {
    if (!layer) return;
    const header = document.querySelector('.topbar');
    const bottom = Math.max(0, Math.ceil(header?.getBoundingClientRect().bottom ?? 0));
    layer.style.top = `${bottom}px`;
    layer.style.setProperty('--sales-header-bottom', `${bottom}px`);
    updateNoticePosition();
}

// On desktop, notices overlay the illustration area, leaving the toolbar,
// main inputs and Save button available. Smaller layouts use a dismissible
// corner notice because the illustration is below the form.
export function updateNoticePosition() {
    if (!layer) return;
    const preview = layer.querySelector('.demo-product-preview');
    const toolbar = layer.querySelector('.demo-product-page__actions');
    const headerBottom = Math.max(0, document.querySelector('.topbar')?.getBoundingClientRect().bottom ?? 0);
    if (window.innerWidth > 1100 && preview && toolbar) {
        const box = preview.getBoundingClientRect();
        const toolbarBottom = toolbar.getBoundingClientRect().bottom;
        layer.style.setProperty('--sales-notice-top', `${Math.max(headerBottom + 12, toolbarBottom + 10)}px`);
        layer.style.setProperty('--sales-notice-right', `${Math.max(14, window.innerWidth - box.right)}px`);
        layer.style.setProperty('--sales-notice-width', `${Math.min(420, box.width)}px`);
    } else {
        layer.style.setProperty('--sales-notice-top', `${headerBottom + 12}px`);
        layer.style.setProperty('--sales-notice-right', '14px');
        layer.style.setProperty('--sales-notice-width', '420px');
    }
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

function onInputKey(event) {
    tabFocus = event.key === 'Tab' && !event.ctrlKey && !event.metaKey && !event.altKey;
}
function onPointerDown() { tabFocus = false; }
function onInputFocus(event) {
    if (!tabFocus || !layer?.contains(event.target)) return;
    tabFocus = false;
    const input = event.target;
    if (input.matches?.('input[data-select-on-focus], .demo-drawer input:not([type="checkbox"]):not([type="radio"]), .demo-editor input:not([type="checkbox"]):not([type="radio"])')) {
        input.select?.();
    }
}
export function focusInvalidField(id) {
    const input = document.getElementById(id);
    if (!input || !layer?.contains(input)) return;
    input.scrollIntoView({ block: 'center', behavior: 'auto' });
    input.focus({ preventScroll: true });
    input.select?.();
    const host = input.closest('.lk-stepper, .material-picker, .lk-price, .demo-product-editor__parameter') ?? input;
    host.classList.remove('sales-field-attention');
    // Restart even when the same field fails on the next explicit save.
    void host.offsetWidth;
    host.classList.add('sales-field-attention');
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
    document.addEventListener('scroll', updateNoticePosition, true);
    document.addEventListener('keydown', onPickerKey);
    document.addEventListener('keydown', onInputKey, true);
    document.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('focusin', onInputFocus, true);
}

export function openProduct() {
    if (window.history.state?.salesProduct) return;
    window.history.pushState({ salesProduct: true }, '', `${baseUrl()}#sales-product`);
}

export function closeProduct() {
    // Done must settle synchronously. A deferred history.back() can otherwise
    // run after Back to orders and undo that navigation to the list.
    if (window.history.state?.salesProduct) window.history.replaceState(null, '', baseUrl());
}

export function unregister() {
    window.removeEventListener('resize', updateOffset);
    window.removeEventListener('popstate', onPopState);
    document.removeEventListener('scroll', updateNoticePosition, true);
    document.removeEventListener('keydown', onPickerKey);
    document.removeEventListener('keydown', onInputKey, true);
    document.removeEventListener('pointerdown', onPointerDown, true);
    document.removeEventListener('focusin', onInputFocus, true);
    tabFocus = false;
    if (window.history.state?.salesProduct) {
        window.history.replaceState(null, '', baseUrl());
    }
    reference = null;
    layer = null;
}
