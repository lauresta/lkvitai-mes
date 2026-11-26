// Finance → Įplaukų judėjimas. Browser-side helpers: the page calls its API with the user's Portal cookie
// (same origin), downloads the workbook, copies the link, and fills the print-only table.
window.financeIncomes = (() => {
  const root = '/api/finance/incomes';
  let media = [];

  async function request(path) {
    const started = new Date();
    let response;
    try {
      response = await fetch(root + path, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    } catch {
      throw new Error(JSON.stringify({ status: 0, path: root + path.split('?')[0], at: started.toISOString(), id: '' }));
    }
    if (!response.ok) {
      throw new Error(JSON.stringify({
        status: response.status, path: root + path.split('?')[0], at: started.toISOString(),
        id: response.headers.get('X-Correlation-Id') || ''
      }));
    }
    return await response.text();
  }

  async function download(query) {
    const response = await fetch(root + '/export.xlsx?' + query, { credentials: 'same-origin' });
    if (!response.ok) throw new Error('finance-http:' + response.status);
    const disposition = response.headers.get('Content-Disposition') || '';
    const match = /filename="?([^";]+)"?/i.exec(disposition);
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = match ? match[1] : 'Iplaukos.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  }

  async function copyLink() {
    const text = window.location.href;
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      const area = document.createElement('textarea');
      area.value = text;
      area.setAttribute('readonly', '');
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); } finally { area.remove(); }
    }
    return text;
  }

  function cell(row, text, className) {
    const td = document.createElement('td');
    if (className) td.className = className;
    td.textContent = text == null ? '' : String(text);
    row.appendChild(td);
    return td;
  }

  // Page 2+ of the print: the whole table as currently filtered, sorted and grouped (no virtualization).
  async function print(query, setup) {
    const host = document.getElementById('inc-print-rows');
    if (host) {
      host.textContent = '';
      try {
        const page = JSON.parse(await request('/rows?' + query + '&skip=0&take=60000'));
        const table = document.createElement('table');
        const head = table.createTHead().insertRow();
        setup.columns.forEach((title, index) => {
          const th = document.createElement('th');
          th.textContent = title;
          if (index === setup.columns.length - 1) th.className = 'num';
          head.appendChild(th);
        });
        const body = table.createTBody();
        const groups = page.groups || [];
        let index = 0;
        const addRows = count => {
          for (let i = 0; i < count && index < page.items.length; i++, index++) {
            const item = page.items[index];
            const tr = body.insertRow();
            setup.fields.forEach((field, column) =>
              cell(tr, format(item, field, setup), column === setup.fields.length - 1 ? 'num' : ''));
          }
        };
        if (groups.length === 0) addRows(page.items.length);
        groups.forEach(group => {
          const tr = body.insertRow();
          tr.className = 'grp';
          const label = cell(tr, `${group.label} · ${group.count}`);
          label.colSpan = setup.fields.length - 1;
          cell(tr, setup.money(group.sumEur), 'num');
          addRows(group.count);
        });
        const foot = table.createTFoot().insertRow();
        const total = cell(foot, setup.totalLabel);
        total.colSpan = setup.fields.length - 1;
        cell(foot, setup.money(page.sumEur), 'num');
        host.appendChild(table);
      } catch {
        host.textContent = setup.failed;
      }
    }
    window.print();
  }

  function format(item, field, setup) {
    const value = item[field];
    if (value == null || value === '') return field === 'employee' ? setup.noEmployee : '—';
    if (field === 'amountEur' || field === 'valueEur' || field === 'paidEur') return setup.money(value);
    // A negative discount is a price correction upwards: print it as "+89,3 %".
    if (field === 'discountPct') return (value < 0 ? '+' + String(-value) : String(value)).replace('.', ',') + ' %';
    if (field === 'method') return setup.methods[value] || value;
    if (field === 'status') return setup.statuses[value] || value;
    return value;
  }

  function moneyFormatter() {
    return value => {
      const n = Number(value) || 0;
      const parts = Math.abs(n).toFixed(2).split('.');
      return (n < -0.004 ? '−' : '') + parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ' ') + ',' + parts[1] + ' €';
    };
  }

  return {
    request,
    download,
    copyLink,
    print: (query, setup) => print(query, { ...setup, money: moneyFormatter() }),
    // Phone (< 760) and tablet (< 1100) layouts need different markup, not just CSS.
    watch(ref) {
      media.forEach(m => m.list.removeEventListener('change', m.handler));
      const phone = window.matchMedia('(max-width: 759px)');
      const tablet = window.matchMedia('(max-width: 1099px)');
      const current = () => phone.matches ? 'phone' : tablet.matches ? 'tablet' : 'desktop';
      const handler = () => ref.invokeMethodAsync('SetViewport', current());
      media = [{ list: phone, handler }, { list: tablet, handler }];
      media.forEach(m => m.list.addEventListener('change', handler));
      return current();
    },
    unwatch() {
      media.forEach(m => m.list.removeEventListener('change', m.handler));
      media = [];
    },
    scrollTableTop() {
      const body = document.querySelector('[data-inc-scroll]');
      if (body) body.scrollTop = 0;
    }
  };
})();
