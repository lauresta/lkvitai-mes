// The read tools: Sales orders (Sales API, legacy SQL behind it) and the Warehouse master data a sales operator needs (customers, price groups, items
// with prices, available stock).

import { ApiError } from './api.js';
import { ArgumentError } from './validate.js';
import { READ, asBool, asNumber, asString, bool, compact, int, money, obj, str, type ToolDef } from './tool.js';

const SALES = '/api/sales';
const WAREHOUSE = '/api/warehouse/v1';

const AMOUNT_KINDS = ['Defined', 'Calculated', 'Discount', 'AfterDiscount', 'Paid', 'Debt'];

type Row = Record<string, unknown>;
const rows = (value: unknown): Row[] => (Array.isArray(value) ? (value as Row[]) : []);

function orderSummary(o: Row): Row {
  return compact({
    id: o.id,
    number: o.number,
    date: o.date,
    customer: o.customer,
    status: o.status,
    statusCode: o.statusCode,
    store: o.store,
    address: o.address,
    price: o.price,
    debt: o.debt,
    hasDebt: o.hasDebt === true ? true : undefined,
    isOverdue: o.isOverdue === true ? true : undefined,
    isVip: o.isVip === true ? true : undefined,
    hasNote: o.hasNote === true ? true : undefined,
  });
}

const ordersSearch: ToolDef = {
  name: 'orders_search',
  title: 'Search sales orders',
  description:
    'Search Cikada sales orders (newest first). Free text matches order number, customer, address and product text. Filter by status label and store exactly as orders_filters lists them (Lithuanian labels such as "Įvestas"), and by outstanding debt. Returns order id, number, date, customer, status, store, price and debt. Use order_get with the order number for lines, amounts and assigned employees.',
  inputSchema: obj({
    search: str('Free text: order number (e.g. "VLN-1234"), customer name, address or product text.', { maxLength: 200 }),
    status: str('Status label exactly as orders_filters returns it.', { maxLength: 100 }),
    store: str('Store / branch exactly as orders_filters returns it (e.g. "Vilnius").', { maxLength: 100 }),
    hasDebt: bool('true: only orders with outstanding debt.'),
    page: int('1-based page, default 1.', { minimum: 1 }),
    pageSize: int('Orders per page, default 25, at most 100.', { minimum: 1, maximum: 100 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const pageSize = asNumber(args.pageSize) ?? 25;
    const result = await api.json<Row>({
      target: 'sales',
      method: 'GET',
      path: `${SALES}/orders`,
      query: { search: asString(args.search), status: asString(args.status), store: asString(args.store), hasDebt: asBool(args.hasDebt), page: asNumber(args.page) ?? 1, pageSize },
    });
    const orders = rows(result.items).map(orderSummary);
    const lines = orders.map((o) => `${o.number} | ${o.date} | ${o.customer ?? ''} | ${o.status ?? ''} | ${o.store ?? ''} | ${money(o.price)} | debt ${money(o.debt)}`);
    return {
      text: `${orders.length} of ${result.total ?? orders.length} orders (page ${result.page ?? 1}).${lines.length ? `\n${lines.join('\n')}` : ''}`,
      data: { total: result.total, page: result.page, pageSize: result.pageSize, orders },
    };
  },
};

const orderGet: ToolDef = {
  name: 'order_get',
  title: 'Get a sales order',
  description:
    'One sales order by its number (as orders_search returns it): header (customer, status, store, address, operator), every line with dimensions, colour, quantity and price, the amounts (defined, calculated, discount, paid, debt) and the employees assigned (measurer, installer, ...). The `id` in the answer is the order id order entry tools take.',
  inputSchema: obj({ number: str('The order number exactly as orders_search returns it.', { minLength: 1, maxLength: 64 }) }, ['number']),
  annotations: READ,
  handler: async (args, { api }) => {
    const number = asString(args.number)!;
    let o: Row;
    try {
      o = await api.json<Row>({ target: 'sales', method: 'GET', path: `${SALES}/orders/${encodeURIComponent(number)}` });
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) throw new ApiError(`There is no order ${number}. Check the number with orders_search.`, 404);
      throw error;
    }
    const lines = rows(o.itemGroups).flatMap((group) =>
      rows(group.lines).map((line) =>
        compact({
          group: group.label,
          num: line.num,
          name: line.name,
          side: line.side,
          color: line.color,
          widthMm: line.width,
          heightMm: line.height,
          qty: line.qty,
          price: line.price,
          amount: line.amount,
          notes: line.notes,
          isAccessory: line.isAccessory === true ? true : undefined,
        }),
      ),
    );
    const amounts = rows(o.amounts).map((a) =>
      compact({ kind: typeof a.kind === 'number' ? AMOUNT_KINDS[a.kind] ?? a.kind : a.kind, label: a.label, amount: a.amount, percent: a.percent }),
    );
    const employees = rows(o.employees).map((e) => compact({ name: e.name, duty: e.dutyLabel, dutyCode: e.dutyCode, serviceDate: e.serviceDate, amount: e.amount }));
    const data = {
      ...orderSummary(o),
      operator: o.operator ?? undefined,
      lines,
      amounts,
      employees,
    };
    const text = [
      `Order ${o.number} (id ${o.id}) of ${o.date}: ${o.customer ?? ''}, ${o.status ?? ''}, ${o.store ?? ''}.`,
      `Price ${money(o.price)}, debt ${money(o.debt)}${o.isOverdue ? ' (overdue)' : ''}. ${lines.length} line(s), ${employees.length} employee assignment(s).`,
    ].join('\n');
    return { text, data: compact(data) };
  },
};

const ordersFilters: ToolDef = {
  name: 'orders_filters',
  title: 'Order status and store filters',
  description: 'The status labels and stores that currently have orders: the exact values orders_search accepts for `status` and `store`.',
  inputSchema: obj({}),
  annotations: READ,
  handler: async (_args, { api }) => {
    const result = await api.json<Row>({ target: 'sales', method: 'GET', path: `${SALES}/orders/filters` });
    const statuses = rows(result.statuses) as unknown as string[];
    const stores = rows(result.stores) as unknown as string[];
    return { text: `Statuses: ${statuses.join(', ')}\nStores: ${stores.join(', ')}`, data: { statuses, stores } };
  },
};

function address(a: unknown): string | undefined {
  if (!a || typeof a !== 'object') return undefined;
  const r = a as Row;
  const parts = [r.street, r.city, r.zipCode, r.country].filter((p) => typeof p === 'string' && p.trim());
  return parts.length ? parts.join(', ') : undefined;
}

const customersSearch: ToolDef = {
  name: 'customers_search',
  title: 'Search customers',
  description:
    'Search Warehouse customers by name or customer code (at most 200, ordered by name). Returns id (UUID), code, name, status, price group and default shipping address. These are Warehouse master-data customers; legacy order entry uses its own numeric customer ids (order_entry_catalog section "customers").',
  inputSchema: obj({
    search: str('Part of the customer name or code.', { maxLength: 200 }),
    activeOnly: bool('Only active customers (default true).'),
    limit: int('At most this many customers, default 50.', { minimum: 1, maximum: 200 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const result = await api.json<unknown>({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/customers`,
      query: { search: asString(args.search), activeOnly: asBool(args.activeOnly) ?? true },
    });
    const all = rows(result);
    const customers = all.slice(0, asNumber(args.limit) ?? 50).map((c) =>
      compact({ id: c.id, code: c.customerCode, name: c.name, status: c.status, priceGroupId: c.priceGroupId, priceGroup: c.priceGroupName, shippingAddress: address(c.defaultShippingAddress) }),
    );
    const lines = customers.map((c) => `${c.code} | ${c.name} | ${c.status ?? ''}${c.priceGroup ? ` | price group ${c.priceGroup}` : ''}`);
    return { text: `${customers.length} of ${all.length} customers.${lines.length ? `\n${lines.join('\n')}` : ''}`, data: { total: all.length, customers } };
  },
};

const UUID = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';

const customerGet: ToolDef = {
  name: 'customer_get',
  title: 'Get a customer',
  description: 'One Warehouse customer by id (UUID from customers_search): contact, status, payment terms, credit limit, price group, billing and shipping address.',
  inputSchema: obj({ id: str('Customer id (UUID) as customers_search returns it.', { pattern: UUID }) }, ['id']),
  annotations: READ,
  handler: async (args, { api }) => {
    const id = asString(args.id)!;
    let c: Row;
    try {
      c = await api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/customers/${id}` });
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) throw new ApiError(`There is no customer ${id}. Find the id with customers_search.`, 404);
      throw error;
    }
    const data = compact({
      id: c.id,
      code: c.customerCode,
      name: c.name,
      email: c.email,
      phone: c.phone,
      status: c.status,
      paymentTerms: c.paymentTerms,
      creditLimit: c.creditLimit,
      priceGroupId: c.priceGroupId,
      priceGroup: c.priceGroupName,
      billingAddress: address(c.billingAddress),
      shippingAddress: address(c.defaultShippingAddress),
    });
    return { text: `${data.code} ${data.name} (${data.status ?? ''})${data.priceGroup ? `, price group ${data.priceGroup}` : ''}${data.paymentTerms ? `, terms ${data.paymentTerms}` : ''}.`, data };
  },
};

const priceGroupsList: ToolDef = {
  name: 'price_groups_list',
  title: 'List price groups',
  description: 'The price groups (id, code, name, active). A customer\'s price group decides which item price applies; pass its id to items_search as priceGroupId.',
  inputSchema: obj({}),
  annotations: READ,
  handler: async (_args, { api }) => {
    const groups = rows(await api.json<unknown>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/price-groups` })).map((g) =>
      compact({ id: g.id, code: g.code, name: g.name, isActive: g.isActive }),
    );
    return { text: groups.map((g) => `${g.id} | ${g.code} | ${g.name}${g.isActive === false ? ' (inactive)' : ''}`).join('\n') || 'No price groups.', data: { priceGroups: groups } };
  },
};

interface StockTotals {
  onHand: number;
  reserved: number;
  available: number;
  locations: number;
}

const emptyTotals = (): StockTotals => ({ onHand: 0, reserved: 0, available: 0, locations: 0 });

function accumulate(totals: StockTotals, row: Row): void {
  totals.onHand += asNumber(row.qty) ?? 0;
  totals.reserved += asNumber(row.reservedQty) ?? 0;
  totals.available += asNumber(row.availableQty) ?? 0;
  totals.locations += 1;
}

function addStock(map: Map<string, StockTotals>, key: string, row: Row): void {
  const totals = map.get(key) ?? emptyTotals();
  accumulate(totals, row);
  map.set(key, totals);
}

const round = (value: number): number => Math.round(value * 10000) / 10000;

const itemsSearch: ToolDef = {
  name: 'items_search',
  title: 'Search items with prices and stock',
  description:
    'Search the item catalogue by SKU, name or #tag. Each item comes with its base (sales) price, purchase price and, with includeStock (default), the quantity on hand and available now across real warehouse locations. Give priceGroupId (a customer\'s price group, see customer_get / price_groups_list) to also get the price for that group. Use stock_get for the per-location breakdown of one item.',
  inputSchema: obj({
    search: str('Part of the SKU or name, or a #tag.', { maxLength: 200 }),
    categoryId: int('Only items of this category.'),
    status: str('Item status filter, e.g. "Active".', { maxLength: 50 }),
    priceGroupId: int('Also return the price for this price group (falls back to the base price where the group has no override).'),
    includeStock: bool('Include on-hand / available quantities (default true).'),
    page: int('1-based page, default 1.', { minimum: 1 }),
    pageSize: int('Items per page, default 20, at most 50.', { minimum: 1, maximum: 50 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const search = asString(args.search);
    const priceGroupId = asNumber(args.priceGroupId);
    const includeStock = asBool(args.includeStock) ?? true;
    const page = await api.json<Row>({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/items`,
      query: { search, categoryId: asNumber(args.categoryId), status: asString(args.status), pageNumber: asNumber(args.page) ?? 1, pageSize: asNumber(args.pageSize) ?? 20 },
    });
    const items = rows(page.items);

    const [groupPrices, stockRows] = await Promise.all([
      priceGroupId !== undefined
        ? api.json<unknown>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/price-groups/${priceGroupId}/item-prices` }).then((r) => new Map(rows(r).map((p) => [p.itemId as number, p.amount as number])))
        : Promise.resolve(undefined),
      // one stock query for the page: by the search text when there is one (it matches SKU or name, as the item search does), else by each item
      includeStock && items.length > 0
        ? search
          ? api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/stock/available`, query: { sku: search.replace(/^#/, ''), includeReserved: true, pageSize: 1000 } }).then((r) => rows(r.items))
          : Promise.all(items.map((i) => api.json<Row>({ target: 'warehouse', method: 'GET', path: `${WAREHOUSE}/stock/available`, query: { itemId: i.id as number, includeReserved: true, pageSize: 1000 } }).then((r) => rows(r.items)))).then((all) => all.flat())
        : Promise.resolve(undefined),
    ]);

    const stock = new Map<string, StockTotals>();
    for (const row of stockRows ?? []) addStock(stock, String(row.internalSku ?? ''), row);
    const needle = search?.toLowerCase();

    const result = items.map((i) => {
      const sku = String(i.internalSKU ?? i.internalSku ?? '');
      const totals = stock.get(sku);
      // an item found only by tag is not in a SKU/name stock query: its stock is unknown here, not zero
      const covered = !needle || sku.toLowerCase().includes(needle) || String(i.name ?? '').toLowerCase().includes(needle);
      const stockOut = !includeStock ? undefined : totals ? { onHand: round(totals.onHand), reserved: round(totals.reserved), available: round(totals.available), locations: totals.locations } : covered ? { onHand: 0, reserved: 0, available: 0, locations: 0 } : null;
      const override = groupPrices?.get(i.id as number);
      return compact({
        id: i.id,
        sku,
        name: i.name,
        category: i.categoryName,
        uom: i.baseUoM ?? i.baseUom,
        status: i.status,
        basePrice: i.basePrice,
        purchasePrice: i.purchasePrice,
        groupPrice: priceGroupId === undefined ? undefined : override ?? i.basePrice,
        groupPriceIsOverride: priceGroupId === undefined ? undefined : override !== undefined,
        stock: stockOut,
      });
    });
    const lines = result.map((i) => {
      const s = i.stock as Row | null | undefined;
      return `${i.sku} | ${i.name} | base ${money(i.basePrice)}${priceGroupId !== undefined ? ` | group ${money(i.groupPrice)}` : ''} | purchase ${money(i.purchasePrice)}${s ? ` | available ${s.available} ${i.uom ?? ''}` : s === null ? ' | stock: use stock_get' : ''}`;
    });
    return {
      text: `${result.length} of ${page.totalCount ?? result.length} items.${lines.length ? `\n${lines.join('\n')}` : ''}`,
      data: { total: page.totalCount, page: page.pageNumber, pageSize: page.pageSize, items: result },
    };
  },
};

const stockGet: ToolDef = {
  name: 'stock_get',
  title: 'Available stock of an item',
  description:
    'Available stock per location for one item (itemId from items_search) or for every item whose SKU or name contains `sku`. Quantities: on hand, reserved, available (on hand minus hard-locked). Read model, may lag a few seconds behind the warehouse.',
  inputSchema: obj({
    itemId: int('Item id from items_search.'),
    sku: str('SKU or part of the SKU / name (when itemId is not known).', { maxLength: 100 }),
    warehouse: str('Only this warehouse id.', { maxLength: 100 }),
  }),
  annotations: READ,
  handler: async (args, { api }) => {
    const itemId = asNumber(args.itemId);
    const sku = asString(args.sku);
    if (itemId === undefined && !sku) throw new ArgumentError('give itemId (from items_search) or sku');
    const result = await api.json<Row>({
      target: 'warehouse',
      method: 'GET',
      path: `${WAREHOUSE}/stock/available`,
      query: { itemId, sku, warehouse: asString(args.warehouse), includeReserved: true, pageSize: 1000 },
    });
    const bySku = new Map<string, { itemId: unknown; sku: string; name: unknown; uom: unknown; totals: StockTotals; locations: Row[] }>();
    for (const row of rows(result.items)) {
      const key = String(row.internalSku ?? '');
      const entry = bySku.get(key) ?? { itemId: row.itemId, sku: key, name: row.itemName, uom: row.baseUom, totals: emptyTotals(), locations: [] };
      accumulate(entry.totals, row);
      entry.locations.push(compact({ location: row.locationCode, lot: row.lotNumber, expiry: row.expiryDate, onHand: row.qty, reserved: row.reservedQty, available: row.availableQty }));
      bySku.set(key, entry);
    }
    const items = [...bySku.values()].map((e) => ({
      itemId: e.itemId,
      sku: e.sku,
      name: e.name,
      uom: e.uom,
      onHand: round(e.totals.onHand),
      reserved: round(e.totals.reserved),
      available: round(e.totals.available),
      locations: e.locations,
    }));
    const lines = items.map((i) => `${i.sku} ${i.name ?? ''}: available ${i.available} ${i.uom ?? ''} (on hand ${i.onHand}, reserved ${i.reserved}) in ${i.locations.length} location(s)`);
    return {
      text: items.length ? lines.join('\n') : 'No stock on hand for that item.',
      data: { items, asOf: result.projectionTimestamp },
    };
  },
};

const whoami: ToolDef = {
  name: 'whoami',
  title: 'Who am I',
  description:
    'The signed-in user and their Cikada roles. Use it to explain a 403: orders and order entry need SalesAdmin (writes also a legacy SQL login), items and stock need Operator or above, the management reports need SalesAdmin or Reports.',
  inputSchema: obj({}),
  annotations: READ,
  handler: async (_args, { user }) => {
    const roles = user?.roles ?? [];
    return {
      text: `${user?.name ?? user?.username ?? 'Unknown user'}${user?.username ? ` (${user.username})` : ''}: roles ${roles.length ? roles.join(', ') : 'none'}.`,
      data: compact({ username: user?.username, name: user?.name, email: user?.email, roles }),
    };
  },
};

export const READ_TOOLS: readonly ToolDef[] = [whoami, ordersSearch, orderGet, ordersFilters, customersSearch, customerGet, priceGroupsList, itemsSearch, stockGet];
