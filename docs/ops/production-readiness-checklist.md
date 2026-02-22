# Production Readiness Checklist

## Build & Test

- [ ] `dotnet build src/LKvitai.MES.sln` passes.
- [ ] `dotnet test src/LKvitai.MES.sln` passes.

## Security

- [ ] `ASPNETCORE_ENVIRONMENT=Production` configured in production.
- [ ] `/api/auth/dev-token` not available in production.
- [ ] Role-based authorization verified for warehouse endpoints.
- [ ] Rate limiting active for API endpoints.
- [ ] Sensitive values masked in error-path logs.

## Reliability

- [ ] Health endpoint (`/health`) monitored.
- [ ] Background jobs (Hangfire) persistent storage configured.
- [ ] Backup and restore procedure tested.

## Observability

- [ ] Correlation ID propagation verified.
- [ ] API logs shipped to central sink.
- [ ] Alerts configured for API downtime / repeated failures.

## Operations

- [ ] Operator runbook published.
- [ ] Deployment guide published.
- [ ] Operator training guide published.

## Advanced Workflow Validation

- [ ] Wave picking flow validated.
- [ ] Cross-dock flow validated.
- [ ] RMA receive/inspect flow validated.
- [ ] Fulfillment KPI and quality analytics pages load.
