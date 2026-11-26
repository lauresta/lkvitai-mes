import assert from 'node:assert/strict';
import { test } from 'node:test';
import { initSentry, reportError, withoutCredentials } from '../src/sentry.js';

test('Sentry stays off without SENTRY_DSN, and reporting is then a no-op', () => {
  assert.equal(initSentry({}), false);
  assert.equal(initSentry({ SENTRY_DSN: '  ', SENTRY_ENVIRONMENT: 'test' }), false);
  assert.doesNotThrow(() => reportError(new Error('boom'), { tool: 'orders_search' }, 'jonas'));
});

test('events leave without the bearer token and cookies of the request', () => {
  const event = {
    request: {
      url: 'https://cikada-test.example.com/mcp',
      headers: { Authorization: 'Bearer secret', cookie: 'a=b', 'content-type': 'application/json' },
      cookies: { a: 'b' },
    },
  };
  const sent = withoutCredentials(event);
  assert.deepEqual(sent.request.headers, { 'content-type': 'application/json' });
  assert.equal('cookies' in sent.request, false);
  assert.deepEqual(withoutCredentials({}), {});
});
