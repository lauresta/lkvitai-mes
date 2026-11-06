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
    name: 'bank_payments_preview',
    title: 'Preview a bank statement import',
    description: 'Read the attached bank statement (Excel/XLSX, CSV, PDF, screenshot, MT940, CAMT.053 or another bank format) using your file or vision capabilities. Extract each transaction into date, payer, purpose and exact signed amount. Never infer unreadable values: use an empty field so the API marks an error. Send at most 100 rows per batch, preserving source order. This read-only tool checks the live legacy bank table for duplicates and returns its latest NR, projected new NR range, errors and previewId. Show the complete preview to the user and get explicit approval before bank_payments_import.',
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
      const errorCount = rows.filter((row) => row.status === 'Error').length;
      const nextNr = latestNr + 1;
      return {
        text: `Latest bank NR: ${latestNr}. ${newLines.length} new (projected NR ${nextNr}–${latestNr + newLines.length}), ${duplicateCount} duplicates, ${errorCount} errors. Review every row and ask for explicit confirmation before importing.`,
        data: { country: args.country, previewId: result.previewId, latestNr, nextNr,
          projectedNrTo: latestNr + newLines.length, selectedNewLines: newLines, rows },
      };
    },
  },
  {
    name: 'bank_payments_import',
    title: 'Import reviewed bank payments',
    description: 'Writes selected rows from a recent bank_payments_preview into the shared production legacy bank table. The chosen rows are read from the server-side preview; the latest NR must still match. Call only after showing the full preview and receiving an explicit yes from the user. New rows are selected by default; include a duplicate line only if the user explicitly approved that duplicate.',
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
