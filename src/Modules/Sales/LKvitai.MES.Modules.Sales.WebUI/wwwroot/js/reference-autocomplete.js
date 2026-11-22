// Only this input's navigation keys are cancelled; normal typing and Tab keep
// native browser behavior. Each component detaches its own listener on disposal.
//
// Choosing with the pointer is handled here as well. An option is chosen on
// mousedown (a tap sends one too, a touch scroll does not), by its record id,
// through the component's .NET reference. A Blazor @onclick on the option row
// lost the choice whenever the list re-rendered between press and release, or
// when the press landed on a part of the menu that let the input lose focus.
const listeners = new WeakMap();
export function attach(input, component) {
    if (!input || listeners.has(input)) return;
    const root = input.closest('.reference-autocomplete');
    const pointer = event => {
        if (input.disabled || input.readOnly) return;
        const menu = event.target instanceof Element ? event.target.closest('[role="listbox"]') : null;
        if (menu && root.contains(menu)) {
            // Keeps focus in the input, so the menu stays open while its header,
            // messages or scrollbars are pressed.
            event.preventDefault();
            const option = event.button === 0 ? event.target.closest('[data-option-id]') : null;
            if (option && component) component.invokeMethodAsync('SelectFromBrowser', Number(option.dataset.optionId));
            return;
        }
        // A click on the already focused input opens the list again.
        if (event.target === input && event.button === 0 && document.activeElement === input
            && input.getAttribute('aria-expanded') !== 'true' && component)
            component.invokeMethodAsync('OpenFromBrowser');
    };
    root?.addEventListener('mousedown', pointer);
    const listener = event => {
        if (input.disabled || input.readOnly) return;
        if (event.isComposing) {
            // Do not interpret an IME confirmation as selecting a reference.
            if (['Enter', 'ArrowDown', 'ArrowUp', 'Escape'].includes(event.key)) event.stopPropagation();
            return;
        }
        const open = input.getAttribute('aria-expanded') === 'true';
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || (open && (event.key === 'Enter' || event.key === 'Escape')))
            event.preventDefault();
    };
    input.addEventListener('keydown', listener);
    listeners.set(input, { listener, pointer, root });
}
export function detach(input) {
    const entry = listeners.get(input);
    if (entry) {
        input.removeEventListener('keydown', entry.listener);
        entry.root?.removeEventListener('mousedown', entry.pointer);
    }
    listeners.delete(input);
}
export function scrollActive(input, optionId) {
    const root = input?.closest('.reference-autocomplete');
    const option = root?.querySelector(`[id="${optionId}"]`);
    if (!option) return;
    const menu = option.parentElement;
    if (option.offsetTop < menu.scrollTop) menu.scrollTop = option.offsetTop;
    else if (option.offsetTop + option.offsetHeight > menu.scrollTop + menu.clientHeight)
        menu.scrollTop = option.offsetTop + option.offsetHeight - menu.clientHeight;
}
