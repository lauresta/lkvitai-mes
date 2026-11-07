// Network-only: production documents contain private customer and order data.
self.addEventListener('fetch', event => {
  if (event.request.mode === 'navigate') event.respondWith(fetch(event.request));
});
