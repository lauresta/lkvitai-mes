// Only this input's navigation keys are cancelled; normal typing and Tab keep
// native browser behavior. Each component detaches its own listener on disposal.
const listeners = new WeakMap();
export function attach(input) {
    if (!input || listeners.has(input)) return;
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
    listeners.set(input, listener);
}
export function detach(input) {
    const listener = listeners.get(input);
    if (listener) input.removeEventListener('keydown', listener);
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
