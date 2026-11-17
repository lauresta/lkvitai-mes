-- Legacy SQL moves from Windows SQL Server 2014 (TERMINAL, 192.168.0.3) to SQL Server 2025 on Ubuntu (192.168.0.32).
-- There is no Windows authentication on Linux: logins TERMINAL\x.name become SQL logins x.name,
-- and every TERMINAL\ prefix is stripped from the legacy data (SYS_User, menus, rights, work periods).
-- Cikada users must impersonate the new names (EXECUTE AS LOGIN in Sales, Shopfloor, Finance).
--
-- Run against the Cikada users database (test: lkvitai_warehouse_test on lkvitai-test) at cutover,
-- after the legacy database is restored on the new server and before the test stack is redeployed.
-- Idempotent. Rollback: 006-legacy-login-strip-terminal-prefix.rollback below.

BEGIN;

SELECT "Username", "LegacySqlLogin" AS before_login,
       lower(substring("LegacySqlLogin" FROM position(E'\\' IN "LegacySqlLogin") + 1)) AS after_login
FROM public.local_user_accounts
WHERE "LegacySqlLogin" ILIKE E'TERMINAL\\\\%';

UPDATE public.local_user_accounts
SET "LegacySqlLogin" = lower(substring("LegacySqlLogin" FROM position(E'\\' IN "LegacySqlLogin") + 1)),
    "UpdatedAt" = now()
WHERE "LegacySqlLogin" ILIKE E'TERMINAL\\\\%';

SELECT "Username", "LegacySqlLogin" FROM public.local_user_accounts ORDER BY "Username";

COMMIT;

-- Rollback (back to the old server):
-- UPDATE public.local_user_accounts
-- SET "LegacySqlLogin" = E'TERMINAL\\' || "LegacySqlLogin", "UpdatedAt" = now()
-- WHERE "LegacySqlLogin" <> 'sa' AND position(E'\\' IN "LegacySqlLogin") = 0 AND "LegacySqlLogin" <> '';
