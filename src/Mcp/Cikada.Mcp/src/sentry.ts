// Sentry for the MCP server, on only when SENTRY_DSN is set (the compose files pass the DSN of the environment's Sentry project together with
// SENTRY_ENVIRONMENT and SENTRY_RELEASE). Uncaught exceptions and unhandled rejections are reported by the SDK itself; tool and request failures
// through reportError. The operator's bearer token and cookies are removed from every event before it leaves the process.

import * as Sentry from '@sentry/node';

type Env = Readonly<Record<string, string | undefined>>;

const CREDENTIAL_HEADERS = new Set(['authorization', 'cookie']);

const value = (text: string | undefined): string | undefined => text?.trim() || undefined;

/** Drops the Authorization and Cookie headers and the cookies of the request an event carries. */
export function withoutCredentials<T extends { request?: { headers?: Record<string, string>; cookies?: unknown } }>(event: T): T {
  const request = event.request;
  if (!request) return event;
  for (const name of Object.keys(request.headers ?? {})) {
    if (CREDENTIAL_HEADERS.has(name.toLowerCase())) delete request.headers?.[name];
  }
  delete request.cookies;
  return event;
}

/** Starts the SDK when SENTRY_DSN is set; returns whether it did. Every event carries the tag module=mcp. */
export function initSentry(env: Env): boolean {
  const dsn = value(env.SENTRY_DSN);
  if (!dsn) return false;
  Sentry.init({
    dsn,
    environment: value(env.SENTRY_ENVIRONMENT),
    release: value(env.SENTRY_RELEASE),
    initialScope: { tags: { module: 'mcp' } },
    beforeSend: withoutCredentials,
  });
  return true;
}

/** Reports an unexpected failure; a no-op while Sentry is off. */
export function reportError(error: unknown, tags: Record<string, string>, username?: string): void {
  Sentry.captureException(error, { tags, ...(username ? { user: { username } } : {}) });
}

/** Waits (bounded) until queued events are sent, e.g. before the process exits. */
export const flushSentry = (timeoutMs = 2000): Promise<boolean> => Sentry.flush(timeoutMs);
