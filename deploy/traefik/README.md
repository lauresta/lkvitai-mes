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

Test VM `lkvitai-test`:

```text
mes-test.lauresta.com                 -> http://10.11.12.15:5010
portal.mes-test.lauresta.com          -> http://10.11.12.15:5010
warehouse.mes-test.lauresta.com       -> http://10.11.12.15:5000
api.mes-test.lauresta.com/portal      -> http://10.11.12.15:5011
api.mes-test.lauresta.com/warehouse   -> http://10.11.12.15:5001
```

API routers strip the module prefix before forwarding. For example,
`https://api.mes.lauresta.com/warehouse/api/auth/login` reaches the Warehouse API
as `/api/auth/login`.
