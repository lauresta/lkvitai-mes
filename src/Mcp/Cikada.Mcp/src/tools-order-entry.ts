// Order entry: the legacy order editor's contract (Sales API /api/sales/order-entry/*). Offered only with
// MCP_ENABLE_ORDER_ENTRY=true. Every write takes the order's revision (`expectedRevision`, from order_entry_get or the previous write's answer) and a
// command id: the API refuses a stale revision, and a repeated command id is a replay, not a second write. No id is ever guessed: each comes from the
// catalog, references or an earlier answer, as the descriptions say.

import { ApiError, type ApiCall, type CikadaApi } from './api.js';
import { ArgumentError } from './validate.js';
import { DATE, READ, asBool, asNumber, asString, bool, compact, int, money, num, obj, str, write, type ToolContext, type ToolDef, type ToolResult } from './tool.js';
import type { Schema } from './validate.js';

const OE = '/api/sales/order-entry';
export const NOT_DEPLOYED = 'Order entry is not deployed on this environment yet.';

type Row = Record<string, unknown>;
const rows = (value: unknown): Row[] => (Array.isArray(value) ? (value as Row[]) : []);

/**
 * One order entry call. A 404 is ambiguous (no such order, or no order entry at all on this Sales API): the references endpoint, which exists wherever
 * order entry does, tells them apart.
 */
async function oe<T = unknown>(api: CikadaApi, call: Omit<ApiCall, 'target'>, notFound?: string): Promise<T> {
  try {
    return await api.json<T>({ ...call, target: 'sales' });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      const probe = await api.call({ target: 'sales', method: 'GET', path: `${OE}/references/categories` }).catch(() => undefined);
      if (!probe || probe.status === 404) throw new ApiError(NOT_DEPLOYED, 404, 'ORDER_ENTRY_NOT_DEPLOYED');
      throw new ApiError(notFound ?? error.message, 404, error.code);
    }
    throw error;
  }
}

const ORDER_ID = int('Order id: `id` from order_get / orders_search, or `orderId` from order_create.', { minimum: 1 });
const PRODUCT_ID = int('Product (line) id from order_entry_get `products[].id`, or `productId` from order_product_add.', { minimum: 1 });
const REVISION = str('The order revision from the latest order_entry_get or write answer. A stale revision is refused; read the order again.', { minLength: 1, maxLength: 200 });
const COMMAND_ID = str('Optional idempotency key (UUID). Leave it out for a new command; reuse the commandId from an answer only to retry the very same command.', {
  pattern: '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
});

/** The editor snapshot, compact: what a model needs to continue (revision, header, products, address, gates). */
function snapshot(order: Row | undefined): Row {
  if (!order) return {};
  const h = (order.header ?? {}) as Row;
  return compact({
    orderId: h.id,
    number: h.number,
    revision: order.revision,
    writesAllowed: order.writesAllowed,
    confirmationEnabled: order.confirmationEnabled,
    cancellationUnavailableReason: order.cancellationUnavailableReason,
    warnings: rows(order.warnings).length ? order.warnings : undefined,
    header: compact({
      seriesId: h.seriesId,
      date: h.date,
      due: h.due,
      customerId: h.customerId,
      customer: h.customerName,
      storeId: h.storeId,
      store: h.storeName,
      managerId: h.managerId,
      manager: h.managerName,
      consultantId: h.consultantId,
      consultant: h.consultantName,
      priorityId: h.priorityId,
      mountingDate: h.mountingDate,
      note: h.note,
      discountPercent: h.discountPercent,
      state: h.stateName,
      cancelled: h.cancelled === true ? true : undefined,
      canEdit: h.canEdit,
      storedTotal: h.storedTotal,
      calculatedTotal: h.calculatedTotal,
      paid: h.paid,
    }),
    products: rows(order.products).map((p) =>
      compact({
        id: p.id,
        typeId: p.typeId,
        type: p.typeName,
        category: p.categoryName,
        widthMm: p.widthMm,
        heightMm: p.heightMm,
        quantity: p.quantity,
        colourId: p.colourId,
        colour: p.colourCode,
        control: p.control,
        unitPrice: p.unitPrice,
        amount: p.amount,
        note: p.note,
        parameters: rows(p.parameters).length ? p.parameters : undefined,
        state: p.stateName,
        canEdit: p.canEdit,
        canDelete: p.canDelete,
        accessories: rows(p.accessories).length ? rows(p.accessories).map((a) => compact({ accessoryId: a.accessoryId, name: a.name, quantity: a.quantity, unitPrice: a.unitPrice })) : undefined,
      }),
    ),
    address: order.address ?? undefined,
  });
}

function snapshotText(s: Row): string {
  const h = (s.header ?? {}) as Row;
  const products = rows(s.products);
  return [
    `Order ${s.number ?? ''} (id ${s.orderId}), revision ${s.revision}${s.writesAllowed === false ? ', read-only' : ''}.`,
    `${h.customer ?? ''} | ${h.store ?? ''} | ${h.date ?? ''} due ${h.due ?? ''} | ${h.state ?? ''}${h.cancelled ? ' | CANCELLED' : ''} | total ${money(h.calculatedTotal)} paid ${money(h.paid)}`,
    ...products.map((p) => `  #${p.id} ${p.type ?? p.typeId} ${p.widthMm ?? '?'}x${p.heightMm ?? '?'} x${p.quantity ?? '?'} colour ${p.colour ?? p.colourId ?? '-'} = ${money(p.amount)}`),
  ].join('\n');
}

/** A write's answer: the new snapshot, the product or production document it made, whether it was a replay, and the command id to retry with. */
function commandResult(result: Row, commandId: string, what: string): ToolResult {
  const inner = (result.result && typeof result.result === 'object' ? result.result : result) as Row;
  const order = snapshot(inner.order as Row | undefined);
  const data = compact({
    ...order,
    productId: inner.productId ?? result.productId,
    productionDocumentId: inner.productionDocumentId ?? result.productionDocumentId,
    replayed: inner.replayed === true ? true : undefined,
    commandId,
  });
  return { text: `${what}${data.replayed ? ' (replayed: this command had already been applied)' : ''}.\n${snapshotText(order)}`, data };
}

async function currentOrder(api: CikadaApi, orderId: number): Promise<Row> {
  return oe<Row>(api, { method: 'GET', path: `${OE}/orders/${orderId}` }, `There is no order with id ${orderId}.`);
}

function requireRevision(order: Row, expected: string): void {
  if (order.revision !== expected) {
    throw new ArgumentError(`the order changed since revision ${expected} (it is now ${String(order.revision)}): read it again with order_entry_get and confirm the change with the user`);
  }
}

// ------------------------------------------------------------------------------------------------------------------------------------- helper reads

const CATALOG_SECTIONS = ['series', 'categories', 'types', 'colours', 'accessories', 'customers', 'employees', 'stores', 'duties', 'priorities'] as const;

const orderEntryCatalog: ToolDef = {
  name: 'order_entry_catalog',
  title: 'Order entry catalog',
  description:
    'The live legacy references order entry needs, one section at a time: series (order number prefix + default customer/store/manager), categories, product types (with price entry mode and parameter formulas), colours / materials (with free stock), accessories (with price), customers, employees (managers, consultants), stores, duties, priorities. Filter with `search` (name or code) and, for types / colours / accessories, `categoryId`. The ids here are what order_create and the product tools take.',
  inputSchema: obj(
    {
      section: { type: 'string', enum: [...CATALOG_SECTIONS], description: 'Which list.' },
      search: str('Part of the name or code.', { maxLength: 100 }),
      categoryId: int('types / colours / accessories: only those of this product category.'),
      limit: int('At most this many rows, default 50.', { minimum: 1, maximum: 500 }),
    },
    ['section'],
  ),
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api }) => {
    const section = args.section as (typeof CATALOG_SECTIONS)[number];
    const catalog = await oe<Row>(api, { method: 'GET', path: `${OE}/catalog` });
    const needle = asString(args.search)?.toLowerCase();
    const categoryId = asNumber(args.categoryId);
    let list = rows(catalog[section]);
    if (needle) list = list.filter((r) => [r.name, r.code].some((v) => typeof v === 'string' && v.toLowerCase().includes(needle)));
    if (categoryId !== undefined) {
      list = list.filter((r) => (Array.isArray(r.categoryIds) ? (r.categoryIds as number[]).includes(categoryId) : r.categoryId === undefined || r.categoryId === categoryId));
    }
    const limit = asNumber(args.limit) ?? 50;
    const items = list.slice(0, limit).map((r) => compact(r));
    const label = (r: Row) => [r.id, r.code, r.name].filter((v) => v !== undefined && v !== null && v !== '').join(' | ');
    return {
      text: `${items.length} of ${list.length} ${section}.${items.length ? `\n${items.map(label).join('\n')}` : ''}`,
      data: { section, loadedAtUtc: catalog.loadedAtUtc, total: list.length, items },
    };
  },
};

const orderEntryReferences: ToolDef = {
  name: 'order_entry_references',
  title: 'Order entry references',
  description:
    'Paged legacy references: `series`, `categories`, `types` (filter by categoryId / search) or one `type` definition (typeId) with its parameter slots, labels, formulas and pricing rules (price entry mode, chargeable minimum width / height / area). Read a type definition before configuring a product of that type.',
  inputSchema: obj(
    {
      kind: { type: 'string', enum: ['series', 'categories', 'types', 'type'], description: 'Which reference.' },
      typeId: int('kind=type: the product type id.'),
      categoryId: int('kind=types: only types of this category.'),
      search: str('series / types: part of the code or name.', { maxLength: 100 }),
      includeInactive: bool('Include hidden series / inactive categories and types.'),
      page: int('1-based page, default 1.', { minimum: 1 }),
      pageSize: int('Rows per page, default 100.', { minimum: 1, maximum: 500 }),
    },
    ['kind'],
  ),
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api }) => {
    const kind = args.kind as string;
    const inactive = asBool(args.includeInactive);
    const paging = { page: asNumber(args.page), pageSize: asNumber(args.pageSize) };
    let data: unknown;
    switch (kind) {
      case 'series':
        data = await oe(api, { method: 'GET', path: `${OE}/references/series`, query: { search: asString(args.search), includeHidden: inactive, ...paging } });
        break;
      case 'categories':
        data = await oe(api, { method: 'GET', path: `${OE}/references/categories`, query: { includeInactive: inactive } });
        break;
      case 'types':
        data = await oe(api, { method: 'GET', path: `${OE}/references/types`, query: { categoryId: asNumber(args.categoryId), search: asString(args.search), includeInactive: inactive, ...paging } });
        break;
      default: {
        const typeId = asNumber(args.typeId);
        if (typeId === undefined) throw new ArgumentError('kind=type needs typeId');
        data = await oe(api, { method: 'GET', path: `${OE}/references/types/${typeId}` }, `There is no product type ${typeId}.`);
      }
    }
    const body = Array.isArray(data) ? { items: data } : ((data ?? {}) as Row);
    const count = Array.isArray(data) ? data.length : Array.isArray((data as Row)?.items) ? ((data as Row).items as unknown[]).length : 1;
    return { text: `${kind}: ${count} row(s).`, data: { kind, ...body } };
  },
};

const orderEntryAddresses: ToolDef = {
  name: 'order_entry_addresses',
  title: 'Find an address (street id)',
  description:
    'The legacy address tree (city → street) for order_address_set: search by name, walk children with parentId, or read one node by id. A street id is a node without children (hasChildren=false) under the right city.',
  inputSchema: obj({
    search: str('Part of a city or street name.', { maxLength: 100 }),
    parentId: int('Children of this node.'),
    id: int('One node by id.'),
  }),
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api }) => {
    const list = rows(await oe(api, { method: 'GET', path: `${OE}/addresses`, query: { search: asString(args.search), parentId: asNumber(args.parentId), id: asNumber(args.id) } }));
    const items = list.slice(0, 100).map((a) => compact(a));
    return { text: `${items.length} address node(s).${items.length ? `\n${items.map((a) => `${a.id} | ${a.path ?? a.name}${a.hasChildren ? ' (has children)' : ''}`).join('\n')}` : ''}`, data: { items } };
  },
};

const orderEntryUnitPrice: ToolDef = {
  name: 'order_entry_unit_price',
  title: 'List unit price',
  description:
    'The list unit price of a product type in a colour / material for given dimensions and quantity on the order date. A lookup only, not a quote or reservation; `status` says Found, Missing, Ambiguous, InvalidReference or InvalidPriceData.',
  inputSchema: obj(
    {
      typeId: int('Product type id (order_entry_catalog section types).'),
      componentId: int('Colour / material id (order_entry_catalog section colours).'),
      widthMm: int('Width in millimetres.', { minimum: 0 }),
      heightMm: int('Height in millimetres.', { minimum: 0 }),
      quantity: num('Quantity.', { minimum: 0 }),
      orderDate: DATE('The order date the price applies on; defaults to today'),
    },
    ['typeId', 'componentId', 'widthMm', 'heightMm', 'quantity'],
  ),
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api, today }) => {
    const result = await oe<Row>(api, {
      method: 'GET',
      path: `${OE}/unit-price`,
      query: { typeId: args.typeId as number, componentId: args.componentId as number, widthMm: args.widthMm as number, heightMm: args.heightMm as number, quantity: args.quantity as number, orderDate: asString(args.orderDate) ?? today() },
    });
    const statuses = ['Found', 'Missing', 'Ambiguous', 'InvalidReference', 'InvalidPriceData'];
    const status = typeof result.status === 'number' ? statuses[result.status] ?? result.status : result.status;
    const data = compact({ ...result, status });
    return { text: status === 'Found' ? `Unit price ${money(result.unitPrice)}.` : `No single unit price: ${String(status)}.`, data };
  },
};

const orderEntryProductPreview: ToolDef = {
  name: 'order_entry_product_preview',
  title: 'Preview a product configuration',
  description:
    'Calculates a product configuration without saving it: the parameter values the type\'s formulas give, the material it needs, the free material, whether it is short, and a calculationRevision to pass to order_product_update. Use before order_product_update when dimensions, colour or parameters change.',
  inputSchema: obj(
    {
      typeId: int('Product type id.'),
      widthMm: int('Width in millimetres.', { minimum: 0 }),
      heightMm: int('Height in millimetres.', { minimum: 0 }),
      quantity: num('Quantity.', { minimum: 0 }),
      colourId: int('Colour / material id.'),
      control: str('Control side / type, as the type uses it (e.g. "K", "D").', { maxLength: 50 }),
      parameters: { type: 'array', items: { type: ['string', 'null'] }, maxItems: 40, description: 'Parameter slot values in slot order (null for empty).' },
      recalculateParameters: bool('Recalculate formula parameters (default true).'),
      orderId: int('The order the product belongs to (for its material reservation).'),
      productId: int('The product being changed, when it exists.'),
    },
    ['typeId', 'widthMm', 'heightMm', 'quantity'],
  ),
  // a calculation: nothing is saved
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api }) => {
    const body = {
      typeId: args.typeId,
      widthMm: args.widthMm,
      heightMm: args.heightMm,
      quantity: args.quantity,
      colourId: asNumber(args.colourId) ?? null,
      control: asString(args.control) ?? null,
      parameters: Array.isArray(args.parameters) ? args.parameters : [],
      recalculateParameters: asBool(args.recalculateParameters) ?? true,
      orderId: asNumber(args.orderId) ?? null,
      productId: asNumber(args.productId) ?? null,
    };
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/product-preview`, body });
    return {
      text: `Material needed ${result.requiredMaterial ?? '-'} ${result.materialUnit ?? ''}, free ${result.freeMaterial ?? '-'}${result.isShort ? ' (SHORT)' : ''}.`,
      data: compact(result),
    };
  },
};

const orderEntryGet: ToolDef = {
  name: 'order_entry_get',
  title: 'Open an order for editing',
  description:
    'The order as the editor sees it: its current revision (every write needs it), header with ids, products with ids and configuration, address, and whether writes, confirmation and cancellation are allowed. Read it before any write and after a refused one.',
  inputSchema: obj({ orderId: ORDER_ID }, ['orderId']),
  annotations: READ,
  orderEntry: true,
  handler: async (args, { api }) => {
    const order = snapshot(await currentOrder(api, args.orderId as number));
    return { text: snapshotText(order), data: order };
  },
};

// ------------------------------------------------------------------------------------------------------------------------------------- writes

const HEADER_FIELDS: Record<string, Schema> = {
  date: DATE('Order date'),
  due: DATE('Due date'),
  customerId: int('Legacy customer id (order_entry_catalog section customers, or the series default).'),
  storeId: int('Store id (order_entry_catalog section stores, or the series default).'),
  managerId: int('Manager employee id (section employees).'),
  consultantId: int('Consultant employee id (section employees).'),
  priorityId: int('Priority id (section priorities); default 2.'),
  mountingDate: DATE('Mounting date'),
  note: str('Order note.', { maxLength: 2000 }),
  discountPercent: num('Discount percent, 0-100.', { minimum: 0, maximum: 100 }),
};

const orderCreate: ToolDef = {
  name: 'order_create',
  title: 'Create an order',
  description:
    'Creates a new legacy sales order header. Needs the series (order_entry_catalog section series: its `code` is the number prefix and it suggests default customer / store / manager / consultant), the full number "<SERIES>-<n>" or "<SERIES>-<n>-<group>", order and due dates, customer and store. Ask the user for anything not given; never invent ids. Then add products with order_product_add.',
  inputSchema: obj(
    {
      seriesId: int('Series id (order_entry_catalog section series).'),
      number: str('Order number with the series prefix, e.g. "VLN-1234".', { minLength: 3, maxLength: 40 }),
      ...HEADER_FIELDS,
      commandId: COMMAND_ID,
    },
    ['seriesId', 'number', 'date', 'due', 'customerId', 'storeId'],
  ),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const commandId = asString(args.commandId) ?? newId();
    const header = {
      seriesId: args.seriesId,
      number: asString(args.number),
      date: args.date,
      due: args.due,
      customerId: args.customerId,
      storeId: args.storeId,
      managerId: asNumber(args.managerId) ?? null,
      consultantId: asNumber(args.consultantId) ?? null,
      priorityId: asNumber(args.priorityId) ?? 2,
      mountingDate: asString(args.mountingDate) ?? null,
      note: asString(args.note) ?? null,
      discountPercent: asNumber(args.discountPercent) ?? 0,
    };
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/orders`, body: { commandId, header } });
    return commandResult(result, commandId, 'Order created');
  },
};

const orderUpdate: ToolDef = {
  name: 'order_update',
  title: 'Change an order header',
  description:
    'Changes header fields of an order (dates, customer, store, manager, consultant, priority, mounting date, note, discount). Give only the fields to change; the others keep their current values. Series and number cannot change.',
  inputSchema: obj({ orderId: ORDER_ID, expectedRevision: REVISION, ...HEADER_FIELDS, commandId: COMMAND_ID }, ['orderId', 'expectedRevision']),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const order = await currentOrder(api, orderId);
    requireRevision(order, args.expectedRevision as string);
    const h = (order.header ?? {}) as Row;
    const pick = (name: string, fallback: unknown) => (args[name] !== undefined ? args[name] : fallback ?? null);
    const header = {
      seriesId: h.seriesId,
      number: h.number,
      date: pick('date', h.date),
      due: pick('due', h.due),
      customerId: pick('customerId', h.customerId),
      storeId: pick('storeId', h.storeId),
      managerId: pick('managerId', h.managerId),
      consultantId: pick('consultantId', h.consultantId),
      priorityId: pick('priorityId', h.priorityId ?? 2),
      mountingDate: pick('mountingDate', h.mountingDate),
      note: pick('note', h.note),
      discountPercent: pick('discountPercent', h.discountPercent ?? 0),
    };
    if (header.date === null || header.due === null || header.customerId === null || header.storeId === null) {
      throw new ArgumentError('the order has no date, due date, customer or store yet: give them');
    }
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(api, { method: 'PUT', path: `${OE}/orders/${orderId}`, body: { commandId, expectedRevision: order.revision, header } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, 'Order header saved');
  },
};

const orderProductAdd: ToolDef = {
  name: 'order_product_add',
  title: 'Add a product to an order',
  description:
    'Adds an empty product line of a product type (order_entry_catalog section types) to an order and returns its productId. Then set its dimensions, colour, quantity and price with order_product_update.',
  inputSchema: obj({ orderId: ORDER_ID, expectedRevision: REVISION, typeId: int('Product type id.'), commandId: COMMAND_ID }, ['orderId', 'expectedRevision', 'typeId']),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/orders/${orderId}/products`, body: { commandId, expectedRevision: args.expectedRevision, typeId: args.typeId } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, 'Product added');
  },
};

const orderProductUpdate: ToolDef = {
  name: 'order_product_update',
  title: 'Configure a product line',
  description:
    'Sets a product line\'s configuration: dimensions, quantity, colour / material, control, parameters, note and price. Give only what changes; the rest keeps its current value. With useListPrice (default when no unitPrice is given) the list price applies; a manual unitPrice only where the type allows it. Pass the calculationRevision from order_entry_product_preview when you previewed the change.',
  inputSchema: obj(
    {
      orderId: ORDER_ID,
      productId: PRODUCT_ID,
      expectedRevision: REVISION,
      typeId: int('Change the product type (rare).'),
      widthMm: int('Width in millimetres.', { minimum: 0 }),
      heightMm: int('Height in millimetres.', { minimum: 0 }),
      quantity: num('Quantity.', { minimum: 0 }),
      colourId: int('Colour / material id (order_entry_catalog section colours).'),
      control: str('Control side / type.', { maxLength: 50 }),
      unitPrice: num('Manual unit price (only where the type permits manual prices).', { minimum: 0 }),
      useListPrice: bool('Use the list price (default true unless unitPrice is given).'),
      note: str('Line note.', { maxLength: 2000 }),
      parameters: { type: 'array', items: { type: ['string', 'null'] }, maxItems: 40, description: 'All parameter slot values in slot order (null for empty).' },
      recalculateParameters: bool('Recalculate formula parameters (default true).'),
      calculationRevision: str('calculationRevision from order_entry_product_preview.', { maxLength: 200 }),
      commandId: COMMAND_ID,
    },
    ['orderId', 'productId', 'expectedRevision'],
  ),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const productId = args.productId as number;
    const order = await currentOrder(api, orderId);
    requireRevision(order, args.expectedRevision as string);
    const current = rows(order.products).find((p) => p.id === productId);
    if (!current) throw new ArgumentError(`order ${orderId} has no product ${productId}: read the order with order_entry_get`);
    const pick = (name: string, fallback: unknown) => (args[name] !== undefined ? args[name] : fallback);
    const unitPrice = asNumber(args.unitPrice);
    const product: Row = {
      typeId: pick('typeId', current.typeId),
      widthMm: pick('widthMm', current.widthMm),
      heightMm: pick('heightMm', current.heightMm),
      quantity: pick('quantity', current.quantity),
      colourId: pick('colourId', current.colourId),
      control: pick('control', current.control ?? null),
      unitPrice: unitPrice ?? current.unitPrice ?? 0,
      useListPrice: asBool(args.useListPrice) ?? unitPrice === undefined,
      note: pick('note', current.note ?? null),
      parameters: pick('parameters', Array.isArray(current.parameters) ? current.parameters : []),
      recalculateParameters: asBool(args.recalculateParameters) ?? true,
    };
    if (asString(args.calculationRevision)) product.calculationRevision = asString(args.calculationRevision);
    for (const field of ['typeId', 'widthMm', 'heightMm', 'quantity', 'colourId']) {
      if (product[field] === null || product[field] === undefined) throw new ArgumentError(`${field} is required: the line has none yet`);
    }
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(
      api,
      { method: 'PUT', path: `${OE}/orders/${orderId}/products/${productId}`, body: { commandId, expectedRevision: order.revision, product } },
      `There is no product ${productId} on order ${orderId}.`,
    );
    return commandResult(result, commandId, 'Product saved');
  },
};

const orderProductRemove: ToolDef = {
  name: 'order_product_remove',
  title: 'Remove a product line',
  description: 'Deletes a product line (and its accessories) from an order. Confirm with the user first.',
  inputSchema: obj({ orderId: ORDER_ID, productId: PRODUCT_ID, expectedRevision: REVISION, commandId: COMMAND_ID }, ['orderId', 'productId', 'expectedRevision']),
  annotations: write(true),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const productId = args.productId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(
      api,
      { method: 'DELETE', path: `${OE}/orders/${orderId}/products/${productId}`, body: { commandId, expectedRevision: args.expectedRevision } },
      `There is no product ${productId} on order ${orderId}.`,
    );
    return commandResult(result, commandId, `Product ${productId} removed`);
  },
};

const orderAddressSet: ToolDef = {
  name: 'order_address_set',
  title: 'Set the delivery / mounting address',
  description: 'Sets the order\'s address: a street id from order_entry_addresses, plus building, room / flat, phone and a note.',
  inputSchema: obj(
    {
      orderId: ORDER_ID,
      expectedRevision: REVISION,
      streetId: int('Street id from order_entry_addresses.'),
      building: str('Building number.', { maxLength: 50 }),
      room: str('Flat / room.', { maxLength: 50 }),
      phone: str('Contact phone.', { maxLength: 50 }),
      note: str('Address note.', { maxLength: 500 }),
      commandId: COMMAND_ID,
    },
    ['orderId', 'expectedRevision', 'streetId'],
  ),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const commandId = asString(args.commandId) ?? newId();
    const address = { streetId: args.streetId, building: asString(args.building) ?? null, room: asString(args.room) ?? null, phone: asString(args.phone) ?? null, note: asString(args.note) ?? null };
    const result = await oe<Row>(api, { method: 'PUT', path: `${OE}/orders/${orderId}/address`, body: { commandId, expectedRevision: args.expectedRevision, address } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, 'Address saved');
  },
};

const orderProductDuplicate: ToolDef = {
  name: 'order_product_duplicate',
  title: 'Duplicate a product line',
  description: 'Copies a product line (configuration and accessories) into a new line on the same order and returns its productId. Change the copy with order_product_update.',
  inputSchema: obj({ orderId: ORDER_ID, productId: PRODUCT_ID, expectedRevision: REVISION, commandId: COMMAND_ID }, ['orderId', 'productId', 'expectedRevision']),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const productId = args.productId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(
      api,
      { method: 'POST', path: `${OE}/orders/${orderId}/products/${productId}/duplicate`, body: { commandId, expectedRevision: args.expectedRevision } },
      `There is no product ${productId} on order ${orderId}.`,
    );
    return commandResult(result, commandId, `Product ${productId} duplicated`);
  },
};

const orderAccessorySet: ToolDef = {
  name: 'order_accessory_set',
  title: 'Add or change an accessory',
  description:
    'Adds an accessory to a product line, or changes its quantity / price when the line already has it. The accessory id comes from order_entry_catalog section accessories (filtered by the line\'s categoryId); its `price` is the list price to use unless the user gives another.',
  inputSchema: obj(
    {
      orderId: ORDER_ID,
      productId: PRODUCT_ID,
      expectedRevision: REVISION,
      accessoryId: int('Accessory id (order_entry_catalog section accessories).', { minimum: 1 }),
      quantity: num('Quantity.', { minimum: 0 }),
      unitPrice: num('Unit price (the catalog price unless the user says otherwise).', { minimum: 0 }),
      commandId: COMMAND_ID,
    },
    ['orderId', 'productId', 'expectedRevision', 'accessoryId', 'quantity', 'unitPrice'],
  ),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const productId = args.productId as number;
    const commandId = asString(args.commandId) ?? newId();
    const body = { commandId, expectedRevision: args.expectedRevision, accessoryId: args.accessoryId, quantity: args.quantity, unitPrice: args.unitPrice };
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/orders/${orderId}/products/${productId}/accessories`, body }, `There is no product ${productId} on order ${orderId}.`);
    return commandResult(result, commandId, 'Accessory saved');
  },
};

const orderAccessoryRemove: ToolDef = {
  name: 'order_accessory_remove',
  title: 'Remove an accessory',
  description: 'Removes an accessory from a product line.',
  inputSchema: obj(
    { orderId: ORDER_ID, productId: PRODUCT_ID, expectedRevision: REVISION, accessoryId: int('Accessory id on the line (order_entry_get products[].accessories[].accessoryId).', { minimum: 1 }), commandId: COMMAND_ID },
    ['orderId', 'productId', 'expectedRevision', 'accessoryId'],
  ),
  annotations: write(true),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const productId = args.productId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(
      api,
      { method: 'DELETE', path: `${OE}/orders/${orderId}/products/${productId}/accessories/${args.accessoryId as number}`, body: { commandId, expectedRevision: args.expectedRevision } },
      `There is no accessory ${String(args.accessoryId)} on product ${productId}.`,
    );
    return commandResult(result, commandId, 'Accessory removed');
  },
};

const orderFinalPriceSet: ToolDef = {
  name: 'order_final_price_set',
  title: 'Set the final order price',
  description:
    'Sets the order\'s final (agreed) total; the discount is derived from it. Use when the user names the total the customer pays. Show the calculated total and the new one to the user first.',
  inputSchema: obj({ orderId: ORDER_ID, expectedRevision: REVISION, finalPrice: num('The final total in EUR.', { minimum: 0 }), commandId: COMMAND_ID }, ['orderId', 'expectedRevision', 'finalPrice']),
  annotations: write(false),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(api, { method: 'PUT', path: `${OE}/orders/${orderId}/final-price`, body: { commandId, expectedRevision: args.expectedRevision, finalPrice: args.finalPrice } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, `Final price set to ${money(args.finalPrice)}`);
  },
};

const orderConfirm: ToolDef = {
  name: 'order_confirm',
  title: 'Confirm products for production',
  description:
    'Confirms the given product lines of an order, which sends them to production (a production document is created) and cannot be undone here. Only when order_entry_get says confirmationEnabled. List the products and totals to the user and get an explicit yes first.',
  inputSchema: obj(
    {
      orderId: ORDER_ID,
      expectedRevision: REVISION,
      productIds: { type: 'array', items: { type: 'integer', minimum: 1 }, minItems: 1, maxItems: 200, description: 'The product ids to confirm (order_entry_get products[].id).' },
      commandId: COMMAND_ID,
    },
    ['orderId', 'expectedRevision', 'productIds'],
  ),
  annotations: write(true),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/orders/${orderId}/confirm`, body: { commandId, expectedRevision: args.expectedRevision, productIds: args.productIds } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, `${(args.productIds as number[]).length} product(s) confirmed for production`);
  },
};

const orderCancel: ToolDef = {
  name: 'order_cancel',
  title: 'Cancel an order',
  description: 'Cancels the whole order. Only when order_entry_get gives no cancellationUnavailableReason. Confirm with the user first.',
  inputSchema: obj({ orderId: ORDER_ID, expectedRevision: REVISION, commandId: COMMAND_ID }, ['orderId', 'expectedRevision']),
  annotations: write(true),
  orderEntry: true,
  handler: async (args, { api, newId }) => {
    const orderId = args.orderId as number;
    const commandId = asString(args.commandId) ?? newId();
    const result = await oe<Row>(api, { method: 'POST', path: `${OE}/orders/${orderId}/cancel`, body: { commandId, expectedRevision: args.expectedRevision } }, `There is no order with id ${orderId}.`);
    return commandResult(result, commandId, 'Order cancelled');
  },
};

export const ORDER_ENTRY_TOOLS: readonly ToolDef[] = [
  orderEntryCatalog,
  orderEntryReferences,
  orderEntryAddresses,
  orderEntryUnitPrice,
  orderEntryProductPreview,
  orderEntryGet,
  orderCreate,
  orderUpdate,
  orderProductAdd,
  orderProductUpdate,
  orderProductRemove,
  orderProductDuplicate,
  orderAccessorySet,
  orderAccessoryRemove,
  orderFinalPriceSet,
  orderAddressSet,
  orderConfirm,
  orderCancel,
];

export type { ToolContext };
