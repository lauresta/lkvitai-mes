// What a tool is: a name, what the model reads, its input schema, its MCP annotations and a handler that makes one or more API calls and returns a
// compact, structured answer (structuredContent) with a short text beside it.

import type { CikadaApi } from './api.js';
import type { Schema } from './validate.js';

export interface Annotations {
  title: string;
  readOnlyHint: boolean;
  destructiveHint: boolean;
  idempotentHint: boolean;
  openWorldHint: boolean;
}

export interface ToolResult {
  /** A few lines a person can read; the model gets the details from `data`. */
  text: string;
  data: Record<string, unknown>;
}

export interface ToolContext {
  api: CikadaApi;
  /** A fresh UUID for an order entry command id; injectable for tests. */
  newId: () => string;
  /** Today in Europe/Vilnius, YYYY-MM-DD. */
  today: () => string;
}

export interface ToolDef {
  name: string;
  title: string;
  description: string;
  inputSchema: Schema;
  annotations: Omit<Annotations, 'title'>;
  /** Offered only when MCP_ENABLE_ORDER_ENTRY=true. */
  orderEntry?: boolean;
  handler: (args: Record<string, unknown>, ctx: ToolContext) => Promise<ToolResult>;
}

/** Reads: nothing changes, a repeated call asks the same question, and the APIs are Cikada's own, not the open world. */
export const READ: ToolDef['annotations'] = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };

/** A write. Idempotent where the command id makes a repeat a replay; destructive where it removes, cancels or irreversibly commits something. */
export const write = (destructive: boolean, idempotent = false): ToolDef['annotations'] => ({
  readOnlyHint: false,
  destructiveHint: destructive,
  idempotentHint: idempotent,
  openWorldHint: false,
});

export const obj = (properties: Record<string, Schema>, required: string[] = []): Schema => ({
  type: 'object',
  properties,
  ...(required.length ? { required } : {}),
  additionalProperties: false,
});

export const str = (description: string, extra: Schema = {}): Schema => ({ type: 'string', description, ...extra });
export const int = (description: string, extra: Schema = {}): Schema => ({ type: 'integer', description, ...extra });
export const num = (description: string, extra: Schema = {}): Schema => ({ type: 'number', description, ...extra });
export const bool = (description: string): Schema => ({ type: 'boolean', description });
export const DATE = (description: string): Schema => ({ type: 'string', description: `${description} (YYYY-MM-DD)`, pattern: '^\\d{4}-\\d{2}-\\d{2}$' });

export const asString = (value: unknown): string | undefined => (typeof value === 'string' && value.trim() ? value.trim() : undefined);
export const asNumber = (value: unknown): number | undefined => (typeof value === 'number' && Number.isFinite(value) ? value : undefined);
export const asBool = (value: unknown): boolean | undefined => (typeof value === 'boolean' ? value : undefined);

export const money = (value: unknown): string => (typeof value === 'number' ? value.toFixed(2) : '-');

/** Drops null, undefined and empty strings from an object: smaller answers, same meaning. */
export function compact<T extends Record<string, unknown>>(value: T): Partial<T> {
  return Object.fromEntries(Object.entries(value).filter(([, v]) => v !== null && v !== undefined && v !== '')) as Partial<T>;
}
