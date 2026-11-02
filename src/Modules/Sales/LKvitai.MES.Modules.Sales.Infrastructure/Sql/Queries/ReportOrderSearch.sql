/* 07 – Order search by period / product type / material / current order state, oldest first
   Order state = Uzsakymai.StateID -> Zinynas_busenos.Busena (order-level state maintained by the app;
   "SKIRTINGAI" = products of the order are in different states).
   Type filter matches Zinynas_tipai.TipoPavadinimas or Zinynas_rusys.RusiesPavadinimas of any order line;
   material filter matches TBD_Components.sCodeInternal / sName of any order line. */

SELECT TOP (@top)
        us.UzsakymoSerijaPav + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(16)) + N'-' + u.UzsakymoGrupe AS OrderNo,
        u.UzsakymoData                                             AS OrderDate,
        c.UzsakovoPavadinimas                                      AS Customer,
        f.FilialoPavadinimas                                       AS Branch,
        b.Busena                                                   AS OrderState,
        ISNULL(u.UzsakymoVerteSuNuolaida, 0)                       AS Price,
        ISNULL(u.Paid, 0)                                          AS Paid,
        ISNULL(u.UzsakymoVerteSuNuolaida, 0) - ISNULL(u.Paid, 0)   AS Debt,
        ml.MatchingLines,
        ml.MatchingPieces,
        u.Sugadintas                                               AS IsCancelled
FROM dbo.Uzsakymai u
JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
JOIN dbo.Zinynas_filialai f         ON f.FilialoID = us.FilialoID
JOIN dbo.Zinynas_uzsakovai c        ON c.UzsakovoID = u.UzsakovoID
JOIN dbo.Zinynas_busenos b          ON b.BusenosID = u.StateID
CROSS APPLY (SELECT COUNT(*) AS MatchingLines, SUM(pg.Kiekis) AS MatchingPieces
             FROM dbo.ParduotiGaminiai pg
             JOIN dbo.Zinynas_tipai t       ON t.TipoID = pg.ParduotoGaminioTipoID
             JOIN dbo.Zinynas_rusys r       ON r.RusiesID = t.Rusis
             LEFT JOIN dbo.TBD_Components k ON k.iComponentID = pg.Spalva
             WHERE pg.UzsakymoID = u.UzsakymasID
               AND (@typeSearch IS NULL OR t.TipoPavadinimas LIKE N'%' + @typeSearch + N'%' OR r.RusiesPavadinimas LIKE N'%' + @typeSearch + N'%')
               AND (@materialSearch IS NULL OR k.sCodeInternal LIKE N'%' + @materialSearch + N'%' OR k.sName LIKE N'%' + @materialSearch + N'%')
            ) ml
WHERE u.UzsakymoData >= @from AND u.UzsakymoData < @to          -- seek on IX_Uzsakymai_UzsakymoData_DESC
  AND us.FilialoID <> 7
  AND (@includeCancelled = 1 OR u.Sugadintas = 0)
  AND (@branch IS NULL OR us.FilialoID IN (SELECT FilialoID FROM dbo.Zinynas_filialai WHERE FilialoPavadinimas LIKE N'%' + @branch + N'%'))
  AND (@state IS NULL OR b.Busena = @state)
  AND (ml.MatchingLines > 0 OR (@typeSearch IS NULL AND @materialSearch IS NULL))
ORDER BY u.UzsakymoData ASC, u.UzsakymasID ASC;
