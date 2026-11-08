import assert from 'node:assert/strict';
import { test } from 'node:test';
import { NOT_DEPLOYED } from '../src/tools-order-entry.js';
import { fakeJwt, mockFetch, rpc, running, testConfig } from './helpers.js';

const READ_TOOLS = ['whoami', 'orders_search', 'order_get', 'orders_filters', 'customers_search', 'customer_get', 'price_groups_list', 'items_search', 'stock_get', 'report_production', 'report_work', 'report_payments', 'report_dimensions', 'production_queue', 'materials_popularity', 'stopped_products', 'unpaid_orders', 'made_not_delivered', 'customer_debt', 'orders_find', 'bank_payments_list', 'bank_payments_preview'];
const WRITE_TOOLS = ['order_create', 'order_update', 'order_product_add', 'order_product_update', 'order_product_remove', 'order_address_set', 'order_confirm', 'order_cancel'];
const DESTRUCTIVE = ['order_product_remove', 'order_confirm', 'order_cancel'];

test('tools/list: the read tools by default, read-only annotated; order entry only when it is on', async () => {
  const off = await running(testConfig());
  const on = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }));
  try {
    const token = fakeJwt();
    const listed = (await rpc(off.base, token, 'tools/list')).body.result.tools as { name: string; annotations: Record<string, unknown> }[];
    assert.deepEqual(listed.map((t) => t.name), [...READ_TOOLS, 'bank_payments_import']);
    for (const tool of listed.filter((t) => t.name !== 'bank_payments_import')) assert.equal(tool.annotations.readOnlyHint, true, tool.name);
    assert.equal(listed.find((t) => t.name === 'bank_payments_import')?.annotations.destructiveHint, true);

    const all = (await rpc(on.base, token, 'tools/list')).body.result.tools as { name: string; annotations: Record<string, unknown> }[];
    for (const name of WRITE_TOOLS) {
      const tool = all.find((t) => t.name === name);
      assert.ok(tool, name);
      assert.equal(tool.annotations.readOnlyHint, false, name);
      assert.equal(tool.annotations.destructiveHint, DESTRUCTIVE.includes(name), name);
    }
    for (const name of ['order_entry_catalog', 'order_entry_get', 'order_entry_unit_price', 'order_entry_product_preview']) {
      assert.equal(all.find((t) => t.name === name)?.annotations.readOnlyHint, true, name);
    }
    const prompts = (await rpc(on.base, token, 'prompts/list')).body.result.prompts as { name: string }[];
    assert.deepEqual(prompts.map((p) => p.name), ['production_status', 'debt_review', 'management_report', 'daily_orders_review', 'take_new_order']);
    const offPrompts = (await rpc(off.base, token, 'prompts/list')).body.result.prompts as { name: string }[];
    assert.deepEqual(offPrompts.map((p) => p.name), ['production_status', 'debt_review', 'management_report', 'daily_orders_review']);
  } finally {
    await off.close();
    await on.close();
  }
});

test('orders_search forwards the bearer unchanged to the Sales API and answers compactly', async () => {
  const api = mockFetch(() => ({
    status: 200,
    body: {
      items: [{ id: 42, number: 'VLN-1001', date: '2026-11-02', price: 1250.5, debt: 0, isOverdue: false, customer: 'UAB Test', hasDebt: false, isVip: false, hasNote: false, status: 'Įvestas', statusCode: 'entered', store: 'Vilnius', address: 'Gedimino pr. 1' }],
      total: 1,
      page: 1,
      pageSize: 25,
    },
  }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const token = fakeJwt();
    const response = await rpc(stand.base, token, 'tools/call', { name: 'orders_search', arguments: { search: 'VLN', store: 'Vilnius', hasDebt: false } });
    assert.equal(response.status, 200);
    const result = response.body.result;
    assert.equal(result.isError, undefined);
    assert.equal(result.structuredContent.total, 1);
    assert.deepEqual(result.structuredContent.orders[0], { id: 42, number: 'VLN-1001', date: '2026-11-02', customer: 'UAB Test', status: 'Įvestas', statusCode: 'entered', store: 'Vilnius', address: 'Gedimino pr. 1', price: 1250.5, debt: 0 });
    assert.match(result.content[0].text, /VLN-1001/);

    assert.equal(api.seen.length, 1);
    const call = api.seen[0]!;
    assert.equal(call.url.origin, 'https://sales.internal');
    assert.equal(call.url.pathname, '/api/sales/orders');
    assert.equal(call.url.searchParams.get('search'), 'VLN');
    assert.equal(call.url.searchParams.get('store'), 'Vilnius');
    assert.equal(call.url.searchParams.get('pageSize'), '25');
    assert.equal(call.headers.authorization, `Bearer ${token}`);
    // the token is in no log line
    assert.ok(!JSON.stringify(stand.logs).includes(token));
  } finally {
    await stand.close();
  }
});

test('bank preview reports live NR and import uses only a confirmed server preview', async () => {
  const previewId = '00000000-0000-4000-8000-000000000123';
  const api = mockFetch((seen) => seen.url.pathname.endsWith('/preview')
    ? { status: 200, body: { previewId, latestNr: 340, rows: [
      { line: 1, date: '2026-11-06', firm: 'UAB Test', purpose: 'Invoice 1', sum: 24.5, status: 'New', selected: true },
      { line: 2, date: '2026-11-06', firm: 'UAB Test', purpose: 'Invoice 2', sum: 24.5, status: 'Duplicate', selected: false },
      { line: 3, date: '2026-10-30', firm: 'Vida Rudženskienė', purpose: 'Advance', sum: 225, status: 'PossibleDuplicate', selected: false, matchedNr: 79757, matchedFirm: 'Vida Rudzenskienė' },
    ] } }
    : { status: 200, body: { importId: '00000000-0000-4000-8000-000000000124', nrFrom: 341, nrTo: 341, count: 1 } });
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const token = fakeJwt();
    const rows = [{ date: '2026-11-06', firm: 'UAB Test', purpose: 'Invoice 1', sum: '24.50' }];
    const preview = (await rpc(stand.base, token, 'tools/call', { name: 'bank_payments_preview', arguments: { country: 'LT', rows } })).body.result;
    assert.equal(preview.isError, undefined, JSON.stringify(preview));
    assert.equal(preview.structuredContent.latestNr, 340);
    assert.equal(preview.structuredContent.nextNr, 341);
    assert.deepEqual(preview.structuredContent.selectedNewLines, [1]);
    assert.equal(preview.structuredContent.possibleDuplicateCount, 1);
    assert.match(preview.content[0].text, /1 possible duplicates/);
    assert.equal(preview.structuredContent.rows[2].matchedNr, 79757);
    assert.deepEqual(api.seen[0]?.body, { country: 'LT', rows });
    assert.equal(api.seen[0]?.url.origin, 'https://finance.internal');
    assert.equal(api.seen[0]?.headers.authorization, `Bearer ${token}`);

    const args = { country: 'LT', previewId, expectedNextNr: 341, selectedLines: [1], confirmed: false };
    const denied = (await rpc(stand.base, token, 'tools/call', { name: 'bank_payments_import', arguments: args })).body.result;
    assert.equal(denied.isError, true);
    assert.equal(api.seen.length, 1);
    const imported = (await rpc(stand.base, token, 'tools/call', { name: 'bank_payments_import', arguments: { ...args, confirmed: true } })).body.result;
    assert.equal(imported.isError, undefined, JSON.stringify(imported));
    assert.equal(imported.structuredContent.nrFrom, 341);
    assert.deepEqual(api.seen[1]?.body, { country: 'LT', previewId, expectedNextNr: 341, selectedLines: [1] });
  } finally {
    await stand.close();
  }
});

test('bank payments list returns individual payers and full purposes across pages', async () => {
  const rows = Array.from({ length: 109 }, (_, i) => ({
    nr: 79755 + i, date: '2026-10-30', firm: `Payer ${i + 1}`,
    purpose: `Full bank payment purpose ${i + 1}`, sum: 225 + i,
  }));
  const api = mockFetch((seen) => {
    const page = Number(seen.url.searchParams.get('page') ?? 1);
    return { status: 200, body: { items: rows.slice((page - 1) * 100, page * 100),
      total: 109, countryTotal: 1000, lastDate: '2026-10-30', page, pageSize: 100 } };
  });
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const token = fakeJwt();
    const argumentsBase = { country: 'LT', from: '2026-10-30', to: '2026-10-30' };
    const first = (await rpc(stand.base, token, 'tools/call', {
      name: 'bank_payments_list', arguments: { ...argumentsBase, page: 1 },
    })).body.result;
    const second = (await rpc(stand.base, token, 'tools/call', {
      name: 'bank_payments_list', arguments: { ...argumentsBase, page: 2 },
    })).body.result;
    assert.equal(first.isError, undefined, JSON.stringify(first));
    assert.equal(first.structuredContent.total, 109);
    assert.equal(first.structuredContent.pages, 2);
    assert.equal(first.structuredContent.items.length, 100);
    assert.equal(second.structuredContent.items.length, 9);
    assert.deepEqual(second.structuredContent.items[0], rows[100]);
    assert.match(first.content[0].text, /Fetch page 2/);
    assert.equal(api.seen.length, 2);
    for (const call of api.seen) {
      assert.equal(call.url.origin, 'https://finance.internal');
      assert.equal(call.url.pathname, '/api/finance/bank-payments');
      assert.equal(call.url.searchParams.get('from'), '2026-10-30');
      assert.equal(call.url.searchParams.get('to'), '2026-10-30');
      assert.equal(call.headers.authorization, `Bearer ${token}`);
    }
  } finally {
    await stand.close();
  }
});

test('bank payments list reports a Finance denial instead of an empty statement', async () => {
  const api = mockFetch(() => ({ status: 403, body: { title: 'Finance access required.' } }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', {
      name: 'bank_payments_list', arguments: { country: 'LT' },
    })).body.result;
    assert.equal(result.isError, true);
    assert.equal(result.structuredContent.error.status, 403);
    assert.match(result.content[0].text, /Finance access required/);
  } finally {
    await stand.close();
  }
});

test('items_search joins prices, the price group price and stock from the Warehouse API', async () => {
  const api = mockFetch((seen) => {
    if (seen.url.pathname === '/api/warehouse/v1/items') {
      return { status: 200, body: { items: [{ id: 7, internalSKU: 'RB-100', name: 'Roller blind white', categoryName: 'Blinds', baseUoM: 'PCS', status: 'Active', basePrice: 20, purchasePrice: 9 }], totalCount: 1, pageNumber: 1, pageSize: 20 } };
    }
    if (seen.url.pathname === '/api/warehouse/v1/price-groups/3/item-prices') return { status: 200, body: [{ itemId: 7, amount: 17.5 }] };
    if (seen.url.pathname === '/api/warehouse/v1/stock/available') {
      return { status: 200, body: { items: [{ itemId: 7, internalSku: 'RB-100', qty: 10, reservedQty: 2, availableQty: 8 }, { itemId: 7, internalSku: 'RB-100', qty: 5, reservedQty: 0, availableQty: 5 }], totalCount: 2, pageNumber: 1, pageSize: 1000 } };
    }
    return { status: 404 };
  });
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'items_search', arguments: { search: 'RB', priceGroupId: 3 } })).body.result;
    assert.equal(result.isError, undefined, JSON.stringify(result));
    assert.deepEqual(result.structuredContent.items[0], {
      id: 7, sku: 'RB-100', name: 'Roller blind white', category: 'Blinds', uom: 'PCS', status: 'Active', basePrice: 20, purchasePrice: 9, groupPrice: 17.5, groupPriceIsOverride: true,
      stock: { onHand: 15, reserved: 2, available: 13, locations: 2 },
    });
    assert.equal(api.seen.find((s) => s.url.pathname.endsWith('/stock/available'))?.url.searchParams.get('sku'), 'RB');
  } finally {
    await stand.close();
  }
});

test('an API refusal is a tool error with the API\'s reason, not a transport failure', async () => {
  const api = mockFetch(() => ({ status: 403, body: { title: 'Forbidden' } }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'customers_search', arguments: { search: 'UAB' } })).body.result;
    assert.equal(result.isError, true);
    assert.match(result.content[0].text, /role/);
    assert.equal(result.structuredContent.error.status, 403);
  } finally {
    await stand.close();
  }
});

test('invalid arguments are refused before any API call', async () => {
  const api = mockFetch(() => ({ status: 200, body: {} }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'customer_get', arguments: { id: 'not-a-uuid' } })).body.result;
    assert.equal(result.isError, true);
    assert.equal(result.structuredContent.error.code, 'INVALID_ARGUMENTS');
    const missing = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_get', arguments: {} })).body.result;
    assert.match(missing.content[0].text, /number is required/);
    assert.equal(api.seen.length, 0);
  } finally {
    await stand.close();
  }
});

test('order entry tools are no tools when order entry is off', async () => {
  const api = mockFetch(() => ({ status: 200, body: {} }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_cancel', arguments: { orderId: 1, expectedRevision: 'r1' } })).body.result;
    assert.equal(result.isError, true);
    assert.equal(result.structuredContent.error.code, 'UNKNOWN_TOOL');
    assert.equal(api.seen.length, 0);
  } finally {
    await stand.close();
  }
});

test('a 404 from a Sales API without order entry says order entry is not deployed', async () => {
  const api = mockFetch(() => ({ status: 404 }));
  const stand = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }), { fetchImpl: api.fetchImpl, newId: () => '00000000-0000-4000-8000-000000000001' });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_cancel', arguments: { orderId: 5, expectedRevision: 'r1' } })).body.result;
    assert.equal(result.isError, true);
    assert.equal(result.content[0].text, NOT_DEPLOYED);
    assert.equal(result.structuredContent.error.code, 'ORDER_ENTRY_NOT_DEPLOYED');
  } finally {
    await stand.close();
  }
});

test('order_update keeps the fields not given, sends the revision and a command id, and refuses a stale revision', async () => {
  const snapshot = {
    header: { id: 5, number: 'VLN-5-26', seriesId: 1, date: '2026-11-01', due: '2026-11-20', customerId: 10, customerName: 'UAB Test', storeId: 2, managerId: 3, consultantId: null, priorityId: 2, mountingDate: null, note: 'old', discountPercent: 0, calculatedTotal: 100, paid: 0 },
    products: [],
    revision: 'r1',
    writesAllowed: true,
  };
  const api = mockFetch((seen) => {
    if (seen.method === 'GET') return { status: 200, body: snapshot };
    return { status: 200, body: { order: { ...snapshot, revision: 'r2', header: { ...snapshot.header, due: '2026-12-01' } }, productId: null, replayed: false } };
  });
  const stand = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }), { fetchImpl: api.fetchImpl, newId: () => '00000000-0000-4000-8000-000000000001' });
  try {
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_update', arguments: { orderId: 5, expectedRevision: 'r1', due: '2026-12-01' } })).body.result;
    assert.equal(result.isError, undefined, JSON.stringify(result));
    assert.equal(result.structuredContent.revision, 'r2');
    assert.equal(result.structuredContent.commandId, '00000000-0000-4000-8000-000000000001');
    const put = api.seen.find((s) => s.method === 'PUT')!;
    assert.equal(put.url.pathname, '/api/sales/order-entry/orders/5');
    assert.deepEqual(put.body, {
      commandId: '00000000-0000-4000-8000-000000000001',
      expectedRevision: 'r1',
      header: { seriesId: 1, number: 'VLN-5-26', date: '2026-11-01', due: '2026-12-01', customerId: 10, storeId: 2, managerId: 3, consultantId: null, priorityId: 2, mountingDate: null, note: 'old', discountPercent: 0 },
    });

    const stale = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_update', arguments: { orderId: 5, expectedRevision: 'r0', note: 'x' } })).body.result;
    assert.equal(stale.isError, true);
    assert.match(stale.content[0].text, /changed since revision r0/);
    assert.equal(api.seen.filter((s) => s.method === 'PUT').length, 1);
  } finally {
    await stand.close();
  }
});

test('order_product_update uses the exact list price on the saved order date, and refuses a different supplied price', async () => {
  const order = {
    header: { id: 5, number: 'VLN-5-26', date: '2026-11-07' },
    products: [{ id: 7, typeId: 10, widthMm: 750, heightMm: 2200, quantity: 2, colourId: 31, unitPrice: 0, parameters: [] }],
    revision: 'r1', writesAllowed: true,
  };
  const api = mockFetch((seen) => {
    if (seen.url.pathname.endsWith('/unit-price')) return { status: 200, body: { status: 'Found', unitPrice: 61.9787 } };
    if (seen.method === 'PUT') return { status: 200, body: { order: { ...order, revision: 'r2' }, productId: 7 } };
    return { status: 200, body: order };
  });
  const stand = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }), {
    fetchImpl: api.fetchImpl, newId: () => '00000000-0000-4000-8000-000000000001', today: () => '2026-11-08',
  });
  try {
    const args = { orderId: 5, productId: 7, expectedRevision: 'r1' };
    const saved = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_product_update', arguments: args })).body.result;
    assert.equal(saved.isError, undefined, JSON.stringify(saved));
    const priceCall = api.seen.find((s) => s.url.pathname.endsWith('/unit-price'))!;
    assert.equal(priceCall.url.searchParams.get('orderDate'), '2026-11-07');
    assert.equal(priceCall.url.searchParams.get('componentId'), '31');
    const put = api.seen.find((s) => s.method === 'PUT')!;
    assert.equal((put.body as { product: { unitPrice: number; useListPrice: boolean } }).product.unitPrice, 61.9787);
    assert.equal((put.body as { product: { unitPrice: number; useListPrice: boolean } }).product.useListPrice, true);

    const stale = (await rpc(stand.base, fakeJwt(), 'tools/call', {
      name: 'order_product_update', arguments: { ...args, unitPrice: 61.98, useListPrice: true },
    })).body.result;
    assert.equal(stale.isError, true);
    assert.match(stale.content[0].text, /differs from the current list price 61\.9787/);
    assert.equal(api.seen.filter((s) => s.method === 'PUT').length, 1);
  } finally {
    await stand.close();
  }
});

test('order_accessory_set posts exactly the SaveAccessoryCommand fields', async () => {
  const order = { header: { id: 5, number: 'VLN-5-26' }, products: [], revision: 'r2', writesAllowed: true };
  const api = mockFetch(() => ({ status: 200, body: { order, productId: 7, replayed: false } }));
  const stand = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }), { fetchImpl: api.fetchImpl, newId: () => '00000000-0000-4000-8000-000000000001' });
  try {
    const args = { orderId: 5, productId: 7, expectedRevision: 'r1', accessoryId: 12, quantity: 2, unitPrice: 3.5 };
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'order_accessory_set', arguments: args })).body.result;
    assert.equal(result.isError, undefined, JSON.stringify(result));
    const post = api.seen.find((s) => s.method === 'POST')!;
    assert.equal(post.url.pathname, '/api/sales/order-entry/orders/5/products/7/accessories');
    assert.deepEqual(post.body, { commandId: '00000000-0000-4000-8000-000000000001', expectedRevision: 'r1', accessoryId: 12, quantity: 2, unitPrice: 3.5 });
  } finally {
    await stand.close();
  }
});

test('report_production sends an exclusive end date and flattens the rows for charts', async () => {
  const api = mockFetch(() => ({
    status: 200,
    body: { granularity: 'month', groupBy: ['period', 'type', 'unit'], measures: ['quantity', 'pieces'], truncated: false, rows: [
      { keys: { period: '2026-10', type: 'Apsauginės (vnt)', unit: 'vnt' }, values: { quantity: 166, pieces: 166 } },
    ] },
  }));
  const stand = await running(testConfig(), { fetchImpl: api.fetchImpl, today: () => '2026-11-02' });
  try {
    const args = { from: '2026-10-01', to: '2026-10-31', groupBy: ['period', 'type'], type: 'apsaug', state: 'Pagamintas' };
    const result = (await rpc(stand.base, fakeJwt(), 'tools/call', { name: 'report_production', arguments: args })).body.result;
    assert.equal(result.isError, undefined, JSON.stringify(result));
    const call = api.seen[0]!;
    assert.equal(call.url.pathname, '/api/sales/reports/production');
    assert.equal(call.url.searchParams.get('to'), '2026-11-01');
    assert.equal(call.url.searchParams.get('groupBy'), 'period,type');
    assert.equal(call.url.searchParams.get('type'), 'apsaug');
    assert.deepEqual(result.structuredContent.rows, [{ period: '2026-10', type: 'Apsauginės (vnt)', unit: 'vnt', quantity: 166, pieces: 166 }]);
    assert.equal(result.structuredContent.totals.pieces, 166);
  } finally {
    await stand.close();
  }
});
