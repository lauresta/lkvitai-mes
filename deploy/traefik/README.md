# LKvitai.MES Traefik Routing

Dynamic config for the public Traefik instance on the Contabo VPS.

The GitHub Actions workflow `.github/workflows/deploy-traefik-dynamic.yml` runs on
the `lauresta-vps` self-hosted runner and installs these files into:

```text
/opt/traefik/dynamic
```

## Current Backends

Production VM `lkvitai-apps`:

```text
mes.lauresta.com                 -> http://10.11.12.9:5010
portal.mes.lauresta.com          -> http://10.11.12.9:5010
warehouse.mes.lauresta.com       -> http://10.11.12.9:5000
api.mes.lauresta.com/portal      -> http://10.11.12.9:5011
api.mes.lauresta.com/warehouse   -> http://10.11.12.9:5001
```

Test VM `lkvitai-test` (both hostnames via `HostRegexp`):

```text
mes-test.lauresta.com  \
lkvitai.lauresta.com    /  -> same backends, cert resolved per SNI by Let's Encrypt

/                      -> redirects to /portal/
/portal                -> http://10.11.12.15:5010
/warehouse             -> http://10.11.12.15:5000
/api/portal            -> http://10.11.12.15:5011
/api/warehouse         -> http://10.11.12.15:5001
```

Production API routers strip the module prefix before forwarding. For example,
`https://api.mes.lauresta.com/warehouse/api/auth/login` reaches the Warehouse API
as `/api/auth/login`.

Test routing intentionally stays path-based under `https://mes-test.lauresta.com`
because Cloudflare Universal SSL does not cover deep subdomains such as
`*.mes-test.lauresta.com`. Test API routers strip `/api/portal` and
`/api/warehouse` before forwarding.
