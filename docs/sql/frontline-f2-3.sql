/*
================================================================================
 Frontline F-2.3 — dbo.mes_Fabric_GetLowStockList (low-stock list SQL adapter)
--------------------------------------------------------------------------------
 Server-side filter / sort / paging for the desktop low-stock list. Replaces
 the F-1 in-memory stub used by SqlFabricQueryService.GetLowStockListAsync.

 Result set (single, paged):
   Code, Name, PhotoUrl, WidthMm, AvailableMeters, ThresholdMeters, Status,
   ExpectedDate, IncomingMeters, Supplier, AlternativeCodes, LastChecked,
   CanReserve, CanNotify, CanReplace, TotalRows

 Status enum (matches FabricAvailabilityStatus on the .NET side):
   1 = Enough, 2 = Low, 3 = None (out), 4 = Discontinued, 0 = Unknown.

 Inclusion rule: a row appears in the list if
     Available < @ThresholdMeters
   OR Status IN (None, Discontinued).
 In other words, "Out" and "Discontinued" are always shown regardless of
 threshold — they need attention even at high threshold settings.

 Sorting (server-side):
   @SortBy IN ('Code', 'Available', 'LastChecked'); fallback Code ASC.
   @SortDir IN ('ASC', 'DESC'); fallback ASC.
   Code ASC is always used as the secondary tie-breaker so paging stays stable.

 Paging: @Page (1-based), @PageSize (1..500). Windowed COUNT() emits the same
 TotalRows on every row — same shape as dbo.weblb_Orders_Paged so the C# reader
 can read it from the last row of the page.

 Idempotent: DROP-then-CREATE because SQL Server 2014 has no CREATE OR ALTER.
================================================================================
*/

USE LKVITAIDBV1SQL;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '== F-2.3: creating dbo.mes_Fabric_GetLowStockList ==';

IF OBJECT_ID('dbo.mes_Fabric_GetLowStockList', 'P') IS NOT NULL
    DROP PROCEDURE dbo.mes_Fabric_GetLowStockList;
GO

CREATE PROCEDURE dbo.mes_Fabric_GetLowStockList
    @Search           nvarchar(100) = NULL,
    @ThresholdMeters  int           = 25,
    @Status           nvarchar(20)  = NULL,    -- 'low' | 'out' | 'disc' | NULL/'all'
    @WidthMm          int           = NULL,
    @Supplier         nvarchar(100) = NULL,
    @SortBy           nvarchar(20)  = 'Code',
    @SortDir          nvarchar(4)   = 'ASC',
    @Page             int           = 1,
    @PageSize         int           = 50,
    @LowThreshold     int           = 10,    -- < @LowThreshold => Status=Low (red chip)
    @DefaultPhotoUrl  nvarchar(256) = N'/img/fabric_pl.png'
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page     IS NULL OR @Page     < 1   SET @Page     = 1;
    IF @PageSize IS NULL OR @PageSize < 1   SET @PageSize = 50;
    IF @PageSize > 500                      SET @PageSize = 500;
    IF @ThresholdMeters IS NULL OR @ThresholdMeters < 0 SET @ThresholdMeters = 25;
    IF @LowThreshold    IS NULL OR @LowThreshold    < 0 SET @LowThreshold    = 10;

    DECLARE @SearchNorm   nvarchar(100) = NULLIF(LTRIM(RTRIM(@Search)),   N'');
    DECLARE @SupplierNorm nvarchar(100) = NULLIF(LTRIM(RTRIM(@Supplier)), N'');
    DECLARE @StatusNorm   nvarchar(20)  = NULLIF(LOWER(LTRIM(RTRIM(@Status))), N'');

    DECLARE @SortByNorm  nvarchar(20) = CASE LOWER(ISNULL(@SortBy, ''))
        WHEN 'available'   THEN 'Available'
        WHEN 'lastchecked' THEN 'LastChecked'
        ELSE 'Code'
    END;
    DECLARE @SortDirNorm nvarchar(4)  =
        CASE WHEN UPPER(ISNULL(@SortDir, '')) = 'DESC' THEN 'DESC' ELSE 'ASC' END;

    ;WITH Base AS (
        SELECT
            Code           = c.sCodeInternal,
            Name           = ISNULL(c.sName, N''),
            WidthMm        = ISNULL(c.fa_WidthMm, 2000),
            Available      = ISNULL(CAST(r.Likutis AS int), 0),
            ExpectedDate   = c.fa_ExpectedDelivery,
            IncomingMeters = c.mes_IncomingMeters,
            LastChecked    = c.mes_LastCheckedAt,
            Supplier       = sup.sName,
            Alts           = c.fa_Alternatives,
            IsDiscontinued = ISNULL(c.fa_IsDiscontinued, 0)
        FROM dbo.TBD_Components AS c
        LEFT JOIN dbo.V_Remains AS r
               ON UPPER(LTRIM(RTRIM(r.Kodas))) = UPPER(LTRIM(RTRIM(c.sCodeInternal)))
        LEFT JOIN dbo.TBD_Suppliers AS sup
               ON sup.iSupplierID = c.iSupplierID
        WHERE c.iComponentCategoryID = 1
          AND c.iComponentID NOT IN (3739, 3143)
    ),
    Classified AS (
        SELECT
            *,
            -- Effective EnoughThreshold for status: the user's threshold filter.
            -- Items below the user's threshold cannot be "Enough" by definition,
            -- so we mirror the picture the operator expects on screen.
            Status = CASE
                        WHEN IsDiscontinued = 1                                            THEN 4  -- Discontinued
                        WHEN Available >= @ThresholdMeters                                 THEN 1  -- Enough (only happens when threshold filter inactive)
                        WHEN Available BETWEEN 1 AND @LowThreshold - 1                     THEN 2  -- Low (red)
                        WHEN Available BETWEEN @LowThreshold AND @ThresholdMeters - 1      THEN 2  -- Low (orange — same enum value, UI varies tone)
                        WHEN Available = 0                                                 THEN 3  -- None / Out
                        ELSE 0                                                                     -- Unknown (defensive)
                     END
        FROM Base
    ),
    Filtered AS (
        SELECT *
        FROM Classified
        WHERE
            -- Inclusion: below user threshold OR Out/Discontinued (always shown).
            (Available < @ThresholdMeters OR Status IN (3, 4))
            -- Status filter (single-status drop-down).
            AND (
                @StatusNorm IS NULL
                OR @StatusNorm = N'all'
                OR (@StatusNorm = N'low'  AND Status = 2)
                OR (@StatusNorm = N'out'  AND Status = 3)
                OR (@StatusNorm = N'disc' AND Status = 4)
            )
            -- Width filter.
            AND (@WidthMm IS NULL OR WidthMm = @WidthMm)
            -- Supplier filter (case-insensitive exact match on display name).
            AND (@SupplierNorm IS NULL OR Supplier = @SupplierNorm)
            -- Free-text search across code / name / supplier.
            AND (
                @SearchNorm IS NULL
                OR Code     LIKE N'%' + @SearchNorm + N'%'
                OR Name     LIKE N'%' + @SearchNorm + N'%'
                OR Supplier LIKE N'%' + @SearchNorm + N'%'
            )
    ),
    Numbered AS (
        SELECT
            *,
            TotalRows = COUNT(*) OVER (),
            -- Sort key fan-out — kept readable instead of dynamic SQL.
            -- Code ASC is the primary tie-breaker on every branch so paging
            -- stays deterministic when two rows share an Available value.
            RowNum = ROW_NUMBER() OVER (
                ORDER BY
                    CASE WHEN @SortByNorm = 'Available'   AND @SortDirNorm = 'ASC'  THEN Available    END ASC,
                    CASE WHEN @SortByNorm = 'Available'   AND @SortDirNorm = 'DESC' THEN Available    END DESC,
                    CASE WHEN @SortByNorm = 'LastChecked' AND @SortDirNorm = 'DESC' THEN LastChecked  END DESC,
                    CASE WHEN @SortByNorm = 'LastChecked' AND @SortDirNorm = 'ASC'  THEN LastChecked  END ASC,
                    CASE WHEN @SortByNorm = 'Code'        AND @SortDirNorm = 'DESC' THEN Code         END DESC,
                    Code ASC
            )
        FROM Filtered
    )
    SELECT
        Code,
        Name,
        PhotoUrl         = @DefaultPhotoUrl,
        WidthMm,
        AvailableMeters  = Available,
        ThresholdMeters  = @ThresholdMeters,
        Status,
        ExpectedDate,
        IncomingMeters,
        Supplier,
        AlternativeCodes = Alts,                                     -- raw CSV; .NET adapter splits + normalises
        LastChecked,
        CanReserve = CASE WHEN Status NOT IN (3, 4) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
        CanNotify  = CASE WHEN Status = 3            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
        CanReplace = CASE WHEN Status = 4            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
        TotalRows
    FROM Numbered
    WHERE RowNum BETWEEN ((@Page - 1) * @PageSize + 1) AND (@Page * @PageSize)
    ORDER BY RowNum;
END
GO

PRINT '== F-2.3: done ==';
GO
