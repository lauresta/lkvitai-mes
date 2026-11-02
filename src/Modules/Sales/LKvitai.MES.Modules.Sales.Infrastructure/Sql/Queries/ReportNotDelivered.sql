/* 05 – Orders made but not handed out to customer, and/or with installation not recorded
   Product level (manufactured types only: Zinynas_tipai.Gamininamas=1, excl. categories Be gaminių/Paslaugos/Priedai):
     made = BusenosID in (Pagamintas 1173102374, Išsiųstas į filialą 1173102376, Išduotas klientui 2).
   MadeDate = latest movement of any order product into Pagamintas / Išsiųstas (same as legacy
     dbo.SelectNotFinishedOrders used by sproc_EmailAboutSeniNeisduotiUzsakymai: >20 days, order state
     Pagamintas/Išsiųstas, branches 1-6).
   Installation: an order needs installation when it has a "Montavimas" service line (type -1551576315).
     There is NO completion flag. The installer (TCR_OrdersDutiesEmployees, iDutyID=4 Montuotojas) is entered
     with dtDate = installation date, practically only after/at handover; sp_MarkOrderAsFinished /
     selOrderMountingMinDate treat MIN(dtDate) of that row as the mounting date. dtAccuiranceDate is the salary
     accrual date (set when paid + handed out), not installation. So "installation pending" = Montavimas line
     exists AND no installer row with dtDate <= today. */
DECLARE @now datetime = GETDATE();

WITH cand AS (   -- live orders that have at least one made-but-not-handed product OR a Montavimas line
    SELECT u.UzsakymasID
    FROM dbo.Uzsakymai u
    JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
    WHERE u.Sugadintas = 0 AND us.FilialoID <> 7
      AND (@maxOrderAgeDays IS NULL OR u.UzsakymoData >= DATEADD(day, -@maxOrderAgeDays, @now))
      AND (EXISTS (SELECT 1 FROM dbo.ParduotiGaminiai p WHERE p.UzsakymoID = u.UzsakymasID AND p.BusenosID IN (1173102374, 1173102376))
           OR (@includeInstall = 1 AND EXISTS (SELECT 1 FROM dbo.ParduotiGaminiai p WHERE p.UzsakymoID = u.UzsakymasID AND p.ParduotoGaminioTipoID = -1551576315)
               AND NOT EXISTS (SELECT 1 FROM dbo.TCR_OrdersDutiesEmployees oe WHERE oe.iOrderID = u.UzsakymasID AND oe.iDutyID = 4 AND oe.dtDate <= @now)))
),
prod AS (
    SELECT pg.UzsakymoID,
           COUNT(*)                                                                         AS Lines,
           SUM(pg.Kiekis)                                                                   AS Pieces,
           SUM(CASE WHEN pg.BusenosID IN (1173102374, 1173102376, 2) THEN 1 ELSE 0 END)     AS MadeLines,
           SUM(CASE WHEN pg.BusenosID = 2 THEN 1 ELSE 0 END)                                AS HandedLines
    FROM cand c
    JOIN dbo.ParduotiGaminiai pg ON pg.UzsakymoID = c.UzsakymasID
    JOIN dbo.Zinynas_tipai t     ON t.TipoID = pg.ParduotoGaminioTipoID
    WHERE t.Gamininamas = 1 AND t.Rusis NOT IN (985019188, 300967316, 1172751774)
    GROUP BY pg.UzsakymoID
),
made AS (
    SELECT pg.UzsakymoID, MAX(d.dtDateTime) AS dtMade
    FROM cand c
    JOIN dbo.ParduotiGaminiai pg                    ON pg.UzsakymoID = c.UzsakymasID
    JOIN dbo.TAB_ProductionOutputDocumentDetails dd ON dd.iBarCode = pg.iBarCode
    JOIN dbo.TAB_ProductionOutputDocuments d        ON d.iProductionOutputDocumentID = dd.iProductionOutputDocumentID
    WHERE d.iAssignedStatusID IN (1173102374, 1173102376)
    GROUP BY pg.UzsakymoID
),
inst AS (
    SELECT c.UzsakymasID,
           MAX(CASE WHEN pg.ParduotoGaminioTipoID = -1551576315 THEN 1 ELSE 0 END) AS NeedsInstall
    FROM cand c JOIN dbo.ParduotiGaminiai pg ON pg.UzsakymoID = c.UzsakymasID
    GROUP BY c.UzsakymasID
),
mnt AS (
    SELECT oe.iOrderID, MIN(oe.dtDate) AS dtInstall
    FROM cand c JOIN dbo.TCR_OrdersDutiesEmployees oe ON oe.iOrderID = c.UzsakymasID AND oe.iDutyID = 4
    GROUP BY oe.iOrderID
),
res AS (
    SELECT u.UzsakymasID, u.UzsakymoSerija, u.UzsakymoNumeris, u.UzsakymoGrupe, u.UzsakymoData, u.StateID, u.UzsakovoID,
           u.dtFirstReleasementDate, u.UzsakymoVerteSuNuolaida, u.Paid,
           p.Lines, p.Pieces, p.MadeLines, p.HandedLines,
           COALESCE(m.dtMade, u.dtFirstReleasementDate) AS dtMade,
           i.NeedsInstall, mn.dtInstall,
           CASE WHEN p.MadeLines > p.HandedLines
                 AND (@allMade = 0 OR p.MadeLines = p.Lines) THEN 1 ELSE 0 END                      AS NotHandedOut,
           CASE WHEN i.NeedsInstall = 1 AND (mn.dtInstall IS NULL OR mn.dtInstall > @now)
                 AND p.MadeLines = p.Lines THEN 1 ELSE 0 END                                         AS InstallPending
    FROM cand c
    JOIN dbo.Uzsakymai u ON u.UzsakymasID = c.UzsakymasID
    JOIN prod p          ON p.UzsakymoID = c.UzsakymasID
    JOIN inst i          ON i.UzsakymasID = c.UzsakymasID
    LEFT JOIN made m     ON m.UzsakymoID = c.UzsakymasID
    LEFT JOIN mnt mn     ON mn.iOrderID = c.UzsakymasID
)
SELECT  us.UzsakymoSerijaPav + N'-' + CAST(r.UzsakymoNumeris AS nvarchar(16)) + N'-' + r.UzsakymoGrupe AS OrderNo,
        r.UzsakymoData                          AS OrderDate,
        b.Busena                                AS OrderState,
        cu.UzsakovoPavadinimas                  AS Customer,
        f.FilialoPavadinimas                    AS Branch,
        r.Lines, r.Pieces, r.MadeLines, r.HandedLines,
        r.dtMade                                AS MadeDate,
        DATEDIFF(day, r.dtMade, @now)           AS DaysSinceMade,
        r.dtFirstReleasementDate                AS HandedOutDate,
        r.NotHandedOut,
        r.NeedsInstall,
        r.dtInstall                             AS InstallerDate,
        r.InstallPending,
        ISNULL(r.UzsakymoVerteSuNuolaida, 0) - ISNULL(r.Paid, 0) AS Debt
FROM res r
JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = r.UzsakymoSerija
JOIN dbo.Zinynas_filialai f         ON f.FilialoID = us.FilialoID
JOIN dbo.Zinynas_uzsakovai cu       ON cu.UzsakovoID = r.UzsakovoID
JOIN dbo.Zinynas_busenos b          ON b.BusenosID = r.StateID
WHERE (r.NotHandedOut = 1 OR (@includeInstall = 1 AND r.InstallPending = 1))
  AND DATEDIFF(day, r.dtMade, @now) >= @days
ORDER BY r.dtMade ASC;
