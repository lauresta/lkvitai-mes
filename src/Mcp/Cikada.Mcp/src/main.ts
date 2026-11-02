#!/usr/bin/env node
// The container's entry point. Configuration: MCP_PUBLIC_URL, MCP_OAUTH_CLIENT_ID, SALES_API_URL, WAREHOUSE_API_URL, optional MCP_PORT (8080) and
// MCP_ENABLE_ORDER_ENTRY. No secret is read: the OAuth client is public and the operator's token arrives with each request.

import { ConfigError, loadConfig } from './config.js';
import { startRemote, stderrLog } from './http.js';

try {
  const config = loadConfig(process.env);
  const server = await startRemote(config);
  const address = server.address();
  stderrLog({
    msg: 'listening',
    port: typeof address === 'object' && address ? address.port : config.port,
    publicUrl: config.publicUrl,
    sales: config.salesApiUrl,
    warehouse: config.warehouseApiUrl,
    orderEntry: config.orderEntry,
  });
  const stop = () => server.close(() => process.exit(0));
  process.once('SIGTERM', stop);
  process.once('SIGINT', stop);
} catch (error) {
  stderrLog({ msg: 'fatal', error: error instanceof ConfigError || error instanceof Error ? error.message : String(error) });
  process.exit(1);
}
