import type { Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { loadConfig, type Config } from '../src/config.js';
import { startRemote, type Dependencies } from '../src/http.js';

export const CLIENT_ID = 'test-public-client';
export const ISSUER = 'https://sso.lauresta.com/application/o/cikada-mcp/';

export function testConfig(overrides: Record<string, string> = {}): Config {
  return loadConfig({
    MCP_PUBLIC_URL: 'https://cikada-test.example.com',
    MCP_OAUTH_CLIENT_ID: CLIENT_ID,
    SALES_API_URL: 'http://sales.internal:8080',
    WAREHOUSE_API_URL: 'http://warehouse.internal:8080',
    MCP_PORT: '0',
    ...overrides,
  });
}

const b64 = (value: unknown): string => Buffer.from(JSON.stringify(value)).toString('base64url');

/** An unsigned token of the right shape: the server checks shape, expiry, issuer and audience only (the APIs check the signature). */
export function fakeJwt(claims: Record<string, unknown> = {}): string {
  return `${b64({ alg: 'RS256', typ: 'JWT', kid: 'k1' })}.${b64({ iss: ISSUER, aud: CLIENT_ID, sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 3600, ...claims })}.c2lnbmF0dXJl`;
}

export interface Seen {
  method: string;
  url: URL;
  headers: Record<string, string>;
  body?: unknown;
}

export type Answer = (seen: Seen) => { status: number; body?: unknown };

/** A fetch stand-in for the Cikada APIs: records every call and answers as the test says. Nothing leaves the process. */
export function mockFetch(answer: Answer): { fetchImpl: typeof fetch; seen: Seen[] } {
  const seen: Seen[] = [];
  const fetchImpl = (async (input: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(input instanceof Request ? input.url : input));
    const headers = Object.fromEntries(Object.entries((init?.headers ?? {}) as Record<string, string>).map(([k, v]) => [k.toLowerCase(), v]));
    const record: Seen = { method: init?.method ?? 'GET', url, headers, ...(typeof init?.body === 'string' ? { body: JSON.parse(init.body) } : {}) };
    seen.push(record);
    const reply = answer(record);
    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), { status: reply.status, headers: { 'content-type': 'application/json' } });
  }) as typeof fetch;
  return { fetchImpl, seen };
}

export interface Running {
  base: string;
  logs: Record<string, unknown>[];
  close(): Promise<void>;
}

export async function running(config: Config, deps: Dependencies = {}): Promise<Running> {
  const logs: Record<string, unknown>[] = [];
  const server: Server = await startRemote(config, (event) => logs.push(event), deps);
  const port = (server.address() as AddressInfo).port;
  return {
    base: `http://127.0.0.1:${port}`,
    logs,
    close: () => new Promise<void>((resolve) => server.close(() => resolve())),
  };
}

let nextId = 1;

/** One JSON-RPC request to /mcp, the way a Streamable HTTP client sends it. */
export async function rpc(base: string, token: string | undefined, method: string, params: Record<string, unknown> = {}): Promise<{ status: number; body: any; headers: Headers }> {
  const response = await fetch(`${base}/mcp`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      accept: 'application/json, text/event-stream',
      'mcp-protocol-version': '2025-06-18',
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ jsonrpc: '2.0', id: nextId++, method, params }),
  });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : undefined, headers: response.headers };
}
