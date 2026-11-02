/* 04 – Long unpaid orders (outstanding debt)
   Price = Uzsakymai.UzsakymoVerteSuNuolaida (final price incl. discount), Paid = Uzsakymai.Paid
   (verified = SUM of dbo.Mokejimai + MTTools.dbo.LKUZAM payments for 25,247 of 25,249 live orders).
   Debt = Price - Paid (same formula as weblb_Order / legacy views).
   LegacyOverdue reproduces dbo.selectReleasedButUnpaidOrders (used by sproc_EmailAboutSeniIsduotiNeapmoketiUzsakymai):
     order state "Išduotas klientui", debt > 0, handed out > 30 days ago for wholesale customers (bIsWholesalerFlag<>0)
     or > 15 days for retail, series AL / RY excluded. (Legacy uses LAST handover movement; here the stored
     Uzsakymai.dtFirstReleasementDate is used – cheaper, differs only for re-issued orders.) */
DECLARE @now datetime = GETDATE();

WITH debt AS (
    SELECT u.UzsakymasID, u.UzsakymoSerija, u.UzsakymoNumeris, u.UzsakymoGrupe, u.UzsakymoData, u.StateID, u.UzsakovoID,
           u.dtFirstReleasementDate,
           ISNULL(u.UzsakymoVerteSuNuolaida, 0)                       AS Price,
           ISNULL(u.Paid, 0)                                          AS Paid,
           ISNULL(u.UzsakymoVerteSuNuolaida, 0) - ISNULL(u.Paid, 0)   AS Debt
    FROM dbo.Uzsakymai u
    WHERE u.Sugadintas = 0
      AND ISNULL(u.UzsakymoVerteSuNuolaida, 0) - ISNULL(u.Paid, 0) >= @minDebt
      AND u.UzsakymoData < DATEADD(day, -@minDays, @now)
      AND (@onlyHandedOut = 0 OR u.StateID = 2)
)
SELECT  us.UzsakymoSerijaPav + N'-' + CAST(d.UzsakymoNumeris AS nvarchar(16)) + N'-' + d.UzsakymoGrupe AS OrderNo,
        d.UzsakymoData                                AS OrderDate,
        b.Busena                                      AS OrderState,
        c.UzsakovoPavadinimas                         AS Customer,
        CASE WHEN c.bIsWholesalerFlag <> 0 THEN 1 ELSE 0 END AS IsWholesale,
        f.FilialoPavadinimas                          AS Branch,
        d.Price, d.Paid, d.Debt,
        DATEDIFF(day, d.UzsakymoData, @now)           AS DaysSinceOrder,
        d.dtFirstReleasementDate                      AS HandedOutDate,
        DATEDIFF(day, d.dtFirstReleasementDate, @now) AS DaysSinceHandedOut,
        lp.dtLastPayment                              AS LastPaymentDate,
        CASE WHEN d.StateID = 2
              AND d.UzsakymoSerija NOT IN (-1892692154, 483921819)          -- AL, RY
              AND DATEDIFF(day, d.dtFirstReleasementDate, @now) > CASE WHEN c.bIsWholesalerFlag <> 0 THEN 30 ELSE 15 END
             THEN 1 ELSE 0 END                        AS LegacyOverdue
FROM debt d
JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = d.UzsakymoSerija
JOIN dbo.Zinynas_filialai f         ON f.FilialoID = us.FilialoID
JOIN dbo.Zinynas_uzsakovai c        ON c.UzsakovoID = d.UzsakovoID
JOIN dbo.Zinynas_busenos b          ON b.BusenosID = d.StateID
OUTER APPLY (SELECT MAX(p.Data) AS dtLastPayment
             FROM (SELECT Data FROM dbo.Mokejimai WHERE UzsakymasID = d.UzsakymasID
                   UNION ALL
                   SELECT Data FROM MTTools.dbo.LKUZAM WHERE UzsakymasID = d.UzsakymasID) p) lp
WHERE us.FilialoID <> 7
  AND (@search IS NULL
       OR c.UzsakovoPavadinimas LIKE N'%' + @search + N'%'
       OR us.UzsakymoSerijaPav + N'-' + CAST(d.UzsakymoNumeris AS nvarchar(16)) + N'-' + d.UzsakymoGrupe LIKE N'%' + @search + N'%')
  AND (@onlyLegacyOverdue = 0 OR (d.StateID = 2 AND d.UzsakymoSerija NOT IN (-1892692154, 483921819)
        AND DATEDIFF(day, d.dtFirstReleasementDate, @now) > CASE WHEN c.bIsWholesalerFlag <> 0 THEN 30 ELSE 15 END))
ORDER BY d.UzsakymoData ASC;   -- oldest first; alternatively ORDER BY Debt DESC
