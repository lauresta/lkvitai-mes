// Item list: hover photo preview (pointer devices only) and the remembered view mode.
// The popover is plain DOM so hovering never makes a Blazor Server round trip.
(function () {
    'use strict';

    var OPEN_DELAY_MS = 150;
    var GAP = 8;
    var VIEW_KEY = 'lk.wh.adminItems.view';
    var POINTER_QUERY = '(hover: hover) and (pointer: fine)';
    var SELECTOR = '[data-preview-src]';

    var state = { popover: null, timer: 0, anchor: null, roots: new WeakMap() };

    function canPreview() {
        return !!window.matchMedia && window.matchMedia(POINTER_QUERY).matches;
    }

    function el(tag, className) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        return node;
    }

    function buildPopover() {
        var root = el('div', 'items-preview');
        root.setAttribute('data-testid', 'admin-items-photo-preview');
        root.setAttribute('role', 'tooltip');
        var frame = el('div', 'items-preview__frame');
        var img = el('img', 'items-preview__img');
        img.alt = '';
        frame.appendChild(img);
        root.appendChild(frame);
        root.appendChild(el('div', 'items-preview__sku'));
        root.appendChild(el('div', 'items-preview__name'));
        root.appendChild(el('div', 'items-preview__hint'));
        document.body.appendChild(root);
        return root;
    }

    function popover() {
        if (!state.popover) { state.popover = buildPopover(); }
        return state.popover;
    }

    function setText(root, selector, text) {
        root.querySelector(selector).textContent = text || '';
    }

    function fill(root, anchor) {
        var frame = root.querySelector('.items-preview__frame');
        var img = root.querySelector('.items-preview__img');
        var thumb = anchor.getAttribute('data-preview-thumb') || '';
        img.classList.remove('is-loaded');
        img.onload = function () { img.classList.add('is-loaded'); };
        frame.style.backgroundImage = thumb ? 'url("' + thumb + '")' : '';
        img.src = anchor.getAttribute('data-preview-src');
        setText(root, '.items-preview__sku', anchor.getAttribute('data-preview-sku'));
        setText(root, '.items-preview__name', anchor.getAttribute('data-preview-name'));
        setText(root, '.items-preview__hint', root.getAttribute('data-hint'));
    }

    function place(root, anchor) {
        var rect = anchor.getBoundingClientRect();
        var width = root.offsetWidth;
        var height = root.offsetHeight;
        var left = rect.right + GAP;
        var top = rect.top - GAP;
        if (top + height > window.innerHeight - GAP) { top = window.innerHeight - height - GAP; }
        if (left + width > window.innerWidth - GAP) { left = rect.left - width - GAP; }
        root.style.left = Math.max(GAP, left) + 'px';
        root.style.top = Math.max(GAP, top) + 'px';
    }

    function open(anchor) {
        var root = popover();
        fill(root, anchor);
        root.classList.add('is-open');
        place(root, anchor);
    }

    function close() {
        window.clearTimeout(state.timer);
        state.anchor = null;
        if (state.popover) { state.popover.classList.remove('is-open'); }
    }

    function schedule(anchor) {
        if (state.anchor === anchor) { return; }
        close();
        state.anchor = anchor;
        state.timer = window.setTimeout(function () { open(anchor); }, OPEN_DELAY_MS);
    }

    function targetOf(event) {
        return event.target && event.target.closest ? event.target.closest(SELECTOR) : null;
    }

    function onEnter(event) {
        var anchor = targetOf(event);
        if (anchor && canPreview()) { schedule(anchor); }
    }

    function onLeave(event) {
        var anchor = targetOf(event);
        var moved = event.relatedTarget;
        if (anchor && anchor === state.anchor && !(moved && anchor.contains(moved))) { close(); }
    }

    function onKey(event) {
        if (event.key === 'Escape') { close(); }
    }

    function attach(root, hint) {
        if (!root || state.roots.has(root)) { return; }
        var handlers = [
            ['mouseover', onEnter], ['mouseout', onLeave], ['focusin', onEnter], ['focusout', onLeave]
        ];
        handlers.forEach(function (h) { root.addEventListener(h[0], h[1]); });
        window.addEventListener('scroll', close, true);
        window.addEventListener('wheel', close, { passive: true });
        document.addEventListener('keydown', onKey);
        state.roots.set(root, handlers);
        popover().setAttribute('data-hint', hint || '');
    }

    function detach(root) {
        var handlers = root ? state.roots.get(root) : null;
        if (!handlers) { return; }
        handlers.forEach(function (h) { root.removeEventListener(h[0], h[1]); });
        state.roots.delete(root);
        window.removeEventListener('scroll', close, true);
        window.removeEventListener('wheel', close);
        document.removeEventListener('keydown', onKey);
        close();
    }

    function readView() {
        try { return window.localStorage.getItem(VIEW_KEY); } catch (e) { return null; }
    }

    function saveView(value) {
        try { window.localStorage.setItem(VIEW_KEY, value); } catch (e) { /* storage blocked: nothing to remember */ }
    }

    window.itemsPhotoPreview = { attach: attach, detach: detach, readView: readView, saveView: saveView };
})();
