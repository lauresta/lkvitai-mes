/* 01 – Current production pipeline snapshot (live queue)
   Current state = ParduotiGaminiai.BusenosID (verified = state of the product's latest
   TAB_ProductionOutputDocuments movement; "Įvestas į programą" products have no movement).
   Only manufactured types (Zinynas_tipai.Gamininamas=1), not-cancelled orders, excl. TESTAVIMAS branch.
   Report type = the product type's "(vnt)" report type (rpt.TBD_ReportsTypes.iUnitID=1; lowest id if several). */
DECLARE @now datetime = GETDATE();

WITH wip AS (
    SELECT pg.iBarCode, pg.BusenosID, pg.Kiekis, pg.ParduotoGaminioTipoID,
           COALESCE(pg.dtDateInsert, u.UzsakymoData) AS dtEntered
    FROM dbo.ParduotiGaminiai pg
    JOIN dbo.Uzsakymai u               ON u.UzsakymasID = pg.UzsakymoID
    JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
    JOIN dbo.Zinynas_tipai t           ON t.TipoID = pg.ParduotoGaminioTipoID
    WHERE pg.BusenosID <> 2                -- not yet handed to customer
      AND u.Sugadintas = 0
      AND t.Gamininamas = 1
      AND us.FilialoID <> 7
      AND (@branch IS NULL OR us.FilialoID IN (SELECT FilialoID FROM dbo.Zinynas_filialai WHERE FilialoPavadinimas LIKE N'%' + @branch + N'%'))
      AND (@maxOrderAgeDays IS NULL OR u.UzsakymoData >= DATEADD(day, -@maxOrderAgeDays, @now))
),
since AS (   -- latest movement INTO the current state
    SELECT dd.iBarCode, MAX(d.dtDateTime) AS dtSince
    FROM wip w
    JOIN dbo.TAB_ProductionOutputDocumentDetails dd ON dd.iBarCode = w.iBarCode
    JOIN dbo.TAB_ProductionOutputDocuments d        ON d.iProductionOutputDocumentID = dd.iProductionOutputDocumentID
                                                    AND d.iAssignedStatusID = w.BusenosID
    GROUP BY dd.iBarCode
)
SELECT  b.Busena                                   AS CurrentState,
        rt.sReportTypeName                         AS ReportType,
        COUNT(*)                                   AS ProductLines,
        SUM(w.Kiekis)                              AS Pieces,
        CAST(AVG(CAST(DATEDIFF(hour, COALESCE(s.dtSince, w.dtEntered), @now) AS decimal(18,2))) / 24 AS decimal(10,1)) AS AvgDaysInState,
        MAX(DATEDIFF(day, COALESCE(s.dtSince, w.dtEntered), @now)) AS MaxDaysInState,
        SUM(CASE WHEN COALESCE(s.dtSince, w.dtEntered) < DATEADD(day, -@staleDays, @now) THEN w.Kiekis ELSE 0 END) AS PiecesOverStaleDays
FROM wip w
JOIN dbo.Zinynas_busenos b ON b.BusenosID = w.BusenosID
LEFT JOIN since s          ON s.iBarCode = w.iBarCode
CROSS APPLY (SELECT TOP 1 r.sReportTypeName   -- vnt report types only; unmapped types drop out
             FROM rpt.TCR_BlindTypes_ReportTypes x
             JOIN rpt.TBD_ReportsTypes r ON r.iReportTypeID = x.iReportTypeID AND r.iUnitID = 1
             WHERE x.iBlindTypeID = w.ParduotoGaminioTipoID
             ORDER BY r.iReportTypeID) rt
GROUP BY b.iOrdering, b.Busena, rt.sReportTypeName
ORDER BY b.iOrdering, Pieces DESC;
