let interval;
let reference;
let endpoint;
let stopped = true;
let refreshing = false;
async function refresh() {
    if (stopped || refreshing) return;
    refreshing = true;
    try {
        const response = await fetch(endpoint, { credentials: 'same-origin', cache: 'no-store', redirect: 'error' });
        const proof = response.ok ? (await response.json()).proof : null;
        if (!stopped) await reference.invokeMethodAsync('AcceptSessionProof', proof);
    } catch {
        // Leave pending editor state in place; a later refresh can recover the same account.
    } finally { refreshing = false; }
}
export function start(target, url) {
    stop();
    stopped = false;
    reference = target;
    endpoint = url;
    refresh();
    interval = setInterval(refresh, 60000);
    window.addEventListener('online', refresh);
    document.addEventListener('visibilitychange', refresh);
}
export function stop() {
    stopped = true;
    clearInterval(interval);
    window.removeEventListener('online', refresh);
    document.removeEventListener('visibilitychange', refresh);
}
