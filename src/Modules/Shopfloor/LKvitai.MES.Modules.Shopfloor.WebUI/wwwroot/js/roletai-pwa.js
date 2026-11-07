// Installable Shopfloor shell. Authenticated documents and API responses are never cached.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register(new URL('roletai-sw.js', document.baseURI), { scope: document.baseURI })
      .catch(() => {});
  });
}
