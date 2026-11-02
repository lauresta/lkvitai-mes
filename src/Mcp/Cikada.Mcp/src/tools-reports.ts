// The management reports (the legacy Mango workbook, live): production, work / accruals and payments received. Sales API /api/sales/reports/*.
// Each answer is a flat table in `data.rows` (one object per group, dimensions and measures side by side), ready to sum, compare or chart.

import { ApiError } from './api.js';
import { READ, asString, obj, str, type ToolContext, type ToolDef, type ToolResult } from './tool.js';
import type { Schema } from './validate.js';

const REPORTS = '/api/sales/reports';
const TEXT_ROWS = 40;

type Row = Record<string, unknown>;

const DATE_ARG = (what: string): Schema => ({ type: 'string', description: `${what} (YYYY-MM-DD).`, pattern: String.raw`^\d{4}-\d{2}-\d{2}$` });

const COMMON: Record<string, Schema> = {
  from: DATE_ARG('First day, inclusive; default the first day of the current month'),
  to: DATE_ARG('Last day, INCLUSIVE; default today'),
  granularity: { type: 'string', enum: ['day', 'month', 'year'], description: 'Size of a "period" group, default month. Use day for daily averages or a daily chart.' },
};

function groupBySchema(dims: string[]): Schema {
  return {
    type: 'array',
    items: { type: 'string', enum: ['period', ...dims] },
    maxItems: 4,
    description: `Group the totals by these, in order. "period" splits by day / month / year (see granularity). Leave out for one total row.`,
  };
}

function addDay(date: string): string {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + 1);
  return d.toISOString().slice(0, 10);
}

async function runReport(report: string, filterNames: string[], args: Record<string, unknown>, ctx: ToolContext): Promise<ToolResult> {
  const today = ctx.today();
  const from = asString(args.from) ?? `${today.slice(0, 8)}01`;
  const toInclusive = asString(args.to) ?? today;
  const query: Record<string, string | undefined> = {
    from,
    to: addDay(toInclusive),
    granularity: asString(args.granularity),
    groupBy: Array.isArray(args.groupBy) ? (args.groupBy as string[]).join(',') : undefined,
  };
  for (const name of filterNames) query[name] = asString(args[name]);

  let result: Row;
  try {
    result = await ctx.api.json<Row>({ target: 'sales', method: 'GET', path: `${REPORTS}/${report}`, query });
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      throw new ApiError('Reports need the Cikada role SalesAdmin or Reports, and a legacy SQL login allowed to read them.', 403, error.code);
    }
    throw error;
  }

  const groupBy = (result.groupBy as string[]) ?? [];
  const measures = (result.measures as string[]) ?? [];
  const rows = ((result.rows as Row[]) ?? []).map((r) => Object.assign({}, r.keys as Row | undefined, r.values as Row | undefined) as Row);
  const totals: Row = {};
  for (const m of measures) totals[m] = Math.round(rows.reduce((sum, r) => sum + (typeof r[m] === 'number' ? (r[m] as number) : 0), 0) * 1000) / 1000;

  const fmt = (v: unknown): string => {
    if (typeof v === 'number') return Number.isInteger(v) ? String(v) : v.toFixed(2);
    return typeof v === 'string' ? v : '';
  };
  const header = [...groupBy, ...measures].join(' | ');
  const lines = rows.slice(0, TEXT_ROWS).map((r) => [...groupBy, ...measures].map((c) => fmt(r[c])).join(' | '));
  const more = rows.length > TEXT_ROWS ? `\n… ${rows.length - TEXT_ROWS} more rows in data.rows` : '';
  const truncatedNote = result.truncated ? ' (TRUNCATED at 2000: narrow the period or the grouping)' : '';
  const totalsText = measures.map((m) => m + ' ' + fmt(totals[m])).join(', ');
  const text = `${report} ${from} … ${toInclusive}: ${rows.length} row(s)${truncatedNote}.\n${header}\n${lines.join('\n')}${more}\nTotal over the rows: ${totalsText}`;
  return { text, data: { report, from, to: toInclusive, granularity: result.granularity, groupBy, measures, totals, truncated: result.truncated === true, rows } };
}

const productionReport: ToolDef = {
  name: 'report_production',
  title: 'Production report',
  description:
    'How many products reached a production state in a period (the "Gamyba" report). Each product counts in the period it FIRST reached a state: state "Pagamintas" = made, "Gaminamas" = in production, "Patvirtintas gamybai" = approved for production (the queue), "Įvestas į programą" = entered, "Išsiųstas į filialą" = sent to a branch, "Išduotas klientui" = handed to the customer, "Sustabdytas" = stopped. ' +
    'Products are counted per report type, and every product has a "(vnt)" type (pieces) and usually a "(kvm)" (m²) or "(plotis)" (running metres) type, e.g. "Apsauginės (vnt)" and "Apsauginės (kvm)": never add up different types or units; for "how many" use the (vnt) type. ' +
    'Filters are case-insensitive "contains": type "apsaug" matches "Apsauginės (vnt)" and "(kvm)"; type "tinklel" matches all insect screens. report_dimensions lists the exact types, series and states. ' +
    'For "how many per day on average" use granularity day and divide by working days. data.rows is flat and ready for a chart (period on the x axis).',
  inputSchema: obj({
    ...COMMON,
    groupBy: groupBySchema(['type', 'unit', 'series', 'state']),
    type: str('Report type contains, e.g. "apsaug", "roletai (vnt)", "tinklel", "zebra".', { maxLength: 100 }),
    state: str('State contains, e.g. "Pagamintas", "Gaminamas", "Patvirtintas", "Sustabdytas".', { maxLength: 100 }),
    series: str('Report series (branch / channel) contains, e.g. "Vilnius", "Ryga", "Senukai".', { maxLength: 100 }),
  }),
  annotations: READ,
  handler: (args, ctx) => runReport('production', ['type', 'state', 'series'], args, ctx),
};

const workReport: ToolDef = {
  name: 'report_work',
  title: 'Work and accruals report',
  description:
    'Work done per employee and duty in a period (the "Darbai" report): accrued amount in EUR, orders and products credited, by the date the work was accrued. Duties are e.g. Vadybininkas (manager), Konsultantas, Matuotojas (measurer), Montuotojas (installer), Transportas. ' +
    'Filters are case-insensitive "contains". data.rows is flat and ready for a chart. Accruals are personal pay data: show them only to the person who asked.',
  inputSchema: obj({
    ...COMMON,
    groupBy: groupBySchema(['duty', 'employee', 'department', 'salaryGroup']),
    duty: str('Duty contains, e.g. "Montuotojas".', { maxLength: 100 }),
    employee: str('Employee name contains.', { maxLength: 100 }),
    department: str('Branch contains, e.g. "Vilnius".', { maxLength: 100 }),
    salaryGroup: str('Salary group contains.', { maxLength: 100 }),
  }),
  annotations: READ,
  handler: (args, ctx) => runReport('work', ['duty', 'employee', 'department', 'salaryGroup'], args, ctx),
};

const paymentsReport: ToolDef = {
  name: 'report_payments',
  title: 'Payments received report',
  description:
    'Money received from customers in a period by payment method and branch (the "Mėnuo" report): method kasa (cash desk), ppk (card / cash receipt), bankas (bank transfer); branches Vilnius, Kaunas, Šiauliai, Klaipėda, Klaipėda mažmena, Ryga. Amount in EUR and number of payments, by payment date. ' +
    'Filters are case-insensitive "contains". data.rows is flat and ready for a chart.',
  inputSchema: obj({
    ...COMMON,
    groupBy: groupBySchema(['method', 'branch', 'branchCode']),
    method: str('kasa, ppk or bankas.', { maxLength: 20 }),
    branch: str('Branch name contains.', { maxLength: 100 }),
  }),
  annotations: READ,
  handler: (args, ctx) => runReport('payments', ['method', 'branch'], args, ctx),
};

const reportDimensions: ToolDef = {
  name: 'report_dimensions',
  title: 'Report filter values',
  description: 'The exact values a report can be grouped and filtered by: production types, series and states; work duties, branches and salary groups; payment methods and branches.',
  inputSchema: obj({ report: { type: 'string', enum: ['production', 'work', 'payments'], description: 'Which report.' } }, ['report']),
  annotations: READ,
  handler: async (args, { api }) => {
    const report = args.report as string;
    const result = await api.json<Row>({ target: 'sales', method: 'GET', path: `${REPORTS}/${report}/dimensions` });
    const values = (result.values as Record<string, string[]>) ?? {};
    const text = Object.entries(values).map(([dim, list]) => `${dim}: ${list.join(', ')}`).join('\n');
    return { text: `${report}: group by ${((result.dimensions as string[]) ?? []).join(', ')}; measures ${((result.measures as string[]) ?? []).join(', ')}.\n${text}`, data: result };
  },
};

// ------------------------------------------------------------------------------------------------------------------------------- operational lists

const LIST_TEXT_ROWS = 30;
const bool = (description: string): Schema => ({ type: 'boolean', description });
const int = (description: string, max?: number): Schema => ({ type: 'integer', minimum: 0, ...(max ? { maximum: max } : {}), description });
const text = (description: string): Schema => ({ type: 'string', maxLength: 100, description });

/** args (tool names) → API query (SQL parameter names); dates given inclusive become an exclusive `to`. */
async function runList(list: string, mapping: Record<string, string>, args: Record<string, unknown>, ctx: ToolContext): Promise<ToolResult> {
  const query: Record<string, string | undefined> = {};
  for (const [argName, apiName] of Object.entries(mapping)) {
    const value = args[argName];
    if (value === undefined || value === null || value === '') continue;
    query[apiName] = argName === 'to' ? addDay(String(value)) : String(value);
  }
  let result: Row;
  try {
    result = await ctx.api.json<Row>({ target: 'sales', method: 'GET', path: `${REPORTS}/lists/${list}`, query });
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      throw new ApiError('Reports need the Cikada role SalesAdmin or Reports, and a legacy SQL login allowed to read them.', 403, error.code);
    }
    throw error;
  }
  const columns = (result.columns as string[]) ?? [];
  const rows = (result.rows as Row[]) ?? [];
  const cell = (v: unknown): string => {
    if (v === null || v === undefined) return '';
    if (typeof v === 'number') return Number.isInteger(v) ? String(v) : v.toFixed(2);
    return typeof v === 'string' ? v : JSON.stringify(v);
  };
  const lines = rows.slice(0, LIST_TEXT_ROWS).map((r) => columns.map((c) => cell(r[c])).join(' | '));
  const more = rows.length > LIST_TEXT_ROWS ? `\n… ${rows.length - LIST_TEXT_ROWS} more rows in data.rows` : '';
  const truncatedNote = result.truncated ? ' (TRUNCATED at 1000: narrow the filters)' : '';
  return {
    text: `${list}: ${rows.length} row(s)${truncatedNote}.\n${columns.join(' | ')}\n${lines.join('\n')}${more}`,
    data: { list, columns, rows, truncated: result.truncated === true },
  };
}

const pipelineList: ToolDef = {
  name: 'production_queue',
  title: 'Production queue now',
  description:
    'The live production queue: manufactured products not yet handed to the customer, by their CURRENT state and product type (vnt types), with pieces, average and maximum days in that state and pieces stuck longer than staleDays. ' +
    'States: Įvestas į programą (entered, waiting for approval), Patvirtintas gamybai (approved = queued for production), Gaminamas (in production), Pagamintas (made), Išsiųstas į filialą (sent to branch), Sustabdytas (stopped). ' +
    'Old forgotten items inflate the queue: use maxOrderAgeDays (e.g. 180) for a realistic picture. For "how many per day do we make" use report_production instead.',
  inputSchema: obj({
    branch: text('Only orders of this branch (name contains).'),
    maxOrderAgeDays: int('Ignore products of orders older than this many days.'),
    staleDays: int('Count pieces in their state longer than this (default 30).'),
  }),
  annotations: READ,
  handler: (args, ctx) => runList('pipeline', { branch: 'branch', maxOrderAgeDays: 'maxOrderAgeDays', staleDays: 'staleDays' }, args, ctx),
};

const materialsList: ToolDef = {
  name: 'materials_popularity',
  title: 'Most used materials',
  description:
    'Materials / colours ranked by use in orders of a period (by order date): material code, name and category, product category and type, orders, pieces and m². ' +
    'Filter by product (type contains, e.g. "roletai", "zebra", "plis") and material (code or name contains). The default placeholder colour is left out.',
  inputSchema: obj({
    from: DATE_ARG('First order date, inclusive; default the first day of the current month'),
    to: DATE_ARG('Last order date, INCLUSIVE; default today'),
    type: text('Product type or category contains.'),
    material: text('Material code or name contains.'),
    top: int('How many rows, default 50.', 500),
  }),
  annotations: READ,
  handler: (args, ctx) => runList('materials', { from: 'from', to: 'to', type: 'typeSearch', material: 'materialSearch', top: 'top' }, args, ctx),
};

const stoppedList: ToolDef = {
  name: 'stopped_products',
  title: 'Stopped products',
  description:
    'Products currently stopped (state Sustabdytas): order, customer, branch, product, material, size, since when and how many days, the state before the stop, who stopped it, and the order / product notes. ' +
    'The legacy system stores NO stop reason (the "missing material" states were never used): the notes are the only hint, say so when asked why.',
  inputSchema: obj({ search: text('Order number or customer contains.'), includeCancelled: bool('Also cancelled orders.') }),
  annotations: READ,
  handler: (args, ctx) => runList('stopped', { search: 'search', includeCancelled: 'includeCancelled' }, args, ctx),
};

const unpaidList: ToolDef = {
  name: 'unpaid_orders',
  title: 'Long unpaid orders',
  description:
    'Orders with outstanding debt (price − paid), oldest first: state, customer, wholesale or retail, branch, price, paid, debt, days since order and since handed out, last payment, and LegacyOverdue (handed out more than 30 days ago for wholesale / 15 for retail, the rule of the legacy reminder e-mail). ' +
    'Debt includes prepayments of orders not yet handed out: use onlyHandedOut for money actually owed after delivery. Retail sales are booked on one customer per branch ("Kaunas - mažmena").',
  inputSchema: obj({
    minDays: int('Only orders at least this many days old (default 30).'),
    minDebt: { type: 'number', minimum: 0, description: 'Only debts of at least this many EUR.' },
    onlyHandedOut: bool('Only orders already handed out to the customer.'),
    onlyLegacyOverdue: bool('Only orders overdue by the legacy rule.'),
    search: text('Customer or order number contains.'),
  }),
  annotations: READ,
  handler: (args, ctx) =>
    runList('unpaid-orders', { minDays: 'minDays', minDebt: 'minDebt', onlyHandedOut: 'onlyHandedOut', onlyLegacyOverdue: 'onlyLegacyOverdue', search: 'search' }, args, ctx),
};

const notDeliveredList: ToolDef = {
  name: 'made_not_delivered',
  title: 'Made but not handed out or installed',
  description:
    'Orders made long ago but not handed out, and handed-out orders whose installation is not recorded yet: lines and pieces, made / handed lines, made date and days since, hand-out date, NotHandedOut, NeedsInstall (has a Montavimas service line), InstallerDate, InstallPending, debt. ' +
    'Installation has no "done" flag in the legacy system: it counts as done when an installer (Montuotojas) is recorded with a date up to today.',
  inputSchema: obj({
    days: int('At least this many days since made (default 20, as the legacy e-mail).'),
    allMade: bool('true (default): every product of the order is made; false: at least one.'),
    includeInstall: bool('Also handed-out orders with installation pending (default true).'),
    maxOrderAgeDays: int('Ignore orders older than this many days (default 365; old data has gaps).'),
  }),
  annotations: READ,
  handler: (args, ctx) => runList('not-delivered', { days: 'days', allMade: 'allMade', includeInstall: 'includeInstall', maxOrderAgeDays: 'maxOrderAgeDays' }, args, ctx),
};

const customerDebtList: ToolDef = {
  name: 'customer_debt',
  title: 'Customer debt',
  description:
    'Debt per customer, largest first: orders in debt, total debt, debt of handed-out orders, oldest debt date and age, the debt in Agnum (accounting) and the credit limit. Search one customer by name. ' +
    'Retail customers are not stored by name (one customer per branch), so this is meaningful for wholesale customers.',
  inputSchema: obj({ search: text('Customer name contains.'), onlyHandedOut: bool('Only handed-out orders.'), top: int('How many customers, default 20.', 500) }),
  annotations: READ,
  handler: (args, ctx) => runList('customer-debt', { search: 'search', onlyHandedOut: 'onlyHandedOut', top: 'top' }, args, ctx),
};

const orderSearchList: ToolDef = {
  name: 'orders_find',
  title: 'Find orders by period, product and material',
  description:
    'Orders with an order date in a period, optionally only those with a product of a type and / or material, in a state, of a branch; oldest first, with price, paid, debt and the matching lines / pieces. Use orders_search for free text and order_get for one order.',
  inputSchema: obj({
    from: DATE_ARG('First order date, inclusive; default the first day of the current month'),
    to: DATE_ARG('Last order date, INCLUSIVE; default today'),
    type: text('Product type or category contains, e.g. "zebra", "roletai", "tinklel".'),
    material: text('Material code or name contains.'),
    state: text('Order state contains, e.g. "Gaminamas", "Pagamintas", "Išduotas".'),
    branch: text('Branch name contains.'),
    includeCancelled: bool('Also cancelled orders.'),
    top: int('At most this many orders, default 200.', 1000),
  }),
  annotations: READ,
  handler: (args, ctx) =>
    runList('order-search', { from: 'from', to: 'to', type: 'typeSearch', material: 'materialSearch', state: 'state', branch: 'branch', includeCancelled: 'includeCancelled', top: 'top' }, args, ctx),
};

export const REPORT_TOOLS: readonly ToolDef[] = [productionReport, workReport, paymentsReport, reportDimensions, pipelineList, materialsList, stoppedList, unpaidList, notDeliveredList, customerDebtList, orderSearchList];
