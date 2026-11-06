// Bank statements are read by ChatGPT (XLSX, CSV, PDF, image, MT940, CAMT.053, etc.) and
// passed as exact normalized rows. The Finance API performs the authoritative validation,
// duplicate check and NR allocation against the shared legacy bank table.

import { ArgumentError } from './validate.js';
import { READ, bool, int, obj, str, write, type ToolDef } from './tool.js';

const BANK = '/api/finance/bank-payments';
type Row = Record<string, unknown>;

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
];
