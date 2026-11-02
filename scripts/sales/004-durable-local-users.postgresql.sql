-- Cikada Warehouse PostgreSQL only. NOT a legacy SQL Server script.
-- Review/apply explicitly before switching Warehouse API to DatabaseAdminUserStore.
-- Adds one empty table. Existing identities/hashes must be imported privately,
-- retaining GUIDs and roles, before the application is restarted. No users seeded.
BEGIN;
DO $migration$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'Expected Warehouse EF migration history is absent';
    END IF;
    IF EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
               WHERE "MigrationId" = '20261101204244_AddDurableLocalUserAccounts') THEN
        IF to_regclass('public.local_user_accounts') IS NULL THEN
            RAISE EXCEPTION 'Migration history exists but local_user_accounts is absent';
        END IF;
        RETURN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
                   WHERE "MigrationId" = '20260723140000_AddInboundShipmentAdditionalCostsAndItemType') THEN
        RAISE EXCEPTION 'Expected Warehouse baseline migration is absent';
    END IF;
    IF to_regclass('public.local_user_accounts') IS NOT NULL THEN
        RAISE EXCEPTION 'Untracked local_user_accounts table exists; inspect rather than overwrite';
    END IF;
    CREATE TABLE public.local_user_accounts (
        "Id" uuid NOT NULL,
        "Username" character varying(100) NOT NULL,
        "NormalizedUsername" character varying(100) NOT NULL,
        "Email" character varying(320) NOT NULL,
        "PasswordHash" character varying(1024) NOT NULL,
        "Roles" text[] NOT NULL,
        "Status" character varying(16) NOT NULL,
        "LegacySqlLogin" character varying(128),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_local_user_accounts" PRIMARY KEY ("Id")
    );
    CREATE UNIQUE INDEX "IX_local_user_accounts_NormalizedUsername"
        ON public.local_user_accounts ("NormalizedUsername");
    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20261101204244_AddDurableLocalUserAccounts', '8.0.0');
END
$migration$;
COMMIT;
