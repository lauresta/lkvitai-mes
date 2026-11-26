// Bank statements are read by ChatGPT (XLSX, CSV, PDF, image, MT940, CAMT.053, etc.) and
// passed as exact normalized rows. The Finance API performs the authoritative validation,
// duplicate check and NR allocation against the shared legacy bank table.

import { ArgumentError } from './validate.js';
import { DATE, READ, asString, bool, int, obj, str, write, type ToolDef } from './tool.js';

const BANK = '/api/finance/bank-payments';
const INCOMES = '/api/finance/incomes';
type Row = Record<string, unknown>;

// «Įplaukų judėjimas» (Finance → /finance/incomes): money received by payment date or orders taken by order date,
// read live from the legacy DB under the caller's SQL login. The Finance API lets only the users on
// Finance__Incomes__AllowedUsers in; everybody else gets 403.
const INCOME_METHODS = ['BANK', 'EKA', 'PPK', 'KASA'];
const incomesFilters = {
  dataset: str('money = money received, by payment date (default); orders = orders taken, by order date, value after discount.',
    { enum: ['money', 'orders'] }),
  from: DATE('First day, inclusive; default the first day of last month'),
  to: DATE('Last day, inclusive; default the last day of last month'),
  branches: { type: 'array', description: 'Branch codes from incomes_summary data.branches[].code (e.g. VLN); default all.',
    items: { type: 'string', maxLength: 10 }, maxItems: 20 },
  methods: { type: 'array', description: 'Money received only: BANK (bank transfer), EKA (cash register, cash or card), PPK (cash with a receipt), KASA (cash desk); default all.',
    items: { type: 'string', enum: INCOME_METHODS }, maxItems: 4 },
};

const joined = (value: unknown): string | undefined =>
  Array.isArray(value) && value.length > 0 ? (value as string[]).join(',') : undefined;

function incomesQuery(args: Record<string, unknown>): Record<string, string | undefined> {
  const orders = args.dataset === 'orders';
  return { ds: orders ? 'orders' : undefined, from: asString(args.from), to: asString(args.to),
    branch: joined(args.branches), method: orders ? undefined : joined(args.methods) };
}

const eur = (value: unknown): string => `${Number(value ?? 0).toFixed(2)} EUR`;

function change(now: unknown, base: unknown): string {
  const current = Number(now ?? 0);
  const previous = Number(base ?? 0);
  if (previous === 0) return 'n/a';
  const pct = (current - previous) / Math.abs(previous) * 100;
  return `${pct >= 0 ? '+' : ''}${pct.toFixed(1)} %`;
}

/** A negative legacy discount is a manual price correction upwards, not a discount (owner, 2026-11-26). */
const discount = (value: unknown): string => {
  const pct = Number(value ?? 0);
  if (pct === 0) return '-';
  return pct < 0 ? `+${(-pct).toFixed(1)} % price correction` : `${pct.toFixed(1)} % discount`;
};

function summaryText(result: Row): string {
  const money = result.dataset !== 'orders';
  const period = (result.period ?? {}) as Row;
  const totals = (result.totals ?? {}) as Row;
  const comparisons = (result.comparisons ?? {}) as Row;
  const branches = Array.isArray(result.branches) ? result.branches as Row[] : [];
  const columns = Array.isArray(result.columns) ? result.columns as Row[] : [];
  const matrix = Array.isArray(result.matrix) ? result.matrix as Row[] : [];
  const flows = Array.isArray(result.soldVsReceived) ? result.soldVsReceived as Row[] : [];
  const remarks = Array.isArray(result.remarks) ? result.remarks as Row[] : [];
  const names = new Map(branches.map((b) => [String(b.code), String(b.name)]));
  const lines = [
    `${money ? 'Money received (by payment date)' : 'Orders taken (by order date, value after discount)'} ${period.from}..${period.to}: ` +
      `${totals.count} ${money ? 'payments' : 'orders'}, ${eur(totals.amountEur)}.`,
  ];
  if (money) {
    const refunds = (totals.refunds ?? {}) as Row;
    lines.push(`Refunds (negative payments): ${refunds.count}, ${eur(refunds.amountEur)}. Payments without an employee: ${totals.noEmployeeCount}.`);
  } else {
    const cancelled = (totals.cancelled ?? {}) as Row;
    lines.push(`Cancelled at the client's request: ${cancelled.count}; they still pay ${eur(cancelled.amountEur)}, included in the total.`);
  }
  for (const [label, key] of [['Previous period', 'previous'], ['Same period last year', 'lastYear']] as const) {
    const c = (comparisons[key] ?? {}) as Row;
    lines.push(`${label} ${c.from}..${c.to}: ${c.count}, ${eur(c.amountEur)} (amount ${change(totals.amountEur, c.amountEur)}, count ${change(totals.count, c.count)}).`);
  }
  const withData = branches.filter((b) => b.hasData).sort((a, b) => Number(b.totalEur) - Number(a.totalEur));
  lines.push(`By branch: ${withData.map((b) => `${b.name} [${b.code}] ${eur(b.totalEur)} (${b.count})`).join('; ') || 'no data'}.`);
  if (money) {
    const byMethod = columns.map((col) => {
      const cells = matrix.filter((cell) => cell.column === col.key);
      return `${col.key} ${eur(cells.reduce((s, c) => s + Number(c.amountEur ?? 0), 0))} (${cells.reduce((s, c) => s + Number(c.count ?? 0), 0)})`;
    });
    lines.push(`By method: ${byMethod.join('; ')}. Branch x method cells are in data.matrix.`);
  }
  if (flows.length > 0)
    lines.push(`Sold vs received per branch (flows, not debt): ${flows.map((f) =>
      `${names.get(String(f.branch)) ?? f.branch} sold ${eur(f.soldEur)}, received ${eur(f.receivedEur)}`).join('; ')}.`);
  if (remarks.length > 0)
    lines.push(`Remarks: ${remarks.map((r) => `${r.kind}(${Object.entries((r.values ?? {}) as Row).map(([k, v]) => `${k}=${v}`).join(', ')})`).join('; ')}.`);
  return lines.join('\n');
}

function rowLine(row: Row, money: boolean): string {
  if (money) {
    const doc = row.bankTransferNo ? `bank NR ${row.bankTransferNo}` : row.ppkNo ? `PPK ${row.ppkNo}` : row.ekaReceiptNo ? `EKA-${row.ekaReceiptNo}` : '-';
    return `${row.date} | ${row.orderNo} | ${row.branch} | ${row.method} | ${eur(row.amountEur)} | ${row.employee ?? 'no employee'} | ${doc} | ${row.note ?? ''}`;
  }
  return `${row.date} | ${row.orderNo} | ${row.branch} | ${row.customer ?? '-'} | ${eur(row.valueEur)} | ${discount(row.discountPct)} | paid ${eur(row.paidEur)} | ${row.status}`;
}

const country = str('Bank country. LT = Lietuva, LV = Latvija.', { enum: ['LT', 'LV'] });
const payment = obj({
  date: str('Transaction date as YYYY-MM-DD; copy the source date exactly. Use an empty string when unreadable.', { maxLength: 20 }),
  firm: str('Payer name exactly as printed; empty when unreadable.', { maxLength: 255 }),
  purpose: str('Full payment purpose/reference; empty when absent.', { maxLength: 1024 }),
  sum: str('Signed amount exactly as printed, including decimal separator; empty when unreadable.', { maxLength: 40 }),
}, ['date', 'firm', 'purpose', 'sum']);

export const FINANCE_TOOLS: readonly ToolDef[] = [
  {
    name: 'bank_payments_list',
    title: 'List individual bank payments',
    description: 'Read individual payments from the live legacy bank table, including NR, date, payer, full purpose and amount. Use this when asked for bank transactions or counterparties: report_payments only provides aggregated totals. Results are paged at 100 rows; fetch every page before claiming to have the complete list. Finance access and the caller’s legacy SQL mapping are required.',
    inputSchema: obj({
      country,
      from: str('First transaction date, inclusive (YYYY-MM-DD).', { pattern: '^\\d{4}-\\d{2}-\\d{2}$' }),
      to: str('Last transaction date, inclusive (YYYY-MM-DD).', { pattern: '^\\d{4}-\\d{2}-\\d{2}$' }),
      search: str('Filter payer or payment purpose by text.', { maxLength: 100 }),
      sort: str('Sort column; default NR.', { enum: ['nr', 'date', 'firm', 'purpose', 'sum'] }),
      descending: bool('Descending order; default true.'),
      page: int('Page number, starting at 1; 100 rows per page.', { minimum: 1, maximum: 10000 }),
    }, ['country']),
    annotations: READ,
    handler: async (args, { api }) => {
      const result = await api.json<Row>({ target: 'finance', method: 'GET', path: BANK,
        query: { country: args.country as string, from: args.from as string | undefined,
          to: args.to as string | undefined, q: args.search as string | undefined,
          sort: args.sort as string | undefined, desc: args.descending as boolean | undefined,
          page: args.page as number | undefined } });
      const items = Array.isArray(result.items) ? result.items as Row[] : [];
      const total = Number(result.total ?? 0);
      const page = Number(result.page ?? 1);
      const pageSize = Number(result.pageSize ?? 100);
      const pages = Math.max(1, Math.ceil(total / pageSize));
      const lines = items.slice(0, 20).map((row) =>
        `${row.nr} | ${row.date} | ${row.firm} | ${row.purpose} | ${row.sum}`);
      const more = items.length > 20 ? `\n${items.length - 20} more rows on this page in data.items.` : '';
      const next = page < pages ? ` Fetch page ${page + 1} for more payments.` : '';
      return {
        text: `Bank payments ${args.country}: page ${page} of ${pages}, ${total} matching payments.\nNR | date | payer | purpose | amount\n${lines.join('\n')}${more}${next}`,
        data: { country: args.country, from: args.from, to: args.to, search: args.search,
          sort: args.sort ?? 'nr', descending: args.descending ?? true,
          total, countryTotal: result.countryTotal, lastDate: result.lastDate,
          page, pageSize, pages, items },
      };
    },
  },
  {
    name: 'bank_payments_preview',
    title: 'Preview a bank statement import',
    description: 'Read the attached bank statement (Excel/XLSX, CSV, PDF, screenshot, MT940, CAMT.053 or another bank format) using your file or vision capabilities. Extract each transaction into date, payer, purpose and exact signed amount. Never infer unreadable values: use an empty field so the API marks an error. Send at most 100 rows per batch, preserving source order. This read-only tool checks the live legacy bank table. OCR can shorten or misspell payer names: a matching date and amount with a different payer is PossibleDuplicate, never New. Show its matched NR and payer to the user; do not offer it for import by default. Show the complete preview and get explicit approval before bank_payments_import.',
    inputSchema: obj({
      country,
      rows: { type: 'array', description: 'Transactions extracted from one statement, in source order. Split larger statements into batches.', items: payment, minItems: 1, maxItems: 100 },
    }, ['country', 'rows']),
    annotations: READ,
    handler: async (args, { api }) => {
      const result = await api.json<Row>({ target: 'finance', method: 'POST', path: `${BANK}/preview`,
        body: { country: args.country, rows: args.rows } });
      const rows = Array.isArray(result.rows) ? result.rows as Row[] : [];
      const latestNr = Number(result.latestNr);
      const newLines = rows.filter((row) => row.status === 'New').map((row) => row.line);
      const duplicateCount = rows.filter((row) => row.status === 'Duplicate').length;
      const possibleDuplicateCount = rows.filter((row) => row.status === 'PossibleDuplicate').length;
      const errorCount = rows.filter((row) => row.status === 'Error').length;
      const nextNr = latestNr + 1;
      return {
        text: `Latest bank NR: ${latestNr}. ${newLines.length} new (projected NR ${nextNr}–${latestNr + newLines.length}), ${duplicateCount} duplicates, ${possibleDuplicateCount} possible duplicates, ${errorCount} errors. Possible duplicates have a matched NR/payer and are excluded by default. Review every row and ask for explicit confirmation before importing.`,
        data: { country: args.country, previewId: result.previewId, latestNr, nextNr,
          projectedNrTo: latestNr + newLines.length, selectedNewLines: newLines,
          possibleDuplicateCount, rows },
      };
    },
  },
  {
    name: 'bank_payments_import',
    title: 'Import reviewed bank payments',
    description: 'Writes selected rows from a recent bank_payments_preview into the shared production legacy bank table. The chosen rows are read from the server-side preview; the latest NR must still match. Call only after showing the full preview and receiving an explicit yes from the user. New rows are selected by default; include a Duplicate or PossibleDuplicate line only if the user explicitly approved that specific line after seeing its matched NR and payer.',
    inputSchema: obj({
      country,
      previewId: str('previewId returned by bank_payments_preview.', { pattern: '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' }),
      expectedNextNr: int('nextNr returned by bank_payments_preview. Import fails if the live NR changed.', { minimum: 1 }),
      selectedLines: { type: 'array', description: 'Line numbers from the reviewed preview; errors cannot be imported.', items: { type: 'integer', minimum: 1 }, minItems: 1, maxItems: 100 },
      confirmed: bool('Must be true only after the user explicitly approved this exact preview and selected lines.'),
    }, ['country', 'previewId', 'expectedNextNr', 'selectedLines', 'confirmed']),
    annotations: write(true),
    handler: async (args, { api }) => {
      if (args.confirmed !== true) throw new ArgumentError('explicit confirmation is required before importing bank payments');
      const result = await api.json<Row>({ target: 'finance', method: 'POST', path: BANK,
        body: { country: args.country, previewId: args.previewId,
          expectedNextNr: args.expectedNextNr, selectedLines: args.selectedLines } });
      return { text: `Imported ${result.count} bank payments into ${args.country}, NR ${result.nrFrom}–${result.nrTo}.`,
        data: { country: args.country, ...result } };
    },
  },
  {
    name: 'incomes_summary',
    title: 'Incomes report: totals, comparisons, branches and methods',
    description: 'The «Įplaukų judėjimas» report (Finance → /finance/incomes), live from the legacy DB. dataset money = money received by payment date (bank transfer, cash register EKA, cash receipt PPK, cash desk); orders = orders taken by order date, value after discount (orders cancelled at the client\'s request count with the amount the client still pays). Both period ends are inclusive; default last month. Returns totals, refunds or cancelled orders, comparison with the previous period and the same period last year, every branch (also those without data), branch x method matrix (orders: branch x week/month), sold vs received per branch (a flow comparison, not debt), daily/monthly buckets and automatic remarks. Use incomes_rows for the individual payments or orders. Only the users on the incomes allow-list may read it: a 403 means this user is not on it.',
    inputSchema: obj(incomesFilters),
    annotations: READ,
    handler: async (args, { api }) => {
      const result = await api.json<Row>({ target: 'finance', method: 'GET', path: `${INCOMES}/summary`, query: incomesQuery(args) });
      return { text: summaryText(result), data: result };
    },
  },
  {
    name: 'incomes_rows',
    title: 'Incomes report: individual payments or orders',
    description: 'Rows of the «Įplaukų judėjimas» report with the same filters as incomes_summary. Money rows: date, order no, branch, method, amount (negative = refund), employee, note, bank transfer NR / PPK receipt / EKA receipt. Order rows: date, order no, branch, consultant, customer, value after discount, discountPct (a NEGATIVE discountPct is a manual price correction upwards, not a discount), paid so far, status accepted/cancelled. Narrow with day, refunds, noEmployee, cancelled and search; sort and group like the page. 100 rows per page: fetch every page before claiming a complete list. Same access rule as incomes_summary.',
    inputSchema: obj({
      ...incomesFilters,
      day: DATE('Only this day'),
      refunds: bool('Money received only: only refunds (negative payments).'),
      noEmployee: bool('Money received only: only payments without an employee.'),
      cancelled: bool('Orders only: only orders cancelled at the client\'s request.'),
      search: str('Order no, note, employee, customer or consultant contains this text.', { maxLength: 100 }),
      sort: str('Sort column; default date (newest first). money: date, order, branch, method, emp, amt; orders: date, order, branch, emp, cust, disc, paid, status, amt.',
        { enum: ['date', 'order', 'branch', 'method', 'emp', 'amt', 'cust', 'disc', 'paid', 'status'] }),
      descending: bool('Descending order; default true.'),
      group: str('Group rows (group subtotals in data.groups): none, day, week, month, branch, method (money), employee (money), consultant (orders).',
        { enum: ['none', 'day', 'week', 'month', 'branch', 'method', 'employee', 'consultant'] }),
      page: int('Page number, starting at 1; 100 rows per page.', { minimum: 1, maximum: 600 }),
    }),
    annotations: READ,
    handler: async (args, { api }) => {
      const money = args.dataset !== 'orders';
      const page = typeof args.page === 'number' ? args.page : 1;
      const sort = asString(args.sort);
      const result = await api.json<Row>({ target: 'finance', method: 'GET', path: `${INCOMES}/rows`, query: {
        ...incomesQuery(args), day: asString(args.day), q: asString(args.search),
        refunds: money && args.refunds === true ? '1' : undefined,
        noEmployee: money && args.noEmployee === true ? '1' : undefined,
        cancelled: !money && args.cancelled === true ? '1' : undefined,
        sort: sort ? `${args.descending === false ? '' : '-'}${sort}` : undefined,
        group: asString(args.group), skip: (page - 1) * 100, take: 100 } });
      const items = Array.isArray(result.items) ? result.items as Row[] : [];
      const total = Number(result.total ?? 0);
      const pages = Math.max(1, Math.ceil(total / 100));
      const lines = items.slice(0, 20).map((row) => rowLine(row, money));
      const more = items.length > 20 ? `\n${items.length - 20} more rows on this page in data.items.` : '';
      const next = page < pages ? ` Fetch page ${page + 1} for more rows.` : '';
      const header = money ? 'date | order | branch | method | amount | employee | document | note'
        : 'date | order | branch | customer | value | discount | paid | status';
      return {
        text: `${total} ${money ? 'payments' : 'orders'}, total ${eur(result.sumEur)}${money ? `, refunds ${eur(result.refundsEur)}` : ''}; page ${page} of ${pages}.\n${header}\n${lines.join('\n')}${more}${next}`,
        data: { ...result, page, pageSize: 100, pages },
      };
    },
  },
];
