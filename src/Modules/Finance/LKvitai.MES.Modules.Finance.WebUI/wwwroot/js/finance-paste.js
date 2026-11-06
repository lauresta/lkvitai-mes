window.financeBank = (() => {
  let listener;
  const root = '/api/finance/bank-payments';
  async function request(path, method = 'GET', body) {
    const response = await fetch(root + path, {
      method, credentials: 'same-origin',
      headers: body == null ? {} : { 'Content-Type': 'application/json' },
      body: body == null ? undefined : JSON.stringify(body)
    });
    if (!response.ok) {
      let problem;
      try { problem = await response.json(); } catch { }
      throw new Error(String(response.status));
    }
    return response.status === 204 ? '' : await response.text();
  }
  return {
    request,
    attach(ref) {
      if (listener) document.removeEventListener('paste', listener);
      listener = event => {
        const target = event.target;
        if (target?.closest('input, textarea, [contenteditable="true"]') && !target?.hasAttribute('data-paste')) return;
        const text = event.clipboardData?.getData('text/plain');
        if (text) { event.preventDefault(); ref.invokeMethodAsync('ReceivePaste', text); }
      };
      document.addEventListener('paste', listener);
    },
    detach() { if (listener) document.removeEventListener('paste', listener); listener = null; },
    focusPaste() { document.querySelector('[data-paste]')?.focus(); },
    confirm(message) { return window.confirm(message); }
  };
})();
