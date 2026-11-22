// The warehouse tools: where things lie, what they cost, purchase documents (a supplier invoice with its lines) and stock moves. The reads are always
// offered; the writes (a location, a purchase document, receiving, putaway, a transfer) only when MCP_ENABLE_WAREHOUSE_ENTRY=true. Warehouse API checks
// the role of every call: reads need Operator or above, purchase documents and putaway QCInspector / WarehouseManager / WarehouseAdmin, a location
// WarehouseManager / WarehouseAdmin.

import { ArgumentError } from './validate.js';
import { DATE, READ, asBool, asNumber, asString, bool, compact, int, money, num, obj, str, write, type ToolDef } from './tool.js';

const WAREHOUSE = '/api/warehouse/v1';

type Row = Record<string, unknown>;
const rows = (value: unknown): Row[] => (Array.isArray(value) ? (value as Row[]) : []);
/** A paged answer ({ items, totalCount }) or a bare array. */
const items = (value: unknown): Row[] => (Array.isArray(value) ? (value as Row[]) : rows((value as Row | undefined)?.items));
const total = (value: unknown, fallback: number): unknown => (value && typeof value === 'object' && !Array.isArray(value) ? ((value as Row).totalCount ?? fallback) : fallback);

const UUID = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';
const uuid = (description: string) => str(description, { pattern: UUID });

const warehousesList: ToolDef = {
  name: 'warehouses_list',
  title: 'Warehouses',
  description: 'The warehouses with id, code, name and status. The id is what location_create and locations_search take as warehouseId.',
  inputSchema: obj({ search: str('Part of the code or name.', { maxLength: 100 }) }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/warehouses`, query: { search: asString(args.search), pageSize: 200 } });
    const list = items(result).map((w) => compact({ id: w.id, code: w.code, name: w.name, description: w.description, status: w.status, isVirtual: w.isVirtual === true ? true : undefined }));
    return { text: list.length ? list.map((w) => `${w.code} | ${w.name} | ${w.status}`).join('\n') : 'No warehouses.', data: { warehouses: list } };
  },
};

function locationSummary(l: Row): Row {
  return compact({
    id: l.id,
    code: l.code,
    barcode: l.barcode,
    type: l.type,
    status: l.status,
    zoneType: l.zoneType,
    isVirtual: l.isVirtual === true ? true : undefined,
    aisle: l.aisle,
    rack: l.rack,
    level: l.level,
    bin: l.bin,
    rackRowId: l.rackRowId,
    capacityWeight: l.capacityWeight,
    capacityVolume: l.capacityVolume,
  });
}

const locationsSearch: ToolDef = {
  name: 'locations_search',
  title: 'Find locations',
  description:
    'Storage locations (bins, zones) by part of the code or barcode. Returns id, code, barcode, type, status, zone type and the rack position. The id is what transfer_create, putaway_move and location_stock take. Virtual locations (RECEIVING, QC_HOLD, SHIPPING and the like) are included unless includeVirtual is false.',
  inputSchema: obj({
    search: str('Part of the code or barcode.', { maxLength: 100 }),
    type: str('Only this type, e.g. Bin, Zone.', { maxLength: 40 }),
    status: str('Only this status, e.g. Active, Maintenance.', { maxLength: 40 }),
    warehouseId: uuid('Only this warehouse (id from warehouses_list).'),
    includeVirtual: bool('Include virtual locations, default true.'),
    limit: int('How many, default 50, at most 500.', { minimum: 1, maximum: 500 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/locations`,
      query: { search: asString(args.search), type: asString(args.type), status: asString(args.status), warehouseId: asString(args.warehouseId), includeVirtual: asBool(args.includeVirtual), pageSize: asNumber(args.limit) ?? 50 },
    });
    const list = items(result).map(locationSummary);
    return {
      text: `${list.length} of ${total(result, list.length)} locations.${list.length ? `\n${list.map((l) => `${l.id} | ${l.code} | ${l.type} | ${l.status}`).join('\n')}` : ''}`,
      data: { total: total(result, list.length), locations: list },
    };
  },
};

const locationStock: ToolDef = {
  name: 'location_stock',
  title: 'What lies in a location',
  description:
    'Every item in one location, by locationId (from locations_search) or by the exact location code. Quantities: on hand, reserved, available. For "where does item X lie" use stock_get. Read model, may lag a few seconds behind the warehouse.',
  inputSchema: obj({
    locationId: int('Location id from locations_search.'),
    location: str('Exact location code, e.g. "A1-01-02".', { maxLength: 100 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const locationId = asNumber(args.locationId);
    const location = asString(args.location);
    if (locationId === undefined && !location) throw new ArgumentError('give locationId (from locations_search) or the location code');
    const result = await api.json<Row>({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/stock/available`,
      query: { locationId, location, includeReserved: true, includeVirtualLocations: true, pageSize: 1000 },
    });
    const list = rows(result.items).map((r) =>
      compact({ itemId: r.itemId, sku: r.internalSku, name: r.itemName, uom: r.baseUom, location: r.locationCode, lot: r.lotNumber, expiry: r.expiryDate, onHand: r.qty, reserved: r.reservedQty, available: r.availableQty }),
    );
    return {
      text: list.length ? list.map((r) => `${r.sku} ${r.name ?? ''}: ${r.onHand} ${r.uom ?? ''} (available ${r.available})`).join('\n') : 'Nothing on hand in that location.',
      data: { items: list, asOf: result.projectionTimestamp },
    };
  },
};

const itemCost: ToolDef = {
  name: 'item_cost',
  title: 'Cost of the stock on hand',
  description:
    'The valuation of the stock on hand: quantity, unit cost and total value per item, for one item (itemId from items_search), one category or one location. This is what the stock cost the company; the sale price and the last purchase price are in items_search. Needs a warehouse role that may read valuation (managers, accountants, CFO).',
  inputSchema: obj({
    itemId: int('Item id from items_search.'),
    categoryName: str('Only this item category.', { maxLength: 100 }),
    locationId: int('Only this location (id from locations_search).'),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/valuation/on-hand-value`,
      query: { itemId: asNumber(args.itemId), categoryName: asString(args.categoryName), locationId: asNumber(args.locationId) },
    });
    const list = items(result).map((v) => compact({ itemId: v.itemId, sku: v.itemSku, name: v.itemName, category: v.categoryName, qty: v.qty, unitCost: v.unitCost, totalValue: v.totalValue, lastUpdated: v.lastUpdated }));
    const sum = list.reduce((acc, v) => acc + (typeof v.totalValue === 'number' ? v.totalValue : 0), 0);
    return {
      text: list.length ? `${list.length} item(s), total value ${money(sum)}.\n${list.slice(0, 50).map((v) => `${v.sku} ${v.name ?? ''}: ${v.qty} x ${money(v.unitCost)} = ${money(v.totalValue)}`).join('\n')}` : 'No valued stock on hand for that filter.',
      data: { totalValue: Math.round(sum * 100) / 100, items: list },
    };
  },
};

const suppliersSearch: ToolDef = {
  name: 'suppliers_search',
  title: 'Find suppliers',
  description: 'Suppliers by part of the name or code. Returns id, code, name, company and VAT code, country. The id is what inbound_shipment_create takes as supplierId.',
  inputSchema: obj({
    search: str('Part of the name or code.', { maxLength: 100 }),
    country: str('Only this country code, e.g. LT.', { maxLength: 10 }),
    limit: int('How many, default 25, at most 200.', { minimum: 1, maximum: 200 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/suppliers`, query: { search: asString(args.search), country: asString(args.country), pageSize: asNumber(args.limit) ?? 25 } });
    const list = items(result).map((s) => compact({ id: s.id, code: s.code, name: s.name, companyCode: s.companyCode, vatCode: s.vatCode, city: s.city, country: s.country }));
    return {
      text: `${list.length} of ${total(result, list.length)} suppliers.${list.length ? `\n${list.map((s) => `${s.id} | ${s.code} | ${s.name}`).join('\n')}` : ''}`,
      data: { total: total(result, list.length), suppliers: list },
    };
  },
};

const shipmentSummary = (s: Row): Row =>
  compact({
    id: s.id,
    reference: s.referenceNumber,
    supplierId: s.supplierId,
    supplier: s.supplierName,
    expectedDate: s.expectedDate,
    status: s.status,
    lines: s.totalLines,
    expectedQty: s.totalExpectedQty,
    receivedQty: s.totalReceivedQty,
    purchaseValue: s.totalExpectedValue,
  });

const inboundShipmentsList: ToolDef = {
  name: 'inbound_shipments_list',
  title: 'Purchase documents',
  description:
    'Purchase documents (inbound shipments: what a supplier invoiced and what has been received), newest first. Filter by supplier, status (Draft, Partial, Complete, Cancelled) or expected date. Returns id, reference, supplier, status, lines, expected and received quantity.',
  inputSchema: obj({
    supplierId: int('Supplier id from suppliers_search.'),
    status: str('Exact status, e.g. Draft, Partial, Complete.', { maxLength: 40 }),
    expectedFrom: DATE('Expected on or after'),
    expectedTo: DATE('Expected on or before'),
    limit: int('How many, default 25, at most 200.', { minimum: 1, maximum: 200 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/inbound-shipments`,
      query: { supplierId: asNumber(args.supplierId), status: asString(args.status), expectedDateFrom: asString(args.expectedFrom), expectedDateTo: asString(args.expectedTo), pageSize: asNumber(args.limit) ?? 25 },
    });
    const list = items(result).map(shipmentSummary);
    return {
      text: `${list.length} of ${total(result, list.length)} purchase documents.${list.length ? `\n${list.map((s) => `${s.id} | ${s.reference} | ${s.supplier} | ${s.status} | received ${s.receivedQty}/${s.expectedQty}`).join('\n')}` : ''}`,
      data: { total: total(result, list.length), shipments: list },
    };
  },
};

function shipmentDetail(s: Row): Row {
  const purchase = (s.purchase ?? undefined) as Row | undefined;
  return compact({
    id: s.id,
    reference: s.referenceNumber,
    supplierId: s.supplierId,
    supplier: s.supplierName,
    expectedDate: s.expectedDate,
    status: s.status,
    invoiceNumber: s.invoiceNumber,
    invoiceDate: s.invoiceDate,
    lines: rows(s.lines).map((l) =>
      compact({ lineId: l.lineId, itemId: l.itemId, sku: l.itemSku, name: l.itemName, uom: l.baseUoM, expectedQty: l.expectedQty, receivedQty: l.receivedQty, unitPrice: l.unitPrice, currency: l.currency, needsLot: l.requiresLotTracking === true ? true : undefined, needsQc: l.requiresQC === true ? true : undefined }),
    ),
    additionalCosts: rows(s.additionalCosts).map((c) => compact({ costType: c.costType, amount: c.amount, currency: c.currency })),
    purchase: purchase
      ? compact({
          currency: purchase.currency,
          netAmount: purchase.netAmount,
          vatAmount: purchase.vatAmount,
          grossAmount: purchase.grossAmount,
          vatTreatment: purchase.vatTreatment,
          accountingRoute: purchase.accountingRoute,
          postingStatus: purchase.postingStatus,
          postedAt: purchase.postedAt,
          queueStatus: purchase.queueStatus,
          queueError: purchase.queueError,
          problems: rows(purchase.problems).length ? rows(purchase.problems) : undefined,
        })
      : undefined,
  });
}

const describeShipment = (d: Row): string => {
  const lines = rows(d.lines).map((l) => `  line ${l.lineId}: ${l.sku} ${l.name ?? ''} ${l.receivedQty ?? 0}/${l.expectedQty} ${l.uom ?? ''}${typeof l.unitPrice === 'number' ? ` at ${money(l.unitPrice)}` : ''}`);
  return `Purchase document ${d.id} ${d.reference} | ${d.supplier ?? ''} | ${d.status}${d.invoiceNumber ? ` | invoice ${d.invoiceNumber}` : ''}\n${lines.join('\n')}`;
};

const inboundShipmentGet: ToolDef = {
  name: 'inbound_shipment_get',
  title: 'One purchase document',
  description:
    'One purchase document by id: supplier, invoice number and date, every line (lineId, item, expected and received quantity, unit price), additional costs and the accounting state (amounts, VAT treatment, posting status and what blocks posting). The lineId is what inbound_shipment_receive takes.',
  inputSchema: obj({ shipmentId: int('Purchase document id from inbound_shipments_list.') }, ['shipmentId']),
  annotations: READ,
  handler: async (args, { api }) => {
    const detail = shipmentDetail(await api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/inbound-shipments/${asNumber(args.shipmentId)}` }));
    return { text: describeShipment(detail), data: { shipment: detail } };
  },
};

const transferSummary = (t: Row): Row =>
  compact({
    id: t.id,
    number: t.transferNumber,
    status: t.status,
    fromWarehouse: t.fromWarehouse,
    toWarehouse: t.toWarehouse,
    requestedBy: t.requestedBy,
    requestedAt: t.requestedAt,
    approvedBy: t.approvedBy,
    executedAt: t.executedAt,
    lines: rows(t.lines).map((l) => compact({ itemId: l.itemId, qty: l.qty, fromLocationId: l.fromLocationId, toLocationId: l.toLocationId })),
  });

const describeTransfer = (t: Row): string => `Transfer ${t.number} | ${t.status} | ${rows(t.lines).length} line(s) | id ${t.id}`;

const transfersList: ToolDef = {
  name: 'transfers_list',
  title: 'Stock transfers',
  description: 'Stock transfers between locations, newest first, optionally by status (Draft, PendingApproval, Approved, InTransit, Completed, Cancelled). Returns id, number, status and the lines (item, quantity, from and to location ids).',
  inputSchema: obj({
    status: str('Only this status.', { maxLength: 40 }),
    limit: int('How many, default 25, at most 200.', { minimum: 1, maximum: 200 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/transfers`, query: { status: asString(args.status), pageSize: asNumber(args.limit) ?? 25 } });
    const list = items(result).map(transferSummary);
    return { text: `${list.length} of ${total(result, list.length)} transfers.${list.length ? `\n${list.map(describeTransfer).join('\n')}` : ''}`, data: { total: total(result, list.length), transfers: list } };
  },
};

const transferGet: ToolDef = {
  name: 'transfer_get',
  title: 'One stock transfer',
  description: 'One stock transfer by id, with its status and lines.',
  inputSchema: obj({ transferId: uuid('Transfer id from transfers_list or transfer_create.') }, ['transferId']),
  annotations: READ,
  handler: async (args, { api }) => {
    const transfer = transferSummary(await api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/transfers/${asString(args.transferId)}` }));
    return { text: describeTransfer(transfer), data: { transfer } };
  },
};

// ---- writes -------------------------------------------------------------------------------------------------------------------------------------

const locationCreate: ToolDef = {
  name: 'location_create',
  title: 'Create a location',
  description:
    'Create a storage location (a bin, a shelf slot, a floor area). The code and the barcode must be unique; the barcode defaults to the code. Give warehouseId (from warehouses_list) for a physical location. Needs WarehouseManager or WarehouseAdmin. Tell the user the code, type and warehouse and get an explicit yes first.',
  inputSchema: obj(
    {
      code: str('Location code, e.g. "A1-01-02".', { minLength: 1, maxLength: 50 }),
      barcode: str('Barcode printed on the label; defaults to the code.', { maxLength: 100 }),
      type: str('Location type, default "Bin" (other values in use: Zone, Virtual).', { maxLength: 40 }),
      warehouseId: uuid('Warehouse id from warehouses_list.'),
      zoneType: str('Zone type, e.g. General, Refrigerated, Hazmat, Quarantine; the API lists the allowed values when it refuses one.', { maxLength: 40 }),
      aisle: str('Aisle label.', { maxLength: 20 }),
      rack: str('Rack label.', { maxLength: 20 }),
      level: str('Level label.', { maxLength: 20 }),
      bin: str('Bin label.', { maxLength: 20 }),
      capacityWeight: num('Capacity in kg.', { minimum: 0 }),
      capacityVolume: num('Capacity in cubic metres.', { minimum: 0 }),
      parentLocationId: int('Parent location id, when the location sits inside another one.'),
    },
    ['code'],
  ),
  annotations: write(false),
  warehouseEntry: true,
  handler: async (args, { api }) => {
    const code = asString(args.code) as string;
    const created = locationSummary(
      await api.json<Row>({
        target: 'warehouse',
        method: 'POST',
        path: `${WAREHOUSE}/locations`,
        body: compact({
          code,
          barcode: asString(args.barcode) ?? code,
          type: asString(args.type) ?? 'Bin',
          isVirtual: false,
          status: 'Active',
          warehouseId: asString(args.warehouseId),
          zoneType: asString(args.zoneType),
          aisle: asString(args.aisle),
          rack: asString(args.rack),
          level: asString(args.level),
          bin: asString(args.bin),
          capacityWeight: asNumber(args.capacityWeight),
          capacityVolume: asNumber(args.capacityVolume),
          parentLocationId: asNumber(args.parentLocationId),
        }),
      }),
    );
    return { text: `Location ${created.code ?? code} created${created.id !== undefined ? ` (id ${created.id})` : ''}.`, data: { location: created } };
  },
};

const shipmentLine = obj(
  {
    itemId: int('Item id from items_search.'),
    qty: num('Quantity expected (and invoiced), in the item base unit.', { minimum: 0.0001 }),
    unitPrice: num('Unit price on the invoice, without VAT.', { minimum: 0 }),
    invoiceQty: num('Quantity on the invoice, when it differs from qty (another unit).', { minimum: 0 }),
    invoiceUnitPrice: num('Unit price on the invoice in its own unit, when invoiceQty is given.', { minimum: 0 }),
  },
  ['itemId', 'qty'],
);

const inboundShipmentCreate: ToolDef = {
  name: 'inbound_shipment_create',
  title: 'Enter a supplier invoice',
  description:
    'Create a purchase document (inbound shipment) from a supplier invoice: the supplier, the invoice number and date, the lines (item, quantity, unit price without VAT) and optional additional costs (transport and the like). Nothing is received yet: stock arrives with inbound_shipment_receive and the document goes to accounting with inbound_shipment_post. Look the supplier up with suppliers_search and every item with items_search first; never guess an id or a price. Show the user the whole document (supplier, invoice, each line, totals) and get an explicit yes before calling. Needs QCInspector, WarehouseManager or WarehouseAdmin.',
  inputSchema: obj(
    {
      referenceNumber: str('Your reference for the document; the invoice number is the usual choice.', { minLength: 1, maxLength: 100 }),
      supplierId: int('Supplier id from suppliers_search.'),
      invoiceNumber: str('Invoice number as printed.', { maxLength: 100 }),
      invoiceDate: DATE('Invoice date'),
      expectedDate: DATE('When the goods are expected'),
      currency: str('Invoice currency, ISO code, default EUR.', { minLength: 3, maxLength: 3 }),
      netAmount: num('Invoice total without VAT, as printed.', { minimum: 0 }),
      vatAmount: num('Invoice VAT total, as printed.', { minimum: 0 }),
      lines: { type: 'array', description: 'Invoice lines.', minItems: 1, maxItems: 200, items: shipmentLine },
      additionalCosts: {
        type: 'array',
        description: 'Costs invoiced on the same document that are spread over the goods (transport, customs).',
        maxItems: 20,
        items: obj({ costType: str('Cost type code, e.g. Transport.', { minLength: 1, maxLength: 50 }), amount: num('Amount without VAT.', { minimum: 0 }) }, ['costType', 'amount']),
      },
    },
    ['referenceNumber', 'supplierId', 'lines'],
  ),
  annotations: write(false),
  warehouseEntry: true,
  handler: async (args, { api }) => {
    const currency = asString(args.currency)?.toUpperCase();
    const created = await api.json<Row>({
      target: 'warehouse',
      method: 'POST',
      path: `${WAREHOUSE}/inbound-shipments`,
      body: compact({
        referenceNumber: asString(args.referenceNumber),
        supplierId: asNumber(args.supplierId),
        invoiceNumber: asString(args.invoiceNumber),
        invoiceDate: asString(args.invoiceDate),
        expectedDate: asString(args.expectedDate),
        currency,
        netAmount: asNumber(args.netAmount),
        vatAmount: asNumber(args.vatAmount),
        lines: rows(args.lines).map((l) => compact({ itemId: l.itemId, expectedQty: l.qty, unitPrice: l.unitPrice, currency, invoiceQty: l.invoiceQty, invoiceUnitPrice: l.invoiceUnitPrice })),
        additionalCosts: rows(args.additionalCosts).length ? rows(args.additionalCosts).map((c) => compact({ costType: c.costType, amount: c.amount, currency })) : undefined,
      }),
    });
    return {
      text: `Purchase document ${created.id} ${created.referenceNumber ?? ''} created, status ${created.status}. Read it with inbound_shipment_get for the line ids before receiving.`,
      data: { shipment: compact({ id: created.id, reference: created.referenceNumber, status: created.status }) },
    };
  },
};

const inboundShipmentReceive: ToolDef = {
  name: 'inbound_shipment_receive',
  title: 'Receive goods of a purchase document',
  description:
    'Book a received quantity of one line of a purchase document into stock. The goods land in RECEIVING (or QC_HOLD when the item needs quality control); from RECEIVING they go to a storage location with putaway_move. lineId comes from inbound_shipment_get. Items with lot tracking need lotNumber. This changes stock: state the item, quantity and document and get an explicit yes first. Needs QCInspector, WarehouseManager or WarehouseAdmin.',
  inputSchema: obj(
    {
      shipmentId: int('Purchase document id.'),
      lineId: int('Line id from inbound_shipment_get.'),
      receivedQty: num('Quantity received now, in the item base unit.', { minimum: 0.0001 }),
      lotNumber: str('Lot number, for items with lot tracking.', { maxLength: 100 }),
      productionDate: DATE('Production date of the lot'),
      expiryDate: DATE('Expiry date of the lot'),
      notes: str('A note for the receipt.', { maxLength: 500 }),
    },
    ['shipmentId', 'lineId', 'receivedQty'],
  ),
  annotations: write(false),
  warehouseEntry: true,
  handler: async (args, { api }) => {
    const received = await api.json<Row>({
      target: 'warehouse',
      method: 'POST',
      path: `${WAREHOUSE}/inbound-shipments/${asNumber(args.shipmentId)}/receive`,
      body: compact({
        lineId: asNumber(args.lineId),
        receivedQty: asNumber(args.receivedQty),
        lotNumber: asString(args.lotNumber),
        productionDate: asString(args.productionDate),
        expiryDate: asString(args.expiryDate),
        notes: asString(args.notes),
      }),
    });
    return {
      text: `Received ${received.receivedQty} of item ${received.itemId} into ${received.destinationLocationCode} (location id ${received.destinationLocationId}).`,
      data: { receipt: compact({ shipmentId: received.shipmentId, lineId: received.lineId, itemId: received.itemId, receivedQty: received.receivedQty, lotId: received.lotId, locationId: received.destinationLocationId, location: received.destinationLocationCode }) },
    };
  },
};

const inboundShipmentPost: ToolDef = {
  name: 'inbound_shipment_post',
  title: 'Post a purchase document to accounting',
  description:
    'Post a purchase document to accounting once the invoice amounts are right. Irreversible from here: a posted document is corrected with a supplier credit, not edited. Read the document with inbound_shipment_get first; when it lists problems, the API refuses and says why. Get an explicit yes before calling. Needs QCInspector, WarehouseManager or WarehouseAdmin.',
  inputSchema: obj({ shipmentId: int('Purchase document id.') }, ['shipmentId']),
  annotations: write(true),
  warehouseEntry: true,
  handler: async (args, { api }) => {
    const id = asNumber(args.shipmentId);
    const posted = await api.json<Row | undefined>({ target: 'warehouse', method: 'POST', path: `${WAREHOUSE}/inbound-shipments/${id}/post`, body: {} });
    const detail = posted && typeof posted === 'object' && posted.lines ? shipmentDetail(posted) : shipmentDetail(await api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/inbound-shipments/${id}` }));
    const purchase = (detail.purchase ?? {}) as Row;
    return { text: `Purchase document ${id} posted: posting status ${purchase.postingStatus ?? 'unknown'}${purchase.queueStatus ? `, accounting queue ${purchase.queueStatus}` : ''}.`, data: { shipment: detail } };
  },
};

const putawayMove: ToolDef = {
  name: 'putaway_move',
  title: 'Put received stock away',
  description:
    'Move received stock from RECEIVING into a storage location (putaway), in one step. fromLocationId is the id of RECEIVING and toLocationId a physical location, both from locations_search; location_stock on RECEIVING shows what waits there. For a move between two storage locations use transfer_create. This changes stock: state the item, quantity and destination and get an explicit yes first. Needs QCInspector, WarehouseManager or WarehouseAdmin.',
  inputSchema: obj(
    {
      itemId: int('Item id.'),
      qty: num('Quantity to move, in the item base unit.', { minimum: 0.0001 }),
      fromLocationId: int('Id of the RECEIVING location.'),
      toLocationId: int('Id of the destination storage location.'),
      notes: str('A note for the move.', { maxLength: 500 }),
    },
    ['itemId', 'qty', 'fromLocationId', 'toLocationId'],
  ),
  annotations: write(false),
  warehouseEntry: true,
  handler: async (args, { api }) => {
    const moved = await api.json<Row>({
      target: 'warehouse',
      method: 'POST',
      path: `${WAREHOUSE}/putaway`,
      body: compact({ itemId: asNumber(args.itemId), qty: asNumber(args.qty), fromLocationId: asNumber(args.fromLocationId), toLocationId: asNumber(args.toLocationId), notes: asString(args.notes) }),
    });
    return {
      text: `Moved ${moved.qty} of item ${moved.itemId} from ${moved.fromLocationCode} to ${moved.toLocationCode}.${moved.warning ? ` Warning: ${moved.warning}` : ''}`,
      data: { move: compact({ itemId: moved.itemId, qty: moved.qty, from: moved.fromLocationCode, to: moved.toLocationCode, warning: moved.warning }) },
    };
  },
};

const transferCreate: ToolDef = {
  name: 'transfer_create',
  title: 'Create a stock transfer',
  description:
    'Create a transfer that moves stock between two locations: one or more lines of item, quantity, from and to location id (locations_search; stock_get shows where the item lies). The transfer starts as a draft and moves no stock until transfer_advance runs it: submit, approve when it asks for approval, then execute. Show the user every line and get an explicit yes before calling.',
  inputSchema: obj(
    {
      lines: {
        type: 'array',
        description: 'What to move.',
        minItems: 1,
        maxItems: 100,
        items: obj(
          {
            itemId: int('Item id from items_search.'),
            qty: num('Quantity, in the item base unit.', { minimum: 0.0001 }),
            fromLocationId: int('Source location id.'),
            toLocationId: int('Destination location id.'),
          },
          ['itemId', 'qty', 'fromLocationId', 'toLocationId'],
        ),
      },
    },
    ['lines'],
  ),
  annotations: write(false, true),
  warehouseEntry: true,
  handler: async (args, { api, newId, user }) => {
    const transfer = transferSummary(
      await api.json<Row>({
        target: 'warehouse',
        method: 'POST',
        path: `${WAREHOUSE}/transfers`,
        body: compact({
          commandId: newId(),
          requestedBy: user?.username,
          lines: rows(args.lines).map((l) => ({ itemId: l.itemId, qty: l.qty, fromLocationId: l.fromLocationId, toLocationId: l.toLocationId })),
        }),
      }),
    );
    return { text: `${describeTransfer(transfer)}. No stock has moved yet: run transfer_advance (submit, then execute).`, data: { transfer } };
  },
};

const transferAdvance: ToolDef = {
  name: 'transfer_advance',
  title: 'Submit, approve or execute a transfer',
  description:
    'Take a transfer one step further: "submit" a draft, "approve" one that waits for approval (WarehouseManager or WarehouseAdmin), "execute" to move the stock. Execute is refused when the source location does not hold enough available stock. Executing changes stock: get an explicit yes first. Returns the transfer with its new status.',
  inputSchema: obj(
    {
      transferId: uuid('Transfer id from transfer_create or transfers_list.'),
      action: str('The step.', { enum: ['submit', 'approve', 'execute'] }),
    },
    ['transferId', 'action'],
  ),
  annotations: write(false),
  warehouseEntry: true,
  handler: async (args, { api, newId, user }) => {
    const action = asString(args.action) as string;
    const transfer = transferSummary(
      await api.json<Row>({
        target: 'warehouse',
        method: 'POST',
        path: `${WAREHOUSE}/transfers/${asString(args.transferId)}/${action}`,
        body: compact({ commandId: newId(), approvedBy: action === 'approve' ? user?.username : undefined }),
      }),
    );
    return { text: `${describeTransfer(transfer)} after ${action}.`, data: { transfer } };
  },
};

export const WAREHOUSE_TOOLS: readonly ToolDef[] = [
  warehousesList,
  locationsSearch,
  locationStock,
  itemCost,
  suppliersSearch,
  inboundShipmentsList,
  inboundShipmentGet,
  transfersList,
  transferGet,
  locationCreate,
  inboundShipmentCreate,
  inboundShipmentReceive,
  inboundShipmentPost,
  putawayMove,
  transferCreate,
  transferAdvance,
];
