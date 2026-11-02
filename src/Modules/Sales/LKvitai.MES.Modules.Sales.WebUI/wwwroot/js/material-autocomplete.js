import { attach as attachKeys, detach as detachKeys, scrollActive } from './reference-autocomplete.js';
export { scrollActive };
export function attach(input) { attachKeys(input); }
export function detach(input, instanceId) { detachKeys(input); latestRequest.delete(instanceId); }

// Optional thumbnails reuse the existing same-origin photo metadata API. Only
// visible code prefixes are queried; never probe image URLs that may not exist.
// Stock displayed by this picker always comes from Sales' Access-compatible
// view_Remains projection, never from Frontline's rounded meter totals.
const metadata = new Map();
const expiryMs = 5 * 60 * 1000;
let outstanding = 0;
const waiters = [];
async function withLimit(work) {
    if (outstanding >= 2) await new Promise(resolve => waiters.push(resolve));
    outstanding++;
    try { return await work(); }
    finally { outstanding--; waiters.shift()?.(); }
}
async function prefixImages(prefix) {
    const cached = metadata.get(prefix);
    if (cached && Date.now() - cached.at < expiryMs) return cached.promise;
    const promise = withLimit(async () => {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), 2500);
        try {
            const query = new URLSearchParams({ search: prefix, threshold: '2147483647', page: '1', pageSize: '100' });
            const response = await fetch(`/api/frontline/fabric/low-stock?${query}`, {
                credentials: 'same-origin', signal: controller.signal, headers: { Accept: 'application/json' }
            });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) return {};
            const data = await response.json();
            const result = {};
            for (const item of (data.items ?? [])) {
                const url = item.thumbnailUrl;
                // Stored photo URLs are service-owned paths. Do not load arbitrary
                // remote URLs or a generic placeholder as if it were this fabric.
                if (typeof item.code === 'string' && typeof url === 'string' && url.startsWith('/api/frontline/fabric/') && !url.startsWith('//'))
                    result[item.code.toUpperCase()] = url;
            }
            return result;
        } catch { return {}; }
        finally { clearTimeout(timer); }
    });
    metadata.set(prefix, { at: Date.now(), promise });
    return promise;
}
const latestRequest = new Map();
export async function loadImages(codes, instanceId = 'material', selectedCode = null) {
    const token = {};
    latestRequest.set(instanceId, token);
    // Browser focus/typing stays immediately usable while metadata is optional.
    await new Promise(resolve => setTimeout(resolve, 180));
    if (latestRequest.get(instanceId) !== token) return {};
    const normalized = [...new Set(codes.filter(code => typeof code === 'string').map(code => code.trim().toUpperCase()))];
    const prefixes = [...new Set(normalized.filter(code => code.length >= 2).map(code => code.slice(0, 2)))].slice(0, 4);
    // Exact selected code is always resolved, even if a broad prefix page is
    // truncated. API search is contains-based; accept only an exact returned
    // code for this thumbnail. The same bounded cache/concurrency applies.
    const searches = selectedCode ? [selectedCode.trim().toUpperCase(), ...prefixes] : prefixes;
    const maps = await Promise.all([...new Set(searches)].map(prefixImages));
    const available = Object.assign({}, ...maps);
    return Object.fromEntries(normalized.filter(code => available[code]).map(code => [code, available[code]]));
}
