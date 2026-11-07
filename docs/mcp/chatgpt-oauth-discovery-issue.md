# ChatGPT custom MCP: OAuth discovery on test

## Status — 2026-11-02

**OAuth discovery is fixed.** In ChatGPT's “Add custom MCP server” form, `https://cikada-test.lauresta.com/mcp` now discovers the authorization, token and dynamic registration endpoints, the MCP resource and OIDC metadata. ChatGPT registered a client through `/register` (`201`) and opened Lauresta SSO. The user confirmed that their connection works. This diagnostic Chrome session remains on its own SSO login page; it has not been independently authenticated. The test MCP log also recorded a successful `/token` (`200`) and authenticated `tools/list` (`200`) in the same verification window, but that request must not be attributed to this particular Chrome session without further evidence.

## Cause and fix

- ChatGPT's previous discovery call returned `404` with `MCP server ... does not implement OAuth`; the MCP container saw no corresponding request.
- Cloudflare Security Events showed `POST /mcp` from `20.250.136.68` challenged by **Bot Fight Mode**, and `GET /.well-known/oauth-protected-resource/mcp` from `172.162.248.79` blocked by **Block non LT or LV or Contabo VPS**. Both IPs are inside OpenAI's [published ChatGPT connector egress ranges](https://openai.com/chatgpt-connectors.json).
- With the user's explicit approval, disabled both the geo custom rule and Bot Fight Mode for the **entire `lauresta.com` zone**. Cloudflare's Free plan could not create path-specific Bot Fight Mode exceptions; its IP Access Rules UI rejected `/28` ranges. These zone-wide changes are live. Cikada's OAuth and application authorization remain enabled. No application deployment was needed for this edge fix.
- PR #234, already merged and deployed as `50503e221ee9030489d9a5aacd90320248e6604b`, fixed the separate trailing-slash discovery paths.

## Remaining verification

1. If further independent verification is needed, authenticate this diagnostic Chrome session and run a read-only MCP tool to check application permissions. Do not perform write operations in this check.
2. Review the OAuth relay's `resource` forwarding and issuer metadata before treating broader MCP client interoperability as complete. These did not block current ChatGPT discovery.

Official references: [Cloudflare Bot Fight Mode](https://developers.cloudflare.com/bots/get-started/bot-fight-mode/), [Cloudflare IP Access Rules](https://developers.cloudflare.com/waf/tools/ip-access-rules/), [OpenAI MCP OAuth](https://developers.openai.com/plugins/build/auth).
