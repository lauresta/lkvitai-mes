// Calls to the Cikada APIs on the compose network, with the operator's own bearer token, unchanged. Nothing is cached and nothing is retried here:
// the APIs validate the token (signature, audience, roles) and say what the operator may see or do.

export const VERSION = '0.1.0';
const TIMEOUT_MS = 30_000;

export type Target = 'sales' | 'warehouse' | 'finance';

export interface ApiCall {
  target: Target;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  path: string;
  query?: Record<string, string | number | boolean | undefined | null>;
  body?: unknown;
}

export interface ApiResult {
  status: number;
  /** The parsed JSON body, or undefined when there was none or it was not JSON. */
  data: unknown;
  /** The raw body, cut to 2000 characters, for error messages. */
  text: string;
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
  ) {
    super(message);
  }
}

export interface ApiOptions {
  salesUrl: string;
  warehouseUrl: string;
  financeUrl: string;
  token: string;
  fetchImpl?: typeof fetch;
}

export class CikadaApi {
  private readonly fetchImpl: typeof fetch;

  constructor(private readonly options: ApiOptions) {
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  async call(input: ApiCall): Promise<ApiResult> {
    const urls: Record<Target, string> = {
      sales: this.options.salesUrl,
      warehouse: this.options.warehouseUrl,
      finance: this.options.financeUrl,
    };
    const base = urls[input.target];
    const url = new URL(base + input.path);
    for (const [name, value] of Object.entries(input.query ?? {})) {
      if (value !== undefined && value !== null && value !== '') url.searchParams.set(name, String(value));
    }
    const headers: Record<string, string> = {
      accept: 'application/json',
      authorization: `Bearer ${this.options.token}`,
      'user-agent': `cikada-mcp/${VERSION}`,
    };
    let body: string | undefined;
    if (input.body !== undefined) {
      headers['content-type'] = 'application/json';
      body = JSON.stringify(input.body);
    }
    const response = await this.fetchImpl(url, { method: input.method, headers, body, redirect: 'manual', signal: AbortSignal.timeout(TIMEOUT_MS) });
    const text = await response.text();
    let data: unknown;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        data = undefined;
      }
    }
    return { status: response.status, data, text: text.slice(0, 2000) };
  }

  /** The call, and its JSON body when the status is 2xx; anything else is an ApiError with the API's own message when it wrote one (ProblemDetails). */
  async json<T = unknown>(input: ApiCall): Promise<T> {
    const result = await this.call(input);
    if (result.status >= 200 && result.status < 300) return result.data as T;
    throw toApiError(input, result);
  }
}

export function toApiError(input: ApiCall, result: ApiResult): ApiError {
  const problem = (result.data && typeof result.data === 'object' ? result.data : {}) as Record<string, unknown>;
  const code = typeof problem.code === 'string' ? problem.code : undefined;
  const title = typeof problem.title === 'string' ? problem.title : typeof problem.message === 'string' ? problem.message : typeof problem.error === 'string' ? problem.error : undefined;
  const detail = typeof problem.detail === 'string' ? problem.detail : undefined;
  const api = { sales: 'Sales API', warehouse: 'Warehouse API', finance: 'Finance API' }[input.target];
  let message: string;
  switch (result.status) {
    case 401:
      message = `${api} did not accept your sign-in (401). Disconnect and reconnect the Cikada connector to sign in again on sso.lauresta.com.`;
      break;
    case 403:
      message = `${api} refused: your Cikada account does not have the role for this (403)${title ? `: ${title}` : ''}.`;
      break;
    case 404:
      message = `${api}: not found (404)${title ? `: ${title}` : ''}.`;
      break;
    default:
      message = `${api} answered ${result.status}${title ? `: ${title}` : ''}${detail ? ` (${detail})` : ''}${!title && !detail && result.text ? `: ${result.text.slice(0, 300)}` : ''}`;
  }
  return new ApiError(message, result.status, code);
}
