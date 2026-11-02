-- NOT APPLIED — REVIEW / VALIDATION ONLY. Not a migration or deployment script.
-- Proposed replacement for the identical #R calculation in the salary and
-- turnover procedures. No ALTER/CREATE PROCEDURE, job, index or business DML.
-- Running the SELECT creates only a connection-local #R temporary table.
-- Inspect its estimated plan first. Use a separate connection, command timeout
-- about10s, LOCK_TIMEOUT3000 and MAXDOP1. Never execute the old full procedure
-- merely to benchmark this proposal on production.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 3000;
SET DEADLOCK_PRIORITY LOW;

-- BEGIN PROPOSED SELECT
;WITH Manufactured AS
(
    -- Count current manufactured ROWS, not Kiekis and not released barcodes.
    -- Retain rows with no event/NULL barcode in the required count.
    SELECT pg.UzsakymoID AS OrderID,pg.iBarCode AS BarCode
    FROM dbo.ParduotiGaminiai AS pg
    INNER JOIN dbo.Zinynas_tipai AS t ON t.TipoID=pg.ParduotoGaminioTipoID
    WHERE t.Gamininamas<>0
),
RequiredCounts AS
(
    SELECT OrderID,COUNT(*) AS RequiredCount
    FROM Manufactured
    GROUP BY OrderID
),
EventDeltasAtTime AS
(
    -- The original <=timestamp sees every event at that exact timestamp.
    -- Group peers BEFORE the window; an individual-row window is not equivalent.
    SELECT p.OrderID,d.dtDateTime AS EventTime,
        SUM(CASE WHEN d.iAssignedStatusID IN(2,1173102376) THEN 1
                 WHEN d.iAssignedStatusID=1173102377 THEN -1 END) AS NetAtTime
    FROM Manufactured AS p
    INNER JOIN dbo.TAB_ProductionOutputDocumentDetails AS m ON m.iBarCode=p.BarCode
    INNER JOIN dbo.TAB_ProductionOutputDocuments AS d
        ON d.iProductionOutputDocumentID=m.iProductionOutputDocumentID
    WHERE d.iAssignedStatusID IN(2,1173102376,1173102377)
        AND d.dtDateTime IS NOT NULL
    GROUP BY p.OrderID,d.dtDateTime
),
RunningOrderBalance AS
(
    -- This is deliberately ORDER-wide. Do not replace the legacy tautology
    -- O2.BlindID=O2.BlindID with a per-product condition as an incidental fix.
    SELECT OrderID,EventTime,
        SUM(NetAtTime) OVER(PARTITION BY OrderID ORDER BY EventTime
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS OrderBalance
    FROM EventDeltasAtTime
),
FirstEquality AS
(
    SELECT b.OrderID,MIN(b.EventTime) AS FirstOrderReleasmentDate
    FROM RunningOrderBalance AS b
    INNER JOIN RequiredCounts AS n ON n.OrderID=b.OrderID
    WHERE b.OrderBalance=n.RequiredCount
    GROUP BY b.OrderID
)
SELECT r.OrderID,
    s.UzsakymoSerijaPav+'-'+CAST(u.UzsakymoNumeris AS nvarchar(64))+'-'+u.UzsakymoGrupe AS OrderNmbr,
    r.FirstOrderReleasmentDate
INTO #R
FROM FirstEquality AS r
INNER JOIN dbo.Uzsakymai AS u ON u.UzsakymasID=r.OrderID
INNER JOIN dbo.Zinynas_UzsakymoSerijos AS s ON s.UzsakymoSerijaID=u.UzsakymoSerija
OPTION(MAXDOP 1);
-- END PROPOSED SELECT

-- #P, final finishing-date calculation and salary/turnover projections remain
-- unchanged. Reuse/materialization across both procedures is a separate design
-- decision; this file does not alter their invocation or output consumers.
