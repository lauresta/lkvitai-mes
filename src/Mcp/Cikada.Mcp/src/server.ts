// The MCP server of one request: tools/list from the table (order entry tools only when they are on), tools/call through the handler, and the prompts.

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { CallToolRequestSchema, GetPromptRequestSchema, ListPromptsRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import { ApiError, VERSION } from './api.js';
import { promptsFor } from './prompts.js';
import type { ToolContext, ToolDef } from './tool.js';
import { ORDER_ENTRY_TOOLS } from './tools-order-entry.js';
import { READ_TOOLS } from './tools-read.js';
import { REPORT_TOOLS } from './tools-reports.js';
import { ArgumentError, validateArgs } from './validate.js';

export const ALL_TOOLS: readonly ToolDef[] = [...READ_TOOLS, ...REPORT_TOOLS, ...ORDER_ENTRY_TOOLS];

export const toolsFor = (orderEntry: boolean): readonly ToolDef[] => ALL_TOOLS.filter((tool) => orderEntry || !tool.orderEntry);

export interface CallOutcome {
  [key: string]: unknown;
  content: { type: 'text'; text: string }[];
  structuredContent?: Record<string, unknown>;
  isError?: boolean;
}

const failure = (code: string, message: string, extra: Record<string, unknown> = {}): CallOutcome => ({
  content: [{ type: 'text', text: message }],
  structuredContent: { error: { code, message, ...extra } },
  isError: true,
});

export type Log = (event: Record<string, unknown>) => void;

export async function callTool(name: string, rawArgs: unknown, ctx: ToolContext, orderEntry: boolean, log?: Log): Promise<CallOutcome> {
  const tool = toolsFor(orderEntry).find((candidate) => candidate.name === name);
  if (!tool) return failure('UNKNOWN_TOOL', `There is no tool ${name}.`);
  try {
    const args = validateArgs(rawArgs, tool.inputSchema);
    const result = await tool.handler(args, ctx);
    return { content: [{ type: 'text', text: result.text }], structuredContent: result.data };
  } catch (error) {
    if (error instanceof ArgumentError) return failure('INVALID_ARGUMENTS', `Invalid arguments for ${name}: ${error.message}.`);
    if (error instanceof ApiError) return failure(error.code ?? `HTTP_${error.status}`, error.message, { status: error.status });
    log?.({ msg: 'tool_failed', tool: name, error: error instanceof Error ? `${error.name}: ${error.message}` : 'unknown' });
    const reason = error instanceof Error && error.name === 'TimeoutError' ? 'the API did not answer in time' : 'the API could not be reached';
    return failure('UPSTREAM_UNREACHABLE', `${name} failed: ${reason}. Try again in a moment.`);
  }
}

export function instructions(orderEntry: boolean): string {
  return [
    'Cikada.MES for Lauresta: sales orders, customers, items with prices and stock, and the management reports.',
    'Orders: orders_filters gives the exact status / store labels; orders_search finds orders; order_get shows one order by number.',
    'Products and prices: items_search (base, purchase and price-group price, available stock), stock_get (per location), price_groups_list; customers_search / customer_get show a customer and its price group.',
    orderEntry
      ? 'Order entry: order_entry_catalog / order_entry_references give every id; order_entry_get gives the revision every write needs. Never guess an id, a number or a price; summarise and get an explicit yes before order_create, order_confirm, order_cancel or order_product_remove. After a refused write read the order again.'
      : 'Order entry (creating or changing orders) is not enabled on this server.',
    'Management reports (live, with history since 2020): report_production (made / in production / queue / stopped by product type, series, state, day or month), report_work (accruals per employee and duty), report_payments (money received by method and branch); report_dimensions gives exact filter values. Answer from them directly, and draw a chart from data.rows when it helps.',
    'Every call runs as the signed-in user; a 403 means the user lacks the Cikada role for it.',
  ].join('\n');
}

export function createServer(ctx: ToolContext, orderEntry: boolean, log?: Log): Server {
  const server = new Server({ name: 'cikada', title: 'Cikada.MES', version: VERSION }, { capabilities: { tools: {}, prompts: {} }, instructions: instructions(orderEntry) });
  server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: toolsFor(orderEntry).map((tool) => ({
      name: tool.name,
      title: tool.title,
      description: tool.description,
      inputSchema: tool.inputSchema as { type: 'object' },
      annotations: { title: tool.title, ...tool.annotations },
    })),
  }));
  server.setRequestHandler(CallToolRequestSchema, async (request) => callTool(request.params.name, request.params.arguments ?? {}, ctx, orderEntry, log));
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
