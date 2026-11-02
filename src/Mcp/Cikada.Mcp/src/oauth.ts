// OAuth 2.1 for the remote MCP server, in front of authentik (sso.lauresta.com).
//
// MCP clients (ChatGPT, claude.ai, Claude Desktop) look for discovery on the resource's own host and expect Dynamic Client Registration. Authentik has
// no DCR, so this process is the public face of the authorization server:
//   /.well-known/oauth-protected-resource[/mcp]   RFC 9728: the resource is <base>/mcp, its authorization server is <base>
//   /.well-known/oauth-authorization-server       RFC 8414 (and /.well-known/openid-configuration as an alias)
//   /register                                     RFC 7591: always answers with the one static public client id (PKCE, no secret)
//   /authorize                                    302 to authentik's authorize endpoint (drops `resource`, adds the default scopes when none were asked)
//   /token                                        relays the form post to authentik's token endpoint (drops `resource`) and answers as authentik did
// The person signs in on sso.lauresta.com; this process never sees a password or a session. It holds no secret.
//
// On /mcp the bearer is checked cheaply (a JWT, not expired, the expected issuer); the signature, audience and roles are checked by the Cikada APIs,
// which receive the very same token.

import type { IncomingMessage, ServerResponse } from 'node:http';
import { SCOPES, stripTrailingSlashes, type Config } from './config.js';

const MAX_FORM_BYTES = 16 * 1024;
const UPSTREAM_TIMEOUT_MS = 10_000;

export type Log = (event: Record<string, unknown>) => void;

export const resourceUrl = (config: Config): string => `${config.publicUrl}/mcp`;
export const resourceMetadataUrl = (config: Config): string => `${config.publicUrl}/.well-known/oauth-protected-resource/mcp`;

// ---------------------------------------------------------------------------------------------------------------------------------- discovery

export function protectedResourceMetadata(config: Config): Record<string, unknown> {
  return {
    resource: resourceUrl(config),
    authorization_servers: [config.publicUrl],
    scopes_supported: [...SCOPES],
    bearer_methods_supported: ['header'],
    resource_name: 'Cikada.MES',
  };
}

export function authorizationServerMetadata(config: Config): Record<string, unknown> {
  return {
    issuer: config.publicUrl,
    authorization_endpoint: `${config.publicUrl}/authorize`,
    token_endpoint: `${config.publicUrl}/token`,
    registration_endpoint: `${config.publicUrl}/register`,
    jwks_uri: config.oauth.jwksUrl,
    scopes_supported: [...SCOPES],
    response_types_supported: ['code'],
    response_modes_supported: ['query'],
    grant_types_supported: ['authorization_code', 'refresh_token'],
    code_challenge_methods_supported: ['S256'],
    token_endpoint_auth_methods_supported: ['none'],
    subject_types_supported: ['public'],
    id_token_signing_alg_values_supported: ['RS256'],
  };
}

/** The `WWW-Authenticate` of a 401 on /mcp: where discovery starts (RFC 9728 section 5.1). */
export function challenge(config: Config, error?: 'invalid_token'): string {
  return `Bearer resource_metadata="${resourceMetadataUrl(config)}"${error ? `, error="${error}"` : ''}`;
}

// ---------------------------------------------------------------------------------------------------------------------------------- bearer check

/** Who the token says the caller is: for display only (whoami); every API validates the token itself. */
export interface TokenUser {
  username?: string;
  name?: string;
  email?: string;
  roles: string[];
}

export type TokenCheck = { ok: true; token: string; subject?: string; expiresAt: number; user: TokenUser } | { ok: false; reason: 'missing' | 'malformed' | 'expired' | 'issuer' | 'audience' };

const BEARER = /^Bearer\s+([A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*)\s*$/i;

function decodePart(part: string): Record<string, unknown> | undefined {
  try {
    const parsed: unknown = JSON.parse(Buffer.from(part, 'base64url').toString('utf8'));
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Shape, expiry, issuer and (when present) audience of the bearer. Not a signature check: that is the APIs' job, and they get this token unchanged.
 * This only separates "sign in again" (401 with the discovery challenge) from a request worth forwarding.
 */
function claimRoles(raw: unknown): string[] {
  let list: unknown[] = [];
  if (Array.isArray(raw)) list = raw;
  else if (typeof raw === 'string') list = raw.split(',');
  return list.filter((r): r is string => typeof r === 'string').map((r) => r.trim()).filter(Boolean);
}

function tokenUser(claims: Record<string, unknown>): TokenUser {
  const user: TokenUser = { roles: claimRoles(claims.roles) };
  if (typeof claims.preferred_username === 'string') user.username = claims.preferred_username;
  if (typeof claims.name === 'string') user.name = claims.name;
  if (typeof claims.email === 'string') user.email = claims.email;
  return user;
}

export function checkBearer(header: string | undefined, config: Config, now: number = Date.now()): TokenCheck {
  if (!header || !header.trim()) return { ok: false, reason: 'missing' };
  const match = BEARER.exec(header);
  if (!match) return { ok: false, reason: 'malformed' };
  const token = match[1]!;
  const [head, body] = token.split('.');
  const header0 = decodePart(head!);
  const claims = decodePart(body!);
  if (!header0 || !claims || typeof header0.alg !== 'string' || header0.alg.toLowerCase() === 'none') return { ok: false, reason: 'malformed' };
  const exp = claims.exp;
  if (typeof exp !== 'number') return { ok: false, reason: 'malformed' };
  if (exp * 1000 <= now) return { ok: false, reason: 'expired' };
  const issuer = typeof claims.iss === 'string' ? claims.iss : '';
  if (stripTrailingSlashes(issuer) !== stripTrailingSlashes(config.oauth.issuer)) return { ok: false, reason: 'issuer' };
  const aud = claims.aud;
  if (aud !== undefined) {
    const audiences = Array.isArray(aud) ? aud : [aud];
    if (!audiences.includes(config.oauth.clientId)) return { ok: false, reason: 'audience' };
  }
  const user = tokenUser(claims);
  return { ok: true, token, expiresAt: exp * 1000, user, ...(typeof claims.sub === 'string' ? { subject: claims.sub } : {}) };
}

// ---------------------------------------------------------------------------------------------------------------------------------- endpoints

const CORS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET, POST, OPTIONS',
  'access-control-allow-headers': 'content-type, authorization, mcp-protocol-version',
  'access-control-max-age': '86400',
};

function json(res: ServerResponse, status: number, body: unknown, headers: Record<string, string> = {}): void {
  res.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store', ...CORS, ...headers });
  res.end(JSON.stringify(body));
}

async function readLimited(req: IncomingMessage): Promise<Buffer | undefined> {
  const chunks: Buffer[] = [];
  let size = 0;
  for await (const chunk of req) {
    size += (chunk as Buffer).length;
    if (size > MAX_FORM_BYTES) return undefined;
    chunks.push(chunk as Buffer);
  }
  return Buffer.concat(chunks);
}

const isHttpsOrLoopback = (value: string): boolean => {
  try {
    const url = new URL(value);
    if (url.protocol === 'https:') return true;
    return url.protocol === 'http:' && (url.hostname === 'localhost' || url.hostname === '127.0.0.1' || url.hostname === '[::1]');
  } catch {
    return false;
  }
};

/**
 * RFC 7591 without a registry: every client gets the same public client id. Authentik decides which redirect URIs it accepts (chatgpt.com, claude.ai,
 * localhost); here they are only checked for form and echoed back, so a client sees in the answer what it asked for.
 */
async function register(config: Config, req: IncomingMessage, res: ServerResponse): Promise<void> {
  const body = await readLimited(req);
  if (!body) {
    json(res, 413, { error: 'invalid_client_metadata', error_description: 'the request is too large' });
    return;
  }
  let metadata: Record<string, unknown> = {};
  if (body.length > 0) {
    try {
      const parsed: unknown = JSON.parse(body.toString('utf8'));
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('not an object');
      metadata = parsed as Record<string, unknown>;
    } catch {
      json(res, 400, { error: 'invalid_client_metadata', error_description: 'the body must be a JSON object' });
      return;
    }
  }
  const redirects = metadata.redirect_uris;
  if (redirects !== undefined && (!Array.isArray(redirects) || !redirects.every((uri) => typeof uri === 'string' && isHttpsOrLoopback(uri)))) {
    json(res, 400, { error: 'invalid_redirect_uri', error_description: 'redirect_uris must be https URLs (http only on localhost)' });
    return;
  }
  json(res, 201, {
    client_id: config.oauth.clientId,
    client_id_issued_at: Math.floor(Date.now() / 1000),
    token_endpoint_auth_method: 'none',
    grant_types: ['authorization_code', 'refresh_token'],
    response_types: ['code'],
    scope: SCOPES.join(' '),
    ...(Array.isArray(redirects) ? { redirect_uris: redirects } : {}),
    ...(typeof metadata.client_name === 'string' ? { client_name: metadata.client_name.slice(0, 200) } : {}),
  });
}

/** The browser arrives with the client's parameters and goes on to authentik with them; `resource` stays here, and no scope means the default ones. */
function authorize(config: Config, res: ServerResponse, url: URL): void {
  const target = new URL(config.oauth.authorizeUrl);
  for (const [name, value] of url.searchParams) {
    if (name === 'resource') continue;
    target.searchParams.append(name, value);
  }
  const scope = target.searchParams.get('scope')?.trim();
  if (!scope) target.searchParams.set('scope', SCOPES.join(' '));
  else if (!scope.split(/\s+/).includes('cikada')) target.searchParams.set('scope', `${scope} cikada`);
  if (!target.searchParams.get('client_id')) target.searchParams.set('client_id', config.oauth.clientId);
  res.writeHead(302, { location: target.toString(), 'cache-control': 'no-store' }).end();
}

/** The token endpoint: the form goes to authentik as it came, without `resource`, and authentik's answer comes back as it is. */
async function token(config: Config, req: IncomingMessage, res: ServerResponse, log: Log): Promise<void> {
  const body = await readLimited(req);
  if (!body) {
    json(res, 413, { error: 'invalid_request', error_description: 'the request is too large' });
    return;
  }
  const form = new URLSearchParams(body.toString('utf8'));
  form.delete('resource');
  try {
    const headers: Record<string, string> = { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' };
    // a confidential client would send Basic credentials; this client is public, but pass the header on if a client sends it anyway
    if (typeof req.headers.authorization === 'string') headers.authorization = req.headers.authorization;
    const response = await fetch(config.oauth.tokenUrl, {
      method: 'POST',
      headers,
      body: form.toString(),
      redirect: 'manual',
      signal: AbortSignal.timeout(UPSTREAM_TIMEOUT_MS),
    });
    if (response.status >= 400) log({ msg: 'token_refused', status: response.status, grant: form.get('grant_type') ?? 'none' });
    res.writeHead(response.status, { 'content-type': response.headers.get('content-type') ?? 'application/json', 'cache-control': 'no-store', pragma: 'no-cache', ...CORS });
    res.end(Buffer.from(await response.arrayBuffer()));
  } catch {
    json(res, 503, { error: 'temporarily_unavailable', error_description: 'sso.lauresta.com did not answer' });
  }
}

export const OAUTH_PATHS = [
  '/.well-known/oauth-protected-resource',
  '/.well-known/oauth-protected-resource/mcp',
  '/.well-known/oauth-authorization-server',
  '/.well-known/oauth-authorization-server/mcp',
  '/.well-known/openid-configuration',
  '/.well-known/openid-configuration/mcp',
  '/mcp/.well-known/openid-configuration',
  '/register',
  '/authorize',
  '/token',
] as const;

/** Handles the OAuth paths; true when the request was one of them (and is answered). */
export async function handleOAuth(config: Config, req: IncomingMessage, res: ServerResponse, path: string, log: Log): Promise<boolean> {
  if (!(OAUTH_PATHS as readonly string[]).includes(path)) return false;
  try {
    if (req.method === 'OPTIONS') {
      res.writeHead(204, CORS).end();
      return true;
    }
    if (path.startsWith('/.well-known/oauth-protected-resource')) {
      if (req.method !== 'GET' && req.method !== 'HEAD') json(res, 405, { error: 'GET only' }, { allow: 'GET' });
      else json(res, 200, protectedResourceMetadata(config), { 'cache-control': 'public, max-age=300' });
    } else if (path.includes('/.well-known/')) {
      if (req.method !== 'GET' && req.method !== 'HEAD') json(res, 405, { error: 'GET only' }, { allow: 'GET' });
      else json(res, 200, authorizationServerMetadata(config), { 'cache-control': 'public, max-age=300' });
    } else if (path === '/authorize') {
      if (req.method !== 'GET') json(res, 405, { error: 'GET only' }, { allow: 'GET' });
      else authorize(config, res, new URL(req.url ?? '/', config.publicUrl));
    } else if (req.method !== 'POST') {
      json(res, 405, { error: 'POST only' }, { allow: 'POST, OPTIONS' });
    } else if (path === '/register') {
      await register(config, req, res);
    } else {
      await token(config, req, res, log);
    }
  } catch (error) {
    log({ msg: 'error', path, error: error instanceof Error ? error.name : 'unknown' });
    if (!res.headersSent) json(res, 500, { error: 'server_error' });
  }
  return true;
}
