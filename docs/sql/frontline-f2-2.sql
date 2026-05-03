/*
================================================================================
 Frontline F-2.2 — dbo.mes_Fabric_GetMobileCard (lookup-card SQL adapter source)
--------------------------------------------------------------------------------
 1:1 successor of legacy dbo.weblb_Fabric_GetMobileCard. Same 3-result-set
 shape so the C# adapter can read it positionally:

   RS1 (main, 0 or 1 row):
       Code, Name, Notes, PhotoUrl, DiscountPercent, MesLastCheckedAt
   RS2 (widths, 0..N rows):
       WidthMm, Status, StockMeters, ExpectedDate, IncomingMeters, IncomingDate
   RS3 (alternatives, 0..N rows):
       Code, PhotoUrl, WidthMm, Status, StockMeters, ExpectedDate

 Differences vs. legacy:
   * RS1 adds MesLastCheckedAt (sourced from F-2.1 mes_LastCheckedAt column).
   * RS2 adds IncomingMeters + IncomingDate (sourced from F-2.1 mes_* columns).
   * Notes returned as raw c.sNotes (legacy ISNULL'd to N'-' which is harder
     to suppress in the WebUI than a NULL).
   * DiscountPercent returned raw (no ISNULL) so the WebUI can hide the chip
     when the value is missing instead of rendering "0%".

 Status enum (RS2 + RS3) — same as legacy, mirrors
 LKvitai.MES.Modules.Frontline.Contracts.Fabric.FabricAvailabilityStatus:
   1 = Enough, 2 = Low, 3 = None, 4 = Discontinued, 0 = Unknown (defensive).

 Idempotent: DROP-then-CREATE because SQL Server 2014 has no CREATE OR ALTER.
================================================================================
*/

USE LKVITAIDBV1SQL;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '== F-2.2: creating dbo.mes_Fabric_GetMobileCard ==';

IF OBJECT_ID('dbo.mes_Fabric_GetMobileCard', 'P') IS NOT NULL
    DROP PROCEDURE dbo.mes_Fabric_GetMobileCard;
GO

CREATE PROCEDURE dbo.mes_Fabric_GetMobileCard
    @Code            nvarchar(50),
    @DefaultPhotoUrl nvarchar(256) = N'/img/fabric_pl.png',
    @LowThreshold    int           = 10,    -- < 10  -> Low
    @EnoughThreshold int           = 25     -- >= 25 -> Enough; in-between still Low
AS
BEGIN
    SET NOCOUNT ON;

    -- Guard: unknown / non-fabric / blacklisted code -> emit zero result
    -- sets and return. The C# adapter treats "no rows in RS1" as a 404,
    -- so an early RETURN here is functionally identical to the legacy
    -- proc's behaviour (which the WebUI already handles).
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.TBD_Components c
        WHERE c.sCodeInternal = @Code
          AND c.iComponentCategoryID = 1
          AND c.iComponentID NOT IN (3739, 3143)
    )
    BEGIN
        RETURN;
    END

    ----------------------------------------------------------------------
    -- RS1: main card (always 1 row when reached)
    ----------------------------------------------------------------------
    SELECT TOP (1)
        Code             = c.sCodeInternal,
        Name             = ISNULL(c.sName, N''),
        Notes            = c.sNotes,
        PhotoUrl         = @DefaultPhotoUrl,
        DiscountPercent  = c.fa_DiscountPercent,
        MesLastCheckedAt = c.mes_LastCheckedAt
    FROM dbo.TBD_Components AS c
    WHERE c.sCodeInternal = @Code
      AND c.iComponentCategoryID = 1
      AND c.iComponentID NOT IN (3739, 3143);

    ----------------------------------------------------------------------
    -- RS2: width + stock + status + incoming
    --      JOIN against dbo.V_Remains for live on-hand meters (Likutis).
    ----------------------------------------------------------------------
    ;WITH W AS (
        SELECT
            WidthMm        = ISNULL(c.fa_WidthMm, 2000),
            StockMetersRaw = r.Likutis,                  -- decimal(38,3) NULL
            ExpectedDate   = c.fa_ExpectedDelivery,
            IsDiscontinued = ISNULL(c.fa_IsDiscontinued, 0),
            IncomingMeters = c.mes_IncomingMeters,
            IncomingDate   = c.mes_IncomingDate
        FROM dbo.TBD_Components AS c
        LEFT JOIN dbo.V_Remains AS r
               ON UPPER(LTRIM(RTRIM(r.Kodas))) = UPPER(LTRIM(RTRIM(c.sCodeInternal)))
        WHERE c.sCodeInternal = @Code
          AND c.iComponentCategoryID = 1
          AND c.iComponentID NOT IN (3739, 3143)
    )
    SELECT
        WidthMm,
        Status = CASE
                    WHEN IsDiscontinued = 1 THEN 4
                    WHEN ISNULL(CAST(StockMetersRaw AS int), 0) >= @EnoughThreshold THEN 1
                    WHEN ISNULL(CAST(StockMetersRaw AS int), 0) BETWEEN 1 AND @LowThreshold - 1 THEN 2
                    WHEN ISNULL(CAST(StockMetersRaw AS int), 0) = 0 THEN 3
                    ELSE 0
                 END,
        StockMeters = CASE
                          WHEN StockMetersRaw IS NULL THEN NULL
                          WHEN CAST(StockMetersRaw AS int) < @EnoughThreshold THEN CAST(StockMetersRaw AS int)
                          ELSE NULL                                              -- enough/disc: meter chip suppressed
                      END,
        ExpectedDate,
        IncomingMeters,
        IncomingDate
    FROM W;

    ----------------------------------------------------------------------
    -- RS3: alternatives. CSV in c.fa_Alternatives, with a small fallback
    --      list for the rare row that has no curated alternatives. Uses
    --      the SQL-2014 XML split trick (no STRING_SPLIT on this server).
    ----------------------------------------------------------------------
    DECLARE @alts_raw nvarchar(max);

    SELECT @alts_raw = c.fa_Alternatives
    FROM dbo.TBD_Components AS c
    WHERE c.sCodeInternal = @Code
      AND c.iComponentCategoryID = 1
      AND c.iComponentID NOT IN (3739, 3143);

    IF @alts_raw IS NULL OR LTRIM(RTRIM(@alts_raw)) = N''
        SET @alts_raw = N'R70,R71,R72,R73,R74,R75';

    ;WITH A0 AS (
        SELECT Txt = UPPER(@alts_raw)
    ),
    A1 AS (
        -- Normalise every plausible separator to comma. Operators have used
        -- spaces, semicolons, slashes, pipes, tabs, and CR/LF over the years.
        SELECT Txt = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Txt,
                       N';', N','), N' ', N','), CHAR(9), N','),
                       CHAR(10), N','), CHAR(13), N','), N'|', N','), N'/', N',')
        FROM A0
    ),
    A2 AS (
        -- Strip stray junk chars that have shown up in the wild.
        SELECT Txt = REPLACE(REPLACE(REPLACE(Txt, N'.', N''), N':', N''), N'\', N'')
        FROM A1
    ),
    SPLIT AS (
        SELECT x.n.value('.', 'nvarchar(50)') AS Tok
        FROM A2
        CROSS APPLY (
            SELECT CAST(N'<r><x>' + REPLACE(Txt, N',', N'</x><x>') + N'</x></r>' AS xml)
        ) z(X)
        CROSS APPLY z.X.nodes('/r/x') AS x(n)
    ),
    CLEAN AS (
        -- Trim, strip residual spaces, normalise the legacy "NRxxx" prefix
        -- to "Rxxx" (operators sometimes write "no R" instead of just "R").
        SELECT DISTINCT
            AltCode = CASE
                        WHEN LEFT(REPLACE(LTRIM(RTRIM(Tok)), N' ', N''), 2) = N'NR'
                             THEN N'R' + SUBSTRING(REPLACE(LTRIM(RTRIM(Tok)), N' ', N''), 3, 100)
                        ELSE REPLACE(LTRIM(RTRIM(Tok)), N' ', N'')
                      END
        FROM SPLIT
        WHERE Tok IS NOT NULL AND LTRIM(RTRIM(Tok)) <> N''
    ),
    ALT AS (
        SELECT
            ac.sCodeInternal              AS Code,
            ISNULL(ac.fa_WidthMm, 2000)   AS WidthMm,
            ac.fa_ExpectedDelivery        AS ExpectedDate,
            ISNULL(ac.fa_IsDiscontinued,0) AS IsDiscontinued
        FROM CLEAN s
        JOIN dbo.TBD_Components ac
          ON ac.sCodeInternal = s.AltCode
        WHERE s.AltCode <> UPPER(@Code)
          AND ac.iComponentCategoryID = 1
          AND ac.iComponentID NOT IN (3739, 3143)
    ),
    AR AS (
        SELECT
            a.Code, a.WidthMm, a.ExpectedDate, a.IsDiscontinued,
            r.Likutis AS StockMetersRaw
        FROM ALT a
        LEFT JOIN dbo.V_Remains r
          ON UPPER(LTRIM(RTRIM(r.Kodas))) = UPPER(LTRIM(RTRIM(a.Code)))
    )
    SELECT
        Code,
        PhotoUrl    = @DefaultPhotoUrl,
        WidthMm,
        Status      = CASE
                        WHEN IsDiscontinued = 1 THEN 4
                        WHEN ISNULL(CAST(StockMetersRaw AS int), 0) >= @EnoughThreshold THEN 1
                        WHEN ISNULL(CAST(StockMetersRaw AS int), 0) BETWEEN 1 AND @LowThreshold - 1 THEN 2
                        WHEN ISNULL(CAST(StockMetersRaw AS int), 0) = 0 THEN 3
                        ELSE 0
                      END,
        StockMeters = CASE
                          WHEN StockMetersRaw IS NULL THEN NULL
                          WHEN CAST(StockMetersRaw AS int) < @EnoughThreshold THEN CAST(StockMetersRaw AS int)
                          ELSE NULL
                      END,
        ExpectedDate
    FROM AR
    ORDER BY Code;
END
GO

PRINT '== F-2.2: done ==';
GO
