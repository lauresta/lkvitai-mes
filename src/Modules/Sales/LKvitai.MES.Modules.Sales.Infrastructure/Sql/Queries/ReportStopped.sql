/* 03 – Products currently stopped (ParduotiGaminiai.BusenosID = 7 "Sustabdytas")
   StoppedSince = latest TAB_ProductionOutputDocuments movement with iAssignedStatusID = 7 for the barcode.
   There is NO dedicated stop-reason field. Closest free-text sources returned:
     OrderNote   = Uzsakymai.sNotes (mirrors the latest entry of the order-notes log dbo.UzsakymuPastabos)
     ProductNote = ParduotiGaminiai.Pastabos
   States 5 "Trūksta audinio" / 6 "Trūksta detalių" (material shortage) exist in Zinynas_busenos but are
   hidden (bShowInApplicationFlag=0) and never used by any product or movement. */
DECLARE @now datetime = GETDATE();

WITH stopped AS (
    SELECT pg.ParduotoGaminioID, pg.iBarCode, pg.UzsakymoID, pg.ParduotoGaminioTipoID, pg.Spalva,
           pg.Plotis, pg.Aukstis, pg.Kiekis, pg.Pastabos
    FROM dbo.ParduotiGaminiai pg
    WHERE pg.BusenosID = 7                              -- index on BusenosID
),
since AS (   -- latest stop movement per barcode
    SELECT dd.iBarCode, d.dtDateTime AS dtStopped, d.sUserNameInsert AS sStoppedBy, dd.iStateBefore,
           ROW_NUMBER() OVER (PARTITION BY dd.iBarCode ORDER BY d.dtDateTime DESC) AS rn
    FROM stopped s
    JOIN dbo.TAB_ProductionOutputDocumentDetails dd ON dd.iBarCode = s.iBarCode
    JOIN dbo.TAB_ProductionOutputDocuments d        ON d.iProductionOutputDocumentID = dd.iProductionOutputDocumentID
                                                    AND d.iAssignedStatusID = 7
)
SELECT  us.UzsakymoSerijaPav + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(16)) + N'-' + u.UzsakymoGrupe AS OrderNo,
        u.UzsakymoData                    AS OrderDate,
        c.UzsakovoPavadinimas             AS Customer,
        f.FilialoPavadinimas              AS Branch,
        ob.Busena                         AS OrderState,
        s.iBarCode                        AS BarCode,
        r.RusiesPavadinimas               AS ProductCategory,
        t.TipoPavadinimas                 AS ProductType,
        comp.sCodeInternal                AS MaterialCode,
        s.Plotis                          AS WidthMm,
        s.Aukstis                         AS HeightMm,
        s.Kiekis                          AS Pieces,
        si.dtStopped                      AS StoppedSince,
        DATEDIFF(day, si.dtStopped, @now) AS DaysStopped,
        pb.Busena                         AS StateBeforeStop,
        si.sStoppedBy                     AS StoppedBy,
        u.sNotes                          AS OrderNote,
        CAST(s.Pastabos AS nvarchar(400)) AS ProductNote,
        u.Sugadintas                      AS OrderCancelled
FROM stopped s
JOIN dbo.Uzsakymai u                 ON u.UzsakymasID = s.UzsakymoID
JOIN dbo.Zinynas_UzsakymoSerijos us  ON us.UzsakymoSerijaID = u.UzsakymoSerija
JOIN dbo.Zinynas_filialai f          ON f.FilialoID = us.FilialoID
JOIN dbo.Zinynas_uzsakovai c         ON c.UzsakovoID = u.UzsakovoID
JOIN dbo.Zinynas_busenos ob          ON ob.BusenosID = u.StateID
JOIN dbo.Zinynas_tipai t             ON t.TipoID = s.ParduotoGaminioTipoID
JOIN dbo.Zinynas_rusys r             ON r.RusiesID = t.Rusis
LEFT JOIN dbo.TBD_Components comp    ON comp.iComponentID = s.Spalva
LEFT JOIN since si                   ON si.iBarCode = s.iBarCode AND si.rn = 1
LEFT JOIN dbo.Zinynas_busenos pb     ON pb.BusenosID = si.iStateBefore
WHERE us.FilialoID <> 7
  AND (@includeCancelled = 1 OR u.Sugadintas = 0)
  AND (@search IS NULL
       OR c.UzsakovoPavadinimas LIKE N'%' + @search + N'%'
       OR us.UzsakymoSerijaPav + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(16)) + N'-' + u.UzsakymoGrupe LIKE N'%' + @search + N'%')
ORDER BY si.dtStopped DESC, OrderNo;
