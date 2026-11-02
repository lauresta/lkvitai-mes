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

export const REPORT_TOOLS: readonly ToolDef[] = [productionReport, workReport, paymentsReport, reportDimensions];
