import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { PNG } from 'pngjs';
import { chunkPage, imagesOf } from '../src/docs-chunker.js';
import { DocsSearch } from '../src/docs-search.js';
import { fakeJwt, mockFetch, rpc, running, testConfig } from './helpers.js';

const WAREHOUSE_WRITES = ['location_create', 'inbound_shipment_create', 'inbound_shipment_receive', 'inbound_shipment_post', 'putaway_move', 'production_issue_create', 'transfer_create', 'transfer_advance'];

test('warehouse writes are offered only with MCP_ENABLE_WAREHOUSE_ENTRY, independently of order entry', async () => {
  const off = await running(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }));
  const on = await running(testConfig({ MCP_ENABLE_WAREHOUSE_ENTRY: 'true' }));
  try {
    const token = fakeJwt();
    const offNames = ((await rpc(off.base, token, 'tools/list')).body.result.tools as { name: string }[]).map((t) => t.name);
    for (const name of WAREHOUSE_WRITES) assert.ok(!offNames.includes(name), name);
    assert.ok(offNames.includes('locations_search'));
    const refused = (await rpc(off.base, token, 'tools/call', { name: 'location_create', arguments: { code: 'A1' } })).body.result;
    assert.equal(refused.structuredContent.error.code, 'UNKNOWN_TOOL');

    const onTools = (await rpc(on.base, token, 'tools/list')).body.result.tools as { name: string; annotations: Record<string, unknown> }[];
    for (const name of WAREHOUSE_WRITES) {
      const tool = onTools.find((t) => t.name === name);
      assert.ok(tool, name);
      assert.equal(tool.annotations.readOnlyHint, false, name);
      assert.equal(tool.annotations.destructiveHint, name === 'inbound_shipment_post' || name === 'production_issue_create', name);
    }
    assert.ok(!onTools.some((t) => t.name === 'order_create'));
  } finally {
    await off.close();
    await on.close();
  }
});

test('location_create defaults the barcode and type; location_stock asks the stock read model by location', async () => {
  const { fetchImpl, seen } = mockFetch((call) => {
    if (call.method === 'POST') return { status: 200, body: { id: 12, code: 'A1-01-02', barcode: 'A1-01-02', type: 'Bin', status: 'Active' } };
    return { status: 200, body: { items: [{ itemId: 5, internalSku: 'RM-1', itemName: 'Audinys', baseUom: 'm', locationCode: 'A1-01-02', qty: 40, reservedQty: 4, availableQty: 36 }], projectionTimestamp: '2026-11-22T08:00:00Z' } };
  });
  const server = await running(testConfig({ MCP_ENABLE_WAREHOUSE_ENTRY: 'true' }), { fetchImpl });
  try {
    const token = fakeJwt();
    const created = (await rpc(server.base, token, 'tools/call', { name: 'location_create', arguments: { code: 'A1-01-02', warehouseId: 'c836e5e8-8090-4df5-bf1c-fe4de2e7009b' } })).body.result;
    assert.equal(created.isError, undefined);
    assert.equal(created.structuredContent.location.id, 12);
    const post = seen[0]!;
    assert.equal(post.url.pathname, '/api/warehouse/v1/locations');
    assert.equal(post.headers.authorization, `Bearer ${token}`);
    assert.deepEqual(post.body, { code: 'A1-01-02', barcode: 'A1-01-02', type: 'Bin', isVirtual: false, status: 'Active', warehouseId: 'c836e5e8-8090-4df5-bf1c-fe4de2e7009b' });

    const stock = (await rpc(server.base, token, 'tools/call', { name: 'location_stock', arguments: { location: 'A1-01-02' } })).body.result;
    assert.equal(seen[1]!.url.pathname, '/api/warehouse/v1/stock/available');
    assert.equal(seen[1]!.url.searchParams.get('location'), 'A1-01-02');
    assert.deepEqual(stock.structuredContent.items[0], { itemId: 5, sku: 'RM-1', name: 'Audinys', uom: 'm', location: 'A1-01-02', onHand: 40, reserved: 4, available: 36 });

    const missing = (await rpc(server.base, token, 'tools/call', { name: 'location_stock', arguments: {} })).body.result;
    assert.equal(missing.structuredContent.error.code, 'INVALID_ARGUMENTS');
  } finally {
    await server.close();
  }
});

test('inbound_shipment_create maps the invoice to the Warehouse API; transfer_create and transfer_advance carry a command id', async () => {
  const transfer = { id: '11111111-1111-4111-8111-111111111111', transferNumber: 'TRF-1', status: 'DRAFT', lines: [{ itemId: 5, qty: 2, fromLocationId: 10, toLocationId: 11 }] };
  const { fetchImpl, seen } = mockFetch((call) => {
    if (call.url.pathname.endsWith('/inbound-shipments')) return { status: 200, body: { id: 77, referenceNumber: 'SF-100', status: 'Draft' } };
    if (call.url.pathname.endsWith('/transfers')) return { status: 200, body: transfer };
    if (call.url.pathname.endsWith('/execute')) return { status: 400, body: { title: 'Validation error', detail: 'Insufficient available stock.' } };
    return { status: 200, body: { ...transfer, status: 'APPROVED' } };
  });
  const server = await running(testConfig({ MCP_ENABLE_WAREHOUSE_ENTRY: 'true' }), { fetchImpl, newId: () => '22222222-2222-4222-8222-222222222222' });
  try {
    const token = fakeJwt({ preferred_username: 'vstropus' });
    const created = (await rpc(server.base, token, 'tools/call', {
      name: 'inbound_shipment_create',
      arguments: { referenceNumber: 'SF-100', supplierId: 3, invoiceNumber: 'SF-100', invoiceDate: '2026-11-20', currency: 'eur', netAmount: 100, vatAmount: 21, lines: [{ itemId: 5, qty: 10, unitPrice: 10 }] },
    })).body.result;
    assert.equal(created.structuredContent.shipment.id, 77);
    assert.deepEqual(seen[0]!.body, {
      referenceNumber: 'SF-100',
      supplierId: 3,
      invoiceNumber: 'SF-100',
      invoiceDate: '2026-11-20',
      currency: 'EUR',
      netAmount: 100,
      vatAmount: 21,
      lines: [{ itemId: 5, expectedQty: 10, unitPrice: 10, currency: 'EUR' }],
    });

    const noLines = (await rpc(server.base, token, 'tools/call', { name: 'inbound_shipment_create', arguments: { referenceNumber: 'X', supplierId: 3, lines: [] } })).body.result;
    assert.equal(noLines.structuredContent.error.code, 'INVALID_ARGUMENTS');

    const made = (await rpc(server.base, token, 'tools/call', { name: 'transfer_create', arguments: { lines: [{ itemId: 5, qty: 2, fromLocationId: 10, toLocationId: 11 }] } })).body.result;
    assert.equal(made.structuredContent.transfer.number, 'TRF-1');
    assert.equal((seen[1]!.body as { commandId: string }).commandId, '22222222-2222-4222-8222-222222222222');

    const submitted = (await rpc(server.base, token, 'tools/call', { name: 'transfer_advance', arguments: { transferId: transfer.id, action: 'submit' } })).body.result;
    assert.equal(seen[2]!.url.pathname, `/api/warehouse/v1/transfers/${transfer.id}/submit`);
    assert.equal(submitted.structuredContent.transfer.status, 'APPROVED');

    const refused = (await rpc(server.base, token, 'tools/call', { name: 'transfer_advance', arguments: { transferId: transfer.id, action: 'execute' } })).body.result;
    assert.equal(refused.isError, true);
    assert.match(refused.content[0].text, /Insufficient available stock/);

    const badAction = (await rpc(server.base, token, 'tools/call', { name: 'transfer_advance', arguments: { transferId: transfer.id, action: 'cancel' } })).body.result;
    assert.equal(badAction.structuredContent.error.code, 'INVALID_ARGUMENTS');
  } finally {
    await server.close();
  }
});

test('item_cost sums the valuation rows', async () => {
  const { fetchImpl, seen } = mockFetch(() => ({ status: 200, body: [{ itemId: 5, itemSku: 'RM-1', itemName: 'Audinys', qty: 10, unitCost: 2.5, totalValue: 25 }, { itemId: 6, itemSku: 'RM-2', itemName: 'Vamzdis', qty: 4, unitCost: 1.25, totalValue: 5 }] }));
  const server = await running(testConfig(), { fetchImpl });
  try {
    const result = (await rpc(server.base, fakeJwt(), 'tools/call', { name: 'item_cost', arguments: { categoryName: 'Audiniai' } })).body.result;
    assert.equal(seen[0]!.url.pathname, '/api/warehouse/v1/valuation/on-hand-value');
    assert.equal(result.structuredContent.totalValue, 30);
    assert.equal(result.structuredContent.items[0].unitCost, 2.5);
  } finally {
    await server.close();
  }
});

test('manual chunks list their screenshots; docs_image returns the PNG and draws frames', async () => {
  assert.deepEqual(imagesOf('warehouse/inbound', '![Langas](../img/warehouse/inbound-create.png)\n![x](../../etc/passwd.png) ![y](https://example.com/a.png)'), [{ path: 'img/warehouse/inbound-create.png', alt: 'Langas' }]);
  const chunks = chunkPage('warehouse/inbound.md', '# Priėmimas\n\n## Naujas dokumentas\nSpauskite (UI: Create).\n\n![Kūrimo langas](../img/warehouse/inbound-create.png)\n');
  assert.deepEqual(chunks[0]!.images, [{ path: 'img/warehouse/inbound-create.png', alt: 'Kūrimo langas' }]);

  const dir = mkdtempSync(join(tmpdir(), 'cikada-img-'));
  mkdirSync(join(dir, 'warehouse'));
  const blank = new PNG({ width: 200, height: 100 });
  blank.data.fill(255);
  writeFileSync(join(dir, 'warehouse', 'inbound-create.png'), PNG.sync.write(blank));

  const server = await running(testConfig(), { docs: new DocsSearch({ generated_at: '', git_sha: '', page_count: 1, chunk_count: chunks.length, chunks }), docsImageDir: dir });
  try {
    const token = fakeJwt();
    const got = (await rpc(server.base, token, 'tools/call', { name: 'docs_get', arguments: { id: chunks[0]!.id } })).body.result;
    assert.deepEqual(got.structuredContent.chunks[0].images, [{ path: 'img/warehouse/inbound-create.png', alt: 'Kūrimo langas' }]);

    const plain = (await rpc(server.base, token, 'tools/call', { name: 'docs_image', arguments: { image: 'img/warehouse/inbound-create.png' } })).body.result;
    assert.equal(plain.content[1].type, 'image');
    assert.equal(plain.content[1].mimeType, 'image/png');
    assert.equal(plain.structuredContent.width, 200);

    const marked = (await rpc(server.base, token, 'tools/call', { name: 'docs_image', arguments: { image: 'img/warehouse/inbound-create.png', marks: [{ x: 10, y: 20, width: 50, height: 40 }] } })).body.result;
    const png = PNG.sync.read(Buffer.from(marked.content[1].data as string, 'base64'));
    const pixel = (x: number, y: number) => [...png.data.subarray((png.width * y + x) * 4, (png.width * y + x) * 4 + 3)];
    assert.deepEqual(pixel(60, 20), [220, 38, 38]);
    assert.deepEqual(pixel(60, 40), [255, 255, 255]);
    assert.equal(marked.structuredContent.marks[0].colour, 'red');

    const missing = (await rpc(server.base, token, 'tools/call', { name: 'docs_image', arguments: { image: 'img/warehouse/nope.png' } })).body.result;
    assert.equal(missing.structuredContent.error.code, 'NOT_FOUND');
    const escape = (await rpc(server.base, token, 'tools/call', { name: 'docs_image', arguments: { image: 'img/../secret.png' } })).body.result;
    assert.equal(escape.structuredContent.error.code, 'INVALID_ARGUMENTS');
  } finally {
    await server.close();
  }
});
