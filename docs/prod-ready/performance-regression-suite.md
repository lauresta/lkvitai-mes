# Performance Regression Suite

## Automated Unit Baselines

- `PerformanceRegressionTests.WaveCreation_100Waves_ShouldStayWithinBaseline`
- `PerformanceRegressionTests.SerialSearch_1000Records_ShouldStayWithinBaseline`

Run:

```bash
dotnet test src/LKvitai.MES.sln --filter Category=Performance
```

## Load/Stress Smoke Script

File: `scripts/load/warehouse-load-smoke.js`

Run with k6:

```bash
BASE_URL=http://localhost:5000 TOKEN="<bearer-token>" k6 run scripts/load/warehouse-load-smoke.js
```

## Targets

- Error rate < 5%
- p95 latency < 2 seconds
