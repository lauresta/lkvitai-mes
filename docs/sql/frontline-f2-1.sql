/*
================================================================================
 Frontline F-2.1 — DDL for fabric-availability "check log" + last-checked + incoming
--------------------------------------------------------------------------------
 Adds the schema artifacts F-2.2/F-2.3 will read/write. Idempotent: re-running
 the script is a no-op if the objects already exist (column-existence guard +
 OBJECT_ID guards + DROP-then-CREATE for the proc).

 Naming convention (per F-2 plan):
   - new TBD_Components columns        : mes_*
   - new tables                        : dbo.mes_*
   - new stored procedures             : dbo.mes_*

 Target: SQL Server 2014 (no CREATE OR ALTER, no JSON, no STRING_AGG; that's
 why the proc uses DROP + CREATE and the table uses datetime2(0)).
================================================================================
*/

USE LKVITAIDBV1SQL;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '== F-2.1: adding mes_* columns to dbo.TBD_Components ==';

IF COL_LENGTH('dbo.TBD_Components', 'mes_LastCheckedAt') IS NULL
BEGIN
    ALTER TABLE dbo.TBD_Components ADD mes_LastCheckedAt datetime2(0) NULL;
    PRINT '  + mes_LastCheckedAt';
END
ELSE
    PRINT '  = mes_LastCheckedAt (already present)';
GO

IF COL_LENGTH('dbo.TBD_Components', 'mes_IncomingMeters') IS NULL
BEGIN
    ALTER TABLE dbo.TBD_Components ADD mes_IncomingMeters int NULL;
    PRINT '  + mes_IncomingMeters';
END
ELSE
    PRINT '  = mes_IncomingMeters (already present)';
GO

IF COL_LENGTH('dbo.TBD_Components', 'mes_IncomingDate') IS NULL
BEGIN
    ALTER TABLE dbo.TBD_Components ADD mes_IncomingDate date NULL;
    PRINT '  + mes_IncomingDate';
END
ELSE
    PRINT '  = mes_IncomingDate (already present)';
GO

PRINT '== F-2.1: creating dbo.mes_FabricAvailabilityCheckLog ==';

IF OBJECT_ID('dbo.mes_FabricAvailabilityCheckLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.mes_FabricAvailabilityCheckLog
    (
        iCheckLogID    bigint        IDENTITY(1, 1) NOT NULL,
        sCodeInternal  nvarchar(32)  NOT NULL,
        CheckedAt      datetime2(0)  NOT NULL
            CONSTRAINT DF_mes_FabricAvailabilityCheckLog_CheckedAt DEFAULT (sysutcdatetime()),
        CheckedBy      nvarchar(64)  NULL,
        CONSTRAINT PK_mes_FabricAvailabilityCheckLog PRIMARY KEY CLUSTERED (iCheckLogID)
    );
    PRINT '  + table created';
END
ELSE
    PRINT '  = table already present';
GO

-- Composite index keyed by (Code, time DESC) so the per-fabric "checks last
-- 7d / 30d" rollups in mes_Fabric_GetLowStockList can do a fast range seek
-- instead of a heap scan when the log grows past a few thousand rows.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.mes_FabricAvailabilityCheckLog')
      AND name      = 'IX_mes_FabricAvailabilityCheckLog_Code_Time'
)
BEGIN
    CREATE INDEX IX_mes_FabricAvailabilityCheckLog_Code_Time
        ON dbo.mes_FabricAvailabilityCheckLog (sCodeInternal, CheckedAt DESC);
    PRINT '  + index IX_mes_FabricAvailabilityCheckLog_Code_Time';
END
ELSE
    PRINT '  = index already present';
GO

PRINT '== F-2.1: creating dbo.mes_Fabric_RecordLookup ==';

IF OBJECT_ID('dbo.mes_Fabric_RecordLookup', 'P') IS NOT NULL
    DROP PROCEDURE dbo.mes_Fabric_RecordLookup;
GO

CREATE PROCEDURE dbo.mes_Fabric_RecordLookup
    @Code      nvarchar(32),
    @CheckedBy nvarchar(64) = NULL  -- F-2.4 will pass the operator name once Frontline auth lands
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CheckedAt datetime2(0) = sysutcdatetime();
    DECLARE @Norm      nvarchar(32) = UPPER(LTRIM(RTRIM(@Code)));

    IF @Norm IS NULL OR @Norm = N''
        RETURN;

    BEGIN TRANSACTION;

    -- 1) Stamp last-checked on the master row(s). Width is intentionally
    --    ignored: in TBD_Components today every sCodeInternal maps to a
    --    single row, but we update by code-only so a future multi-width
    --    layout can't silently leave half the rows stale.
    UPDATE dbo.TBD_Components
       SET mes_LastCheckedAt = @CheckedAt
     WHERE sCodeInternal        = @Norm
       AND iComponentCategoryID = 1
       AND iComponentID NOT IN (3739, 3143);

    -- 2) Append a row to the audit log so the low-stock list can render
    --    "checks last 7d / 30d" without a separate counter column. The
    --    INSERT goes through even if step 1 stamped zero rows (unknown
    --    code) — the lookup attempt itself is still operational signal.
    INSERT INTO dbo.mes_FabricAvailabilityCheckLog (sCodeInternal, CheckedAt, CheckedBy)
    VALUES (@Norm, @CheckedAt, @CheckedBy);

    COMMIT TRANSACTION;
END
GO

PRINT '== F-2.1: done ==';
GO
