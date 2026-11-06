// The whole configuration of the process, read once from the environment. There is no secret here: the OAuth client is a public client (PKCE, no
// secret), and the operator's own access token is the only credential, carried per request and never stored.

export const DEFAULT_PORT = 8080;
export const DEFAULT_ISSUER = 'https://sso.lauresta.com/application/o/cikada-mcp/';
export const DEFAULT_AUTHORIZE_URL = 'https://sso.lauresta.com/application/o/authorize/';
export const DEFAULT_TOKEN_URL = 'https://sso.lauresta.com/application/o/token/';
export const DEFAULT_JWKS_URL = 'https://sso.lauresta.com/application/o/cikada-mcp/jwks/';
export const SCOPES = ['openid', 'profile', 'email', 'offline_access', 'cikada'] as const;

export interface OAuthSettings {
  /** The public OAuth client id registered in authentik (application `cikada-mcp`). Handed out by /register. */
  clientId: string;
  /** The `iss` of access tokens minted for the client. */
  issuer: string;
  authorizeUrl: string;
  tokenUrl: string;
  jwksUrl: string;
}

export interface Config {
  port: number;
  /** What clients see, the origin only: https://cikada-test.lauresta.com. Discovery documents and the resource (`<origin>/mcp`) are built from it. */
  publicUrl: string;
  oauth: OAuthSettings;
  /** Sales API on the compose network, e.g. http://lkvitai-mes-sales-api:8080. */
  salesApiUrl: string;
  /** Warehouse API on the compose network, e.g. http://lkvitai-mes-warehouse-api:8080. */
  warehouseApiUrl: string;
  financeApiUrl: string;
  /** MCP_ENABLE_ORDER_ENTRY=true: the order entry tools (writes and their helper reads) are offered. */
  orderEntry: boolean;
}

export class ConfigError extends Error {}

/** Drops trailing slashes without a backtracking regex. */
export const stripTrailingSlashes = (text: string): string => {
  let end = text.length;
  while (end > 0 && text.charCodeAt(end - 1) === 47) end--;
  return text.slice(0, end);
};

const trimUrl = (value: string | undefined): string | undefined => (value === undefined ? undefined : stripTrailingSlashes(value.trim()) || undefined);

function httpUrl(name: string, value: string | undefined, fallback?: string): string {
  const text = trimUrl(value) ?? fallback;
  if (!text) throw new ConfigError(`${name} is not set`);
  let url: URL;
  try {
    url = new URL(text);
  } catch {
    throw new ConfigError(`${name} is not a URL`);
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') throw new ConfigError(`${name} must be http or https`);
  return text;
}

export function loadConfig(env: Readonly<Record<string, string | undefined>>): Config {
  const portText = env.MCP_PORT?.trim() || env.PORT?.trim() || String(DEFAULT_PORT);
  const port = Number(portText);
  if (!Number.isInteger(port) || port < 0 || port > 65535) throw new ConfigError('MCP_PORT is not a port');

  const publicText = httpUrl('MCP_PUBLIC_URL', env.MCP_PUBLIC_URL);
  const pub = new URL(publicText);
  const loopback = pub.hostname === 'localhost' || pub.hostname === '127.0.0.1' || pub.hostname === '[::1]';
  if (pub.protocol !== 'https:' && !loopback) throw new ConfigError('MCP_PUBLIC_URL must be https (http only on a loopback address)');
  if (pub.pathname !== '/' || pub.search || pub.hash) throw new ConfigError('MCP_PUBLIC_URL is the origin only, e.g. https://cikada-test.lauresta.com');

  const clientId = env.MCP_OAUTH_CLIENT_ID?.trim();
  if (!clientId) throw new ConfigError('MCP_OAUTH_CLIENT_ID is not set (the public client id of the authentik application cikada-mcp)');

  // the issuer keeps its trailing slash: authentik writes it that way in `iss`
  const issuer = env.MCP_OAUTH_ISSUER?.trim() || DEFAULT_ISSUER;

  return {
    port,
    publicUrl: pub.origin,
    oauth: {
      clientId,
      issuer,
      authorizeUrl: env.MCP_OAUTH_AUTHORIZE_URL?.trim() || DEFAULT_AUTHORIZE_URL,
      tokenUrl: env.MCP_OAUTH_TOKEN_URL?.trim() || DEFAULT_TOKEN_URL,
      jwksUrl: env.MCP_OAUTH_JWKS_URL?.trim() || DEFAULT_JWKS_URL,
    },
    salesApiUrl: httpUrl('SALES_API_URL', env.SALES_API_URL),
    warehouseApiUrl: httpUrl('WAREHOUSE_API_URL', env.WAREHOUSE_API_URL),
    financeApiUrl: httpUrl('FINANCE_API_URL', env.FINANCE_API_URL),
    orderEntry: env.MCP_ENABLE_ORDER_ENTRY?.trim().toLowerCase() === 'true',
  };
}
