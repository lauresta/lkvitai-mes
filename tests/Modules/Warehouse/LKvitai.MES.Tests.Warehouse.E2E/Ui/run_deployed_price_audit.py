#!/usr/bin/env python3
"""Read-only DB snapshot, deployed EN/LT UI audit, and post-run server log gate."""

import os
import pathlib
import subprocess
import sys


HERE = pathlib.Path(__file__).resolve().parent
ROOT = pathlib.Path(subprocess.check_output(
    ["git", "rev-parse", "--show-toplevel"], cwd=HERE, text=True).strip())
OUT = pathlib.Path("/tmp/warehouse-price-audit")
OUT.mkdir(exist_ok=True)
HOST = "root@10.11.12.15"
DB = "lkvitai-mes-db-test"
SERVICES = ("lkvitai-mes-warehouse-webui", "lkvitai-mes-warehouse-api")


def ssh(command, input_text=None):
    return subprocess.run(["ssh", "-o", "BatchMode=yes", HOST, command],
                          input=input_text, text=True, capture_output=True)


sql = (HERE / "PriceAuditExpected.sql").read_text()
snapshot = ssh(f"docker exec -i {DB} psql -v ON_ERROR_STOP=1 -U postgres -d lkvitai_warehouse_test -At", sql)
if snapshot.returncode:
    print("SELECT snapshot failed:", snapshot.stderr, file=sys.stderr)
    sys.exit(2)
(OUT / "expected.json").write_text(snapshot.stdout)

environment = os.environ.copy()
storage_state = environment.get("WAREHOUSE_E2E_STORAGE_STATE")
if not storage_state or not pathlib.Path(storage_state).is_file():
    sys.exit("Set WAREHOUSE_E2E_STORAGE_STATE to an ordinary authenticated browser storage-state file.")
project = HERE.parent / "LKvitai.MES.Tests.Warehouse.E2E.csproj"
test = subprocess.run(["dotnet", "test", str(project), "-c", "Release", "--no-restore",
                       "--filter", "FullyQualifiedName~DeployedPriceAuditTests",
                       "--logger", "console;verbosity=minimal"], cwd=ROOT, env=environment)

start = (OUT / "run-start.txt").read_text().strip()
log_errors = 0
for service in SERVICES:
    result = ssh(f"docker logs --since {start} {service}")
    log = result.stdout + result.stderr
    (OUT / f"{service}.log").write_text(log)
    matches = [line for line in log.splitlines() if "ERROR" in line.upper() or "EXCEPTION" in line.upper()]
    log_errors += len(matches)
    print(f"{service}: {len(matches)} ERROR/exception log lines")
    if result.returncode:
        print(f"docker logs failed for {service}: {result.stderr}", file=sys.stderr)
        log_errors += 1

print(f"UI result: {'GREEN' if test.returncode == 0 else 'RED'}; server logs: {'GREEN' if log_errors == 0 else 'RED'}")
print(f"Local artifacts: {OUT}")
sys.exit(0 if test.returncode == 0 and log_errors == 0 else 1)
