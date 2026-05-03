// Local-only "last opened modules" tracker for the Portal hero block.
// Stores up to 10 raw entries in localStorage, returns the top N keys
// (most recent first). Validation against the live module list happens
// on the .NET side so a renamed/removed module never appears stale.
//
// Storage shape (JSON):
//   [{ "key": "warehouse", "at": "2026-05-03T17:21:18.123Z" }, ...]
//
// Why localStorage instead of a cookie:
//   * Recents are personal browser state, not auth state — no need to
//     ship them on every HTTP request.
//   * No size pressure on the Portal cookie (which already carries the
//     structured bearer + claims).
//   * Survives soft refresh; does not survive private-mode / clear-data,
//     which is the desired UX (recents are convenience, not data).

(() => {
    const STORAGE_KEY = "lkvitai-mes.portal.recents.v1";
    const MAX_STORED  = 10;

    function readAll() {
        try {
            const raw = window.localStorage.getItem(STORAGE_KEY);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            // Quota exceeded, JSON parse failure, or storage disabled (private
            // mode in some browsers). Treat as "no recents" so the hero never
            // throws into the Blazor circuit.
            return [];
        }
    }

    function writeAll(items) {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
        } catch {
            // Same swallow rationale as readAll() — recents are best-effort.
        }
    }

    function record(key) {
        if (typeof key !== "string" || !key) return;
        const now = new Date().toISOString();
        const existing = readAll().filter(r => r && r.key !== key);
        existing.unshift({ key: key, at: now });
        writeAll(existing.slice(0, MAX_STORED));
    }

    window.lkPortalRecents = {
        // Returns up to `take` most-recent module keys. .NET filters them
        // against the live module list, so we don't bother validating here.
        get(take) {
            const limit = typeof take === "number" && take > 0 ? take : 3;
            return readAll()
                .slice()
                .sort((a, b) => (b.at || "").localeCompare(a.at || ""))
                .slice(0, limit)
                .map(r => r.key)
                .filter(k => typeof k === "string" && k.length > 0);
        },

        // Bumps `key` to the front of the list with a fresh timestamp.
        record: record
    };

    // Capture-phase click listener so we record the bump BEFORE the browser
    // navigates away. Calling JSInterop from a Razor @onclick on an <a href>
    // is racy because the browser may tear down the SignalR circuit before
    // the message lands. Doing it in plain JS keeps it synchronous + reliable.
    document.addEventListener("click", function (event) {
        const anchor = event.target?.closest?.("[data-portal-recent-key]");
        if (!anchor) return;
        const key = anchor.getAttribute("data-portal-recent-key");
        if (key) record(key);
    }, true);
})();
