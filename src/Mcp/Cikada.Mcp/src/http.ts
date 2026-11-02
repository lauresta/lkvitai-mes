// The remote MCP server: Streamable HTTP, stateless, at /mcp; /healthz; and the OAuth discovery / relay endpoints (oauth.ts).
//
// The operator's bearer token is the credential of one request: checked cheaply here, forwarded unchanged to the Sales and Warehouse APIs, and written
// nowhere (not in a log line, not in a variable that outlives the request). Every POST gets a fresh server and transport; nothing is kept between requests.

import { randomUUID } from 'node:crypto';
import { createServer as createHttpServer, type IncomingMessage, type Server as HttpServer, type ServerResponse } from 'node:http';
import { WebStandardStreamableHTTPServerTransport } from '@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js';
import { CikadaApi } from './api.js';
import { stripTrailingSlashes, type Config } from './config.js';
import { challenge, checkBearer, handleOAuth, type Log } from './oauth.js';
import { createServer } from './server.js';

const MAX_BODY_BYTES = 1024 * 1024;

export const stderrLog: Log = (event) => void process.stderr.write(JSON.stringify({ ts: new Date().toISOString(), ...event }) + '\n');

/** Today in Vilnius, YYYY-MM-DD (the business date for prices and default order dates). */
export const vilniusToday = (): string => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Vilnius' }).format(new Date());

function unauthorised(res: ServerResponse, config: Config, invalid: boolean, reason: string): void {
  res.writeHead(401, { 'content-type': 'application/json', 'www-authenticate': challenge(config, invalid ? 'invalid_token' : undefined), 'access-control-expose-headers': 'www-authenticate' });
  res.end(JSON.stringify({ error: invalid ? 'invalid_token' : 'unauthorized', error_description: reason }));
}

function jsonRpcError(res: ServerResponse, status: number, message: string, headers: Record<string, string> = {}): void {
  res.writeHead(status, { 'content-type': 'application/json', ...headers });
  res.end(JSON.stringify({ jsonrpc: '2.0', error: { code: -32000, message }, id: null }));
}

async function readBody(req: IncomingMessage): Promise<Buffer> {
  const chunks: Buffer[] = [];
  let size = 0;
  for await (const chunk of req) {
    size += (chunk as Buffer).length;
    if (size > MAX_BODY_BYTES) throw new RangeError('the request body is too large');
    chunks.push(chunk as Buffer);
  }
  return Buffer.concat(chunks);
}

/** JSON-RPC method names and the tool called, for the request log; never arguments. */
export function rpcSummary(body: Buffer): string[] {
  try {
    const parsed: unknown = JSON.parse(body.toString('utf8'));
    const messages = Array.isArray(parsed) ? parsed : [parsed];
    return messages.slice(0, 10).map((m) => {
      if (!m || typeof m !== 'object') return 'invalid';
      const { method, params } = m as { method?: unknown; params?: { name?: unknown } };
      if (typeof method !== 'string') return 'response';
      return method === 'tools/call' && typeof params?.name === 'string' ? `tools/call:${params.name.slice(0, 48)}` : method;
    });
  } catch {
    return ['unparsable'];
  }
}

export interface Dependencies {
  fetchImpl?: typeof fetch;
  newId?: () => string;
  today?: () => string;
}

export function startRemote(config: Config, log: Log = stderrLog, deps: Dependencies = {}): Promise<HttpServer> {
  const http = createHttpServer(async (req, res) => {
    const started = Date.now();
    const requestPath = new URL(req.url ?? '/', 'http://localhost').pathname;
    // Some MCP clients normalize the endpoint to /mcp/ and append / to its
    // protected-resource discovery URL. Both spellings identify the same MCP
    // resource; keep the advertised canonical resource URL unchanged.
    const path = requestPath === '/' ? '/' : stripTrailingSlashes(requestPath);
    const detail: Record<string, unknown> = {};
    if (path !== '/healthz') res.on('finish', () => log({ msg: 'request', method: req.method, path, status: res.statusCode, ms: Date.now() - started, ...detail }));

    if (path === '/healthz') {
      res.writeHead(200, { 'content-type': 'text/plain' }).end('ok');
      return;
    }
    if (await handleOAuth(config, req, res, path, log)) return;
    if (path !== '/mcp') {
      jsonRpcError(res, 404, 'the MCP endpoint is /mcp');
      return;
    }
    if (req.method === 'OPTIONS') {
      res.writeHead(204, {
        'access-control-allow-origin': '*',
        'access-control-allow-methods': 'POST, OPTIONS',
        'access-control-allow-headers': 'content-type, authorization, mcp-protocol-version, mcp-session-id',
        'access-control-expose-headers': 'www-authenticate',
      }).end();
      return;
    }

    const bearer = checkBearer(req.headers.authorization, config);
    if (!bearer.ok) {
      detail.auth = bearer.reason;
      unauthorised(res, config, bearer.reason !== 'missing', bearer.reason === 'missing' ? 'sign in to Cikada (sso.lauresta.com)' : `the access token is not accepted (${bearer.reason})`);
      return;
    }
    if (req.method !== 'POST') {
      // stateless: no session to resume, no server-initiated stream
      jsonRpcError(res, 405, 'POST only', { allow: 'POST' });
      return;
    }

    let body: Buffer;
    try {
      body = await readBody(req);
    } catch (error) {
      const tooLarge = error instanceof RangeError;
      jsonRpcError(res, tooLarge ? 413 : 400, tooLarge ? 'the request body is too large' : 'the request could not be read');
      return;
    }
    detail.rpc = rpcSummary(body);
    if (bearer.subject) detail.sub = bearer.subject.slice(0, 64);

    const api = new CikadaApi({ salesUrl: config.salesApiUrl, warehouseUrl: config.warehouseApiUrl, token: bearer.token, ...(deps.fetchImpl ? { fetchImpl: deps.fetchImpl } : {}) });
    const server = createServer({ api, newId: deps.newId ?? randomUUID, today: deps.today ?? vilniusToday, user: bearer.user }, config.orderEntry, log);
    const transport = new WebStandardStreamableHTTPServerTransport({ sessionIdGenerator: undefined, enableJsonResponse: true });
    try {
      await server.connect(transport);
      const headers = new Headers();
      for (const [name, value] of Object.entries(req.headers)) {
        if (value !== undefined) headers.set(name, Array.isArray(value) ? value.join(', ') : value);
      }
      const response = await transport.handleRequest(new Request(`http://localhost${req.url ?? '/mcp'}`, { method: 'POST', headers, body: new Uint8Array(body) }));
      const payload = Buffer.from(await response.arrayBuffer());
      if (response.status >= 400) detail.reason = payload.toString('utf8').slice(0, 200);
      res.writeHead(response.status, Object.fromEntries(response.headers));
      res.end(payload);
    } catch (error) {
      log({ msg: 'error', path, error: error instanceof Error ? `${error.name}: ${error.message}` : 'unknown' });
      if (!res.headersSent) jsonRpcError(res, 500, 'internal error');
    } finally {
      void transport.close();
      void server.close();
    }
  });
  return new Promise((resolve, reject) => {
    http.once('error', reject);
    http.listen(config.port, '0.0.0.0', () => resolve(http));
  });
}
