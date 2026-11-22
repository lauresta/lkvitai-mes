// The MCP server of one request: tools/list from the table (order entry tools only when they are on), tools/call through the handler, and the prompts.

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { CallToolRequestSchema, GetPromptRequestSchema, ListPromptsRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import { ApiError, VERSION } from './api.js';
import { promptsFor } from './prompts.js';
import type { ToolContext, ToolDef } from './tool.js';
import { ORDER_ENTRY_TOOLS } from './tools-order-entry.js';
import { FINANCE_TOOLS } from './tools-finance.js';
import { DOCS_TOOLS } from './tools-docs.js';
import { READ_TOOLS } from './tools-read.js';
import { WAREHOUSE_TOOLS } from './tools-warehouse.js';
import { REPORT_TOOLS } from './tools-reports.js';
import { ArgumentError, validateArgs } from './validate.js';

export const ALL_TOOLS: readonly ToolDef[] = [...READ_TOOLS, ...WAREHOUSE_TOOLS, ...REPORT_TOOLS, ...ORDER_ENTRY_TOOLS, ...FINANCE_TOOLS, ...DOCS_TOOLS];

export const toolsFor = (orderEntry: boolean, warehouseEntry = false): readonly ToolDef[] =>
  ALL_TOOLS.filter((tool) => (orderEntry || !tool.orderEntry) && (warehouseEntry || !tool.warehouseEntry));

export interface CallOutcome {
  [key: string]: unknown;
  content: ({ type: 'text'; text: string } | { type: 'image'; data: string; mimeType: string })[];
  structuredContent?: Record<string, unknown>;
  isError?: boolean;
}

const failure = (code: string, message: string, extra: Record<string, unknown> = {}): CallOutcome => ({
  content: [{ type: 'text', text: message }],
  structuredContent: { error: { code, message, ...extra } },
  isError: true,
});

export type Log = (event: Record<string, unknown>) => void;

export async function callTool(name: string, rawArgs: unknown, ctx: ToolContext, orderEntry: boolean, log?: Log, warehouseEntry = false): Promise<CallOutcome> {
  const tool = toolsFor(orderEntry, warehouseEntry).find((candidate) => candidate.name === name);
  if (!tool) return failure('UNKNOWN_TOOL', `There is no tool ${name}.`);
  try {
    const args = validateArgs(rawArgs, tool.inputSchema);
    const result = await tool.handler(args, ctx);
    return {
      content: [{ type: 'text', text: result.text }, ...(result.images ?? []).map((image) => ({ type: 'image' as const, data: image.data, mimeType: image.mimeType }))],
      structuredContent: result.data,
    };
  } catch (error) {
    if (error instanceof ArgumentError) return failure('INVALID_ARGUMENTS', `Invalid arguments for ${name}: ${error.message}.`);
    if (error instanceof ApiError) return failure(error.code ?? `HTTP_${error.status}`, error.message, { status: error.status });
    log?.({ msg: 'tool_failed', tool: name, error: error instanceof Error ? `${error.name}: ${error.message}` : 'unknown' });
    const reason = error instanceof Error && error.name === 'TimeoutError' ? 'the API did not answer in time' : 'the API could not be reached';
    return failure('UPSTREAM_UNREACHABLE', `${name} failed: ${reason}. Try again in a moment.`);
  }
}

export function instructions(orderEntry: boolean, warehouseEntry = false): string {
  return [
    'Cikada.MES for Lauresta: sales orders, customers, items with prices and stock, and the management reports.',
    'Orders: orders_filters gives the exact status / store labels; orders_search finds orders; order_get shows one order by number.',
    'Products and prices: items_search (base, purchase and price-group price, available stock), stock_get (per location), price_groups_list; customers_search / customer_get show a customer and its price group.',
    'Warehouse: warehouses_list and locations_search give warehouse and location ids; location_stock shows what lies in a location and stock_get where an item lies; item_cost gives the unit cost and value of the stock on hand (items_search gives the sale and purchase price). suppliers_search, inbound_shipments_list / inbound_shipment_get read purchase documents (a supplier invoice with its lines); transfers_list / transfer_get read stock moves; production_issues_list reads what was issued to production. supplier_stock_report answers what to order from a supplier: per item on hand, issued in the last months, ordered and expected, minimum, shortage and prices.',
    warehouseEntry
      ? 'Warehouse entry: location_create adds a storage location; inbound_shipment_create enters a supplier invoice (purchase document) with its lines, inbound_shipment_receive books received quantities into RECEIVING (or QC_HOLD), putaway_move puts received stock from RECEIVING into a storage location, inbound_shipment_post posts the document to accounting; transfer_create plus transfer_advance (submit, approve, execute) moves stock between locations; production_issue_create issues stock to production. Never guess an item, supplier or location id: look it up first. Summarise what will be written and get an explicit yes before every one of these calls; after a refused write read the document again.'
      : 'Warehouse entry (creating locations, purchase documents and stock moves) is not enabled on this server.',
    'Finance: bank_payments_list reads individual live bank payments with NR, date, payer, purpose and amount; use it for transaction lists and counterparties, and fetch all pages before claiming a complete list. report_payments is aggregated and cannot answer those questions. When given a bank statement in Excel, CSV, PDF, image, MT940, CAMT.053 or another format, extract exact transaction rows and call bank_payments_preview. It checks duplicates and possible duplicates caused by OCR payer-name errors, reporting matched NR and payer, the latest NR and projected numbers. Never call a possible duplicate new. Show every preview row, then call bank_payments_import only after an explicit user yes for the selected lines. The legacy bank table is shared with production and Access.',
    orderEntry
      ? 'Order entry: order_entry_catalog / order_entry_references give every id; order_entry_get gives the revision every write needs. Never guess an id, a number or a price; summarise and get an explicit yes before order_create, order_confirm, order_cancel or order_product_remove. After a refused write read the order again.'
      : 'Order entry (creating or changing orders) is not enabled on this server.',
    'Management reports (live, with history since 2020): report_production (made / in production / queue / stopped by product type, series, state, day or month), report_work (accruals per employee and duty), report_payments (money received by method and branch); report_dimensions gives exact filter values. Live lists: production_queue (queue now), materials_popularity, stopped_products, unpaid_orders, made_not_delivered (not handed out / not installed), customer_debt, orders_find (orders by period, product, material). Answer from them directly, and draw a chart from data.rows when it helps.',
    'User manuals (Lithuanian, how to use each screen and process in every module): docs_search with Lithuanian keywords (translate the question first, try 2-3 queries), then docs_get for the full chunk before answering; when a chunk lists images, docs_image returns that screenshot so you can describe the screen precisely, and with marks it draws coloured frames around the buttons or fields you are explaining; say when a screen is marked "laikina" (a stub). No backend call, any signed-in user.',
    'Every call runs as the signed-in user; a 403 means the user lacks the Cikada role for it.',
  ].join('\n');
}

export function createServer(ctx: ToolContext, orderEntry: boolean, log?: Log, warehouseEntry = false): Server {
  const server = new Server({ name: 'cikada', title: 'Cikada.MES', version: VERSION }, { capabilities: { tools: {}, prompts: {} }, instructions: instructions(orderEntry, warehouseEntry) });
  server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: toolsFor(orderEntry, warehouseEntry).map((tool) => ({
      name: tool.name,
      title: tool.title,
      description: tool.description,
      inputSchema: tool.inputSchema as { type: 'object' },
      annotations: { title: tool.title, ...tool.annotations },
    })),
  }));
  server.setRequestHandler(CallToolRequestSchema, async (request) => callTool(request.params.name, request.params.arguments ?? {}, ctx, orderEntry, log, warehouseEntry));
  server.setRequestHandler(ListPromptsRequestSchema, async () => ({
    prompts: promptsFor(orderEntry).map(({ name, title, description, arguments: args }) => ({ name, title, description, arguments: args.map((a) => ({ ...a })) })),
  }));
  server.setRequestHandler(GetPromptRequestSchema, async (request) => {
    const prompt = promptsFor(orderEntry).find((p) => p.name === request.params.name);
    if (!prompt) throw new Error(`there is no prompt ${request.params.name}`);
    return {
      description: prompt.description,
      messages: [{ role: 'user' as const, content: { type: 'text' as const, text: prompt.text(request.params.arguments ?? {}) } }],
    };
  });
  return server;
}
