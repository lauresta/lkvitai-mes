# S2-012 bounded SQL wait observer

Started2026-11-01 09:02:11UTC; automatic expiry2026-11-02 03:02:11UTC
(05:02 Europe/Vilnius). This is a temporary diagnostic, not an application
service, scheduled task or production-schema migration.

- Container: `sales-s2012-readonly-observer` on the test host10.11.12.15.
- Target: current Sales API SQL host name `ce83d5900550`.
- Resource bounds:0.1CPU,256MB RAM,128PIDs,64MB output,18hours maximum runtime.
- It polls existing SQL DMVs once per second and writes only slow-request
  metadata. No business INSERT/UPDATE/DELETE, procedure execution, new SQL
  objects, configuration changes or public listeners are included.
- Output includes session/connection IDs, wait type/resource, blocker session,
  program/module, query hash, CPU/read counters and runnable task counts.
  Batch SQL is hashed in memory; raw text, parameter values, credentials and
  Portal session proofs are not stored.
- Production SQL credentials are read from the existing API configuration into
  supervisor memory and sent via stdin, not command arguments or new env files.
  This uses the existing login's rights; it does not create a new limited login.
- The observer uses the stack's named Docker network. It does not keep an API
  network-namespace dependency. **Its host-name filter must be rebound if the
  API container is replaced.** It has no automatic restart policy.

Remote private paths (root-only parent directory):

- `/opt/lkvitai-mes/m2-private-deploy/s2012-observer/state.json`
- `/opt/lkvitai-mes/m2-private-deploy/s2012-observer/capture/observations.jsonl`
- `/opt/lkvitai-mes/m2-private-deploy/s2012-observer/stderr.log`
- `/opt/lkvitai-mes/m2-private-deploy/s2012-observer/supervisor.log`

Read status using the authorized SSH jump, without printing API environment:

```sh
ssh -J contabo root@10.11.12.15 \
  'docker inspect --format "{{.State.Running}}" sales-s2012-readonly-observer'
```

Stop early, if required:

```sh
ssh -J contabo root@10.11.12.15 \
  'docker stop -t 3 sales-s2012-readonly-observer'
```

The container removes itself on exit; private evidence remains. Retrieve only
these diagnostic files, not Docker/API environment dumps. Correlate SQL's
ObservedUtc with API request/connection IDs; the hosts' clocks can differ by a
few seconds. Do not equate an absent sample with proof that no short wait
occurred. If no naturally slow request occurs, no historical wait can be
reconstructed from these new samples.

Validation used two read-only connections locking only owned order2148197453
for2.4seconds. Twelve samples identified the exact blocking session and lock
wait. Private validation and source artifacts are under
`/tmp/sales-m2-evidence/s2012`; business procedures and jobs were not altered.
