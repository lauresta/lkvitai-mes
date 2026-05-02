// Sales Orders keyboard shortcut bridge (S-3.3).
//
// Loaded once per circuit by Index.razor via dynamic import. Owns a single
// document-level keydown listener that dispatches into the .NET component
// via DotNetObjectReference. The module keeps mutable state in module scope
// so re-importing during hot-reload or accidental double-register replaces
// the previous handler instead of stacking them.
//
// Shortcut grammar (codex §S-3.3):
//   /        — focus the orders search input (only when not already typing)
//   Esc      — clear active search if any; else deselect current row;
//              forwarded from inside the search input too so Esc clears it
//   ArrowUp  — move row selection one up
//   ArrowDn  — move row selection one down
//   Enter    — open the currently selected order in a new tab
//
// Editable-element guard: ArrowUp/Down/Enter are intentionally NOT swallowed
// when the user is typing in an input/textarea/select/contenteditable, so
// keyboard navigation inside the search field, dropdowns, and any future
// inline editors keeps working as expected.

let _handler = null;
let _dotnetRef = null;

export function register(ref) {
    unregister();
    _dotnetRef = ref;
    _handler = onKey;
    document.addEventListener('keydown', _handler);
}

export function unregister() {
    if (_handler) {
        document.removeEventListener('keydown', _handler);
    }
    _handler = null;
    _dotnetRef = null;
}

export function focusElement(element) {
    if (element && typeof element.focus === 'function') {
        element.focus();
        if (typeof element.select === 'function') {
            element.select();
        }
    }
}

export function openInNewTab(url) {
    if (url) {
        window.open(url, '_blank', 'noopener,noreferrer');
    }
}

// requestAnimationFrame so we wait for Blazor to commit the new is-selected
// class before measuring; without it we'd scroll the previously-selected row.
export function scrollSelectedIntoView() {
    requestAnimationFrame(() => {
        const el = document.querySelector('.orders-list__body tr.is-selected');
        if (el && typeof el.scrollIntoView === 'function') {
            el.scrollIntoView({ block: 'nearest' });
        }
    });
}

function onKey(e) {
    if (!_dotnetRef) return;

    const target = e.target;
    const isEditable = !!(target && target.matches && target.matches(
        'input, textarea, select, [contenteditable="true"]'));

    // Esc is forwarded unconditionally so it can both clear the search input
    // (focused) and deselect a row (focus elsewhere) from the same handler.
    if (e.key === 'Escape') {
        e.preventDefault();
        _dotnetRef.invokeMethodAsync('HandleShortcut', 'escape');
        return;
    }

    // '/' from a non-editable element jumps focus into the search box. While
    // already typing we let it through so the user can actually search for
    // strings containing '/'.
    if (e.key === '/' && !isEditable) {
        e.preventDefault();
        _dotnetRef.invokeMethodAsync('HandleShortcut', 'slash');
        return;
    }

    if (isEditable) return;

    switch (e.key) {
        case 'ArrowUp':
            e.preventDefault();
            _dotnetRef.invokeMethodAsync('HandleShortcut', 'up');
            break;
        case 'ArrowDown':
            e.preventDefault();
            _dotnetRef.invokeMethodAsync('HandleShortcut', 'down');
            break;
        case 'Enter':
            e.preventDefault();
            _dotnetRef.invokeMethodAsync('HandleShortcut', 'enter');
            break;
        default:
            // not ours
            break;
    }
}
