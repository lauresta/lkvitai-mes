import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import type { AddressInfo } from 'node:net';
import { test } from 'node:test';
import { ConfigError, loadConfig } from '../src/config.js';
import { checkBearer } from '../src/oauth.js';
import { CLIENT_ID, fakeJwt, rpc, running, testConfig } from './helpers.js';

const BASE = 'https://cikada-test.example.com';

test('configuration needs the public URL, the client id and both APIs, and holds no secret', () => {
  const config = testConfig();
  assert.equal(config.publicUrl, BASE);
  assert.equal(config.oauth.clientId, CLIENT_ID);
  assert.equal(config.orderEntry, false);
  assert.equal(testConfig({ MCP_ENABLE_ORDER_ENTRY: 'true' }).orderEntry, true);
  assert.throws(() => loadConfig({ MCP_PUBLIC_URL: BASE, SALES_API_URL: 'http://s', WAREHOUSE_API_URL: 'http://w' }), ConfigError);
  assert.throws(() => testConfig({ MCP_PUBLIC_URL: 'http://cikada.example.com' }), ConfigError);
  assert.throws(() => testConfig({ MCP_PUBLIC_URL: 'https://cikada.example.com/mcp' }), ConfigError);
  assert.equal(testConfig({ MCP_PUBLIC_URL: 'http://localhost:5070' }).publicUrl, 'http://localhost:5070');
});

test('protected resource metadata names /mcp and this host as its authorization server, including client-added trailing slashes', async () => {
  const stand = await running(testConfig());
  try {
    for (const path of ['/.well-known/oauth-protected-resource', '/.well-known/oauth-protected-resource/mcp', '/.well-known/oauth-protected-resource/mcp/']) {
      const response = await fetch(stand.base + path);
      assert.equal(response.status, 200);
      const body = (await response.json()) as Record<string, unknown>;
      assert.equal(body.resource, `${BASE}/mcp`);
      assert.deepEqual(body.authorization_servers, [BASE]);
      assert.ok((body.scopes_supported as string[]).includes('cikada'));
      assert.ok((body.scopes_supported as string[]).includes('offline_access'));
    }
  } finally {
    await stand.close();
  }
});

test('authorization server metadata: PKCE S256, public client, registration here, and the openid-configuration alias says the same', async () => {
  const stand = await running(testConfig());
  try {
    const metadata = (await (await fetch(`${stand.base}/.well-known/oauth-authorization-server`)).json()) as Record<string, unknown>;
    assert.equal(metadata.issuer, BASE);
    assert.equal(metadata.authorization_endpoint, `${BASE}/authorize`);
    assert.equal(metadata.token_endpoint, `${BASE}/token`);
    assert.equal(metadata.registration_endpoint, `${BASE}/register`);
    assert.deepEqual(metadata.code_challenge_methods_supported, ['S256']);
    assert.deepEqual(metadata.token_endpoint_auth_methods_supported, ['none']);
    assert.deepEqual(metadata.grant_types_supported, ['authorization_code', 'refresh_token']);
    const alias = (await (await fetch(`${stand.base}/.well-known/openid-configuration`)).json()) as Record<string, unknown>;
    assert.deepEqual(alias, metadata);
  } finally {
    await stand.close();
  }
});

test('/mcp without a token is a 401 whose challenge points at the resource metadata', async () => {
  const stand = await running(testConfig());
  try {
    const response = await rpc(stand.base, undefined, 'tools/list');
    assert.equal(response.status, 401);
    assert.equal(response.headers.get('www-authenticate'), `Bearer resource_metadata="${BASE}/.well-known/oauth-protected-resource/mcp"`);
    const slash = await fetch(`${stand.base}/mcp/`);
    assert.equal(slash.status, 401);
    assert.equal(slash.headers.get('www-authenticate'), response.headers.get('www-authenticate'));
  } finally {
    await stand.close();
  }
});

test('an expired token, another issuer or another audience is a 401 invalid_token; no API is called', async () => {
  const stand = await running(testConfig());
  try {
    for (const token of [fakeJwt({ exp: Math.floor(Date.now() / 1000) - 10 }), fakeJwt({ iss: 'https://sso.lauresta.com/application/o/other/' }), fakeJwt({ aud: 'someone-else' }), 'not-a-jwt']) {
      const response = await rpc(stand.base, token, 'tools/list');
      assert.equal(response.status, 401);
      assert.match(response.headers.get('www-authenticate') ?? '', /error="invalid_token"/);
    }
  } finally {
    await stand.close();
  }
});

test('checkBearer accepts an issuer with or without its trailing slash and an audience list', () => {
  const config = testConfig();
  assert.equal(checkBearer(`Bearer ${fakeJwt({ iss: 'https://sso.lauresta.com/application/o/cikada-mcp' })}`, config).ok, true);
  assert.equal(checkBearer(`Bearer ${fakeJwt({ aud: ['x', CLIENT_ID] })}`, config).ok, true);
  assert.deepEqual(checkBearer(undefined, config), { ok: false, reason: 'missing' });
  const none = `${Buffer.from('{"alg":"none"}').toString('base64url')}.${Buffer.from(JSON.stringify({ iss: 'https://sso.lauresta.com/application/o/cikada-mcp/', exp: 9999999999 })).toString('base64url')}.`;
  assert.deepEqual(checkBearer(`Bearer ${none}`, config), { ok: false, reason: 'malformed' });
});

test('/register answers RFC 7591 with the static public client and echoes the redirect URIs', async () => {
  const stand = await running(testConfig());
  try {
    const redirect = ['https://chatgpt.com/connector_platform_oauth_redirect'];
    const response = await fetch(`${stand.base}/register`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ client_name: 'ChatGPT', redirect_uris: redirect, token_endpoint_auth_method: 'none' }),
    });
    assert.equal(response.status, 201);
    const body = (await response.json()) as Record<string, unknown>;
    assert.equal(body.client_id, CLIENT_ID);
    assert.equal(body.token_endpoint_auth_method, 'none');
    assert.deepEqual(body.redirect_uris, redirect);
    assert.equal(body.client_secret, undefined);

    const bad = await fetch(`${stand.base}/register`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ redirect_uris: ['http://evil.example.com/cb'] }) });
    assert.equal(bad.status, 400);
  } finally {
    await stand.close();
  }
});

test('/authorize redirects to authentik with the client parameters, without resource, adding the cikada scope', async () => {
  const stand = await running(testConfig());
  try {
    const query = new URLSearchParams({
      response_type: 'code',
      client_id: CLIENT_ID,
      redirect_uri: 'https://claude.ai/api/mcp/auth_callback',
      code_challenge: 'abc',
      code_challenge_method: 'S256',
      state: 's1',
      scope: 'openid profile',
      resource: `${BASE}/mcp`,
    });
    const response = await fetch(`${stand.base}/authorize?${query}`, { redirect: 'manual' });
    assert.equal(response.status, 302);
    const location = new URL(response.headers.get('location')!);
    assert.equal(location.origin + location.pathname, 'https://sso.lauresta.com/application/o/authorize/');
    assert.equal(location.searchParams.get('resource'), null);
    assert.equal(location.searchParams.get('scope'), 'openid profile cikada');
    assert.equal(location.searchParams.get('state'), 's1');
    assert.equal(location.searchParams.get('code_challenge_method'), 'S256');

    const bare = await fetch(`${stand.base}/authorize?client_id=${CLIENT_ID}&response_type=code`, { redirect: 'manual' });
    assert.equal(new URL(bare.headers.get('location')!).searchParams.get('scope'), 'openid profile email offline_access cikada');
  } finally {
    await stand.close();
  }
});

test('/token relays the form to authentik without resource and returns its answer as it is', async () => {
  let received = '';
  const upstream = createServer((req, res) => {
    const chunks: Buffer[] = [];
    req.on('data', (c: Buffer) => chunks.push(c));
    req.on('end', () => {
      received = Buffer.concat(chunks).toString('utf8');
      res.writeHead(200, { 'content-type': 'application/json' }).end('{"access_token":"a.b.c","token_type":"Bearer","expires_in":300}');
    });
  });
  await new Promise<void>((resolve) => upstream.listen(0, '127.0.0.1', resolve));
  const port = (upstream.address() as AddressInfo).port;
  const stand = await running(testConfig({ MCP_OAUTH_TOKEN_URL: `http://127.0.0.1:${port}/application/o/token/` }));
  try {
    const response = await fetch(`${stand.base}/token`, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ grant_type: 'authorization_code', code: 'c1', code_verifier: 'v1', client_id: CLIENT_ID, redirect_uri: 'https://chatgpt.com/cb', resource: `${BASE}/mcp` }).toString(),
    });
    assert.equal(response.status, 200);
    assert.equal(((await response.json()) as Record<string, unknown>).access_token, 'a.b.c');
    const form = new URLSearchParams(received);
    assert.equal(form.get('resource'), null);
    assert.equal(form.get('code_verifier'), 'v1');
    assert.equal(form.get('grant_type'), 'authorization_code');
  } finally {
    await stand.close();
    await new Promise<void>((resolve) => upstream.close(() => resolve()));
  }
});

test('/healthz answers ok without a token', async () => {
  const stand = await running(testConfig());
  try {
    const response = await fetch(`${stand.base}/healthz`);
    assert.equal(response.status, 200);
    assert.equal(await response.text(), 'ok');
  } finally {
    await stand.close();
  }
});
