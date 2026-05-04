-- ============================================================
-- mes_OperationsSummary
-- Portal operations summary: stage counts, raw status counts,
-- created-by-day and completed-by-day for one calendar month.
--
-- All objects created by this file use the mes_ prefix.
-- Safe to run multiple times (DROP + CREATE pattern for SQL 2014).
--
-- Parameters
--   @Period  NVARCHAR(10)  'this' = current month (default)
--                          'last' = previous calendar month
--
-- Result sets (always 6, always in this order)
--   1. Period      (1 row)   PeriodKey, PeriodFrom, PeriodTo
--   2. Stages      (8 rows)  StageKey, StageLabel, StageCount
--   3. Statuses    (n rows)  StatusName, StatusCount
--   4. CreatedByDay          Day (DATE), DayCount
--   5. CompletedByDay        Day (DATE), DayCount
--   6. BranchesOnTrack       BranchName, ReadyBasis, ReadyCount, IssuedCount, OnTrackPercent (INT NULL)
--
-- Business rules
--   * Exclude test branch: FilialoID <> 7
--   * Exclude customer-cancelled orders:
--       Sugadintas = 1
--       AND LTRIM(RTRIM(ISNULL(sNotes,''))) = 'Kliento prasymu anuliuotas'
--   * Period scope : dtDateInsert in [@From, @To)  (@To = first day of next month)
--   * completedByDay date: ISNULL(dtFinishingOrderDate, dtDateInsert)
--   * branchesOnTrack readiness:
--       Klaipeda (FilialoID=1): ready = Pagamintas (StateID=1173102374) + Isduotas klientui (StateID=2)
--       All other branches:     ready = Issistas i filiala (StateID=1173102376) + Isduotas klientui (StateID=2)
--       onTrackPercent = IssuedCount * 100 / ReadyCount  (NULL when ReadyCount = 0)
-- ============================================================

IF OBJECT_ID(N'dbo.mes_OperationsSummary', N'P') IS NOT NULL
    DROP PROCEDURE dbo.mes_OperationsSummary;
GO

CREATE PROCEDURE [dbo].[mes_OperationsSummary]
    @Period NVARCHAR(10) = N'this'
AS
BEGIN
    SET NOCOUNT ON;

    -- -------------------------------------------------------
    -- Resolve period date range
    -- -------------------------------------------------------
    DECLARE @Today  DATE = CAST(GETDATE() AS DATE);
    DECLARE @From   DATE;
    DECLARE @To     DATE;   -- exclusive upper bound (= first day of NEXT month)
    DECLARE @ToIncl DATE;   -- inclusive last day (for display only)

    IF ISNULL(@Period, N'') = N'last'
    BEGIN
        SET @From   = DATEFROMPARTS(YEAR(DATEADD(month, -1, @Today)), MONTH(DATEADD(month, -1, @Today)), 1);
        SET @To     = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
    END
    ELSE  -- 'this' or any other value → current month
    BEGIN
        SET @From   = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
        SET @To     = DATEFROMPARTS(YEAR(DATEADD(month, 1, @Today)), MONTH(DATEADD(month, 1, @Today)), 1);
    END;

    SET @ToIncl = DATEADD(day, -1, @To);

    -- -------------------------------------------------------
    -- Result set 1 — Period metadata  (1 row)
    -- -------------------------------------------------------
    SELECT
        CASE WHEN ISNULL(@Period, N'') = N'last' THEN N'last' ELSE N'this' END AS PeriodKey,
        CONVERT(NVARCHAR(10), @From,   126)                                    AS PeriodFrom,
        CONVERT(NVARCHAR(10), @ToIncl, 126)                                    AS PeriodTo;

    -- -------------------------------------------------------
    -- Result set 2 — Stage counts  (8 rows, zero-filled)
    -- -------------------------------------------------------
    ;WITH Counts AS (
        SELECT u.StateID, COUNT(*) AS Cnt
        FROM dbo.Uzsakymai u
        INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
        WHERE
            us.FilialoID <> 7
            AND CAST(u.dtDateInsert AS DATE) >= @From
            AND CAST(u.dtDateInsert AS DATE) <  @To
            AND (
                u.Sugadintas = 0
                OR LTRIM(RTRIM(ISNULL(u.sNotes, N''))) <> N'Kliento prasymu anuliuotas'
            )
        GROUP BY u.StateID
    )
    SELECT StageKey, StageLabel, StageCount FROM (
        SELECT 1 AS SortKey, N'intake'      AS StageKey, N'New / Intake'          AS StageLabel, ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID IN (4, 1173102373)),    0) AS StageCount
        UNION ALL SELECT 2, N'queued',      N'Queued',               ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID IN (8, 1173102375)),    0)
        UNION ALL SELECT 3, N'production',  N'In production',        ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID = 1),                   0)
        UNION ALL SELECT 4, N'mixed',       N'Mixed',                ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID = 0),                   0)
        UNION ALL SELECT 5, N'blocked',     N'Blocked',              ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID IN (5,6,7,1173102377)), 0)
        UNION ALL SELECT 6, N'produced',    N'Produced',             ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID = 1173102374),          0)
        UNION ALL SELECT 7, N'branch',      N'In branch / delivery', ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID = 1173102376),          0)
        UNION ALL SELECT 8, N'completed',   N'Completed',            ISNULL((SELECT SUM(Cnt) FROM Counts WHERE StateID = 2),                   0)
    ) AS StageRows
    ORDER BY SortKey;

    -- -------------------------------------------------------
    -- Result set 3 — Raw status counts
    -- -------------------------------------------------------
    SELECT
        LTRIM(RTRIM(b.Busena)) AS StatusName,
        COUNT(*)               AS StatusCount
    FROM dbo.Uzsakymai u
    INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
    INNER JOIN dbo.Zinynas_busenos         b  ON b.BusenosID         = u.StateID
    WHERE
        us.FilialoID <> 7
        AND CAST(u.dtDateInsert AS DATE) >= @From
        AND CAST(u.dtDateInsert AS DATE) <  @To
        AND (
            u.Sugadintas = 0
            OR LTRIM(RTRIM(ISNULL(u.sNotes, N''))) <> N'Kliento prasymu anuliuotas'
        )
    GROUP BY LTRIM(RTRIM(b.Busena))
    ORDER BY StatusCount DESC;

    -- -------------------------------------------------------
    -- Result set 4 — Created by day
    -- -------------------------------------------------------
    SELECT
        CAST(u.dtDateInsert AS DATE) AS [Day],
        COUNT(*)                     AS DayCount
    FROM dbo.Uzsakymai u
    INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
    WHERE
        us.FilialoID <> 7
        AND CAST(u.dtDateInsert AS DATE) >= @From
        AND CAST(u.dtDateInsert AS DATE) <  @To
        AND (
            u.Sugadintas = 0
            OR LTRIM(RTRIM(ISNULL(u.sNotes, N''))) <> N'Kliento prasymu anuliuotas'
        )
    GROUP BY CAST(u.dtDateInsert AS DATE)
    ORDER BY [Day];

    -- -------------------------------------------------------
    -- Result set 5 — Completed by day
    --    Anchored on ISNULL(dtFinishingOrderDate, dtDateInsert)
    --    so orders without a finishing date fall back to their
    --    creation date. Only StateID = 2 (Isduotas klientui).
    -- -------------------------------------------------------
    SELECT
        CAST(ISNULL(u.dtFinishingOrderDate, u.dtDateInsert) AS DATE) AS [Day],
        COUNT(*)                                                      AS DayCount
    FROM dbo.Uzsakymai u
    INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
    WHERE
        us.FilialoID <> 7
        AND u.StateID = 2
        AND CAST(ISNULL(u.dtFinishingOrderDate, u.dtDateInsert) AS DATE) >= @From
        AND CAST(ISNULL(u.dtFinishingOrderDate, u.dtDateInsert) AS DATE) <  @To
        AND (
            u.Sugadintas = 0
            OR LTRIM(RTRIM(ISNULL(u.sNotes, N''))) <> N'Kliento prasymu anuliuotas'
        )
    GROUP BY CAST(ISNULL(u.dtFinishingOrderDate, u.dtDateInsert) AS DATE)
    ORDER BY [Day];

    -- -------------------------------------------------------
    -- Result set 6 — Branches on track
    --   For Klaipeda (FilialoID=1):
    --     ready = Pagamintas (StateID=1173102374) + Isduotas klientui (StateID=2)
    --   For all other branches:
    --     ready = Issistas i filiala (StateID=1173102376) + Isduotas klientui (StateID=2)
    --   onTrackPercent = IssuedCount * 100 / ReadyCount  (NULL when ReadyCount=0)
    -- -------------------------------------------------------
    ;WITH BranchOrders AS (
        SELECT us.FilialoID, u.StateID
        FROM dbo.Uzsakymai u
        INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
        WHERE
            us.FilialoID <> 7
            AND CAST(u.dtDateInsert AS DATE) >= @From
            AND CAST(u.dtDateInsert AS DATE) <  @To
            AND (
                u.Sugadintas = 0
                OR LTRIM(RTRIM(ISNULL(u.sNotes, N''))) <> N'Kliento prasymu anuliuotas'
            )
    ),
    BranchAgg AS (
        SELECT
            o.FilialoID,
            SUM(CASE
                WHEN o.FilialoID = 1
                    THEN CASE WHEN o.StateID IN (1173102374, 2) THEN 1 ELSE 0 END
                ELSE
                    CASE WHEN o.StateID IN (1173102376, 2) THEN 1 ELSE 0 END
            END) AS ReadyCount,
            SUM(CASE WHEN o.StateID = 2 THEN 1 ELSE 0 END) AS IssuedCount,
            COUNT(*) AS TotalCount
        FROM BranchOrders o
        GROUP BY o.FilialoID
    )
    SELECT
        f.FilialoPavadinimas                                                        AS BranchName,
        CASE WHEN f.FilialoID = 1 THEN N'Pagamintas' ELSE N'Išsiųstas į filialą' END AS ReadyBasis,
        a.ReadyCount,
        a.IssuedCount,
        CASE WHEN a.ReadyCount > 0
             THEN CAST(ROUND(a.IssuedCount * 100.0 / a.ReadyCount, 0) AS INT)
             ELSE NULL
        END AS OnTrackPercent
    FROM BranchAgg a
    INNER JOIN dbo.Zinynas_filialai f ON f.FilialoID = a.FilialoID
    WHERE a.TotalCount > 0
    ORDER BY f.FilialoPavadinimas;

END;
GO
