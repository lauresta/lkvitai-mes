/* 02 – Material / colour popularity by product type, for products ordered in [@from, @to)
   Material = ParduotiGaminiai.Spalva -> TBD_Components (sCodeInternal = Lauresta code, sName = supplier/fabric name,
   category = TBD_ComponentCategories). Type = Zinynas_tipai.TipoPavadinimas, category = Zinynas_rusys.RusiesPavadinimas.
   m² = width(mm) * height(mm) / 1e6 * quantity (raw size, no min-area pricing rules). */

SELECT TOP (@top)
        c.sCodeInternal                         AS MaterialCode,
        c.sName                                 AS MaterialName,
        cc.sName                                AS MaterialCategory,
        r.RusiesPavadinimas                     AS ProductCategory,
        t.TipoPavadinimas                       AS ProductType,
        COUNT(DISTINCT pg.UzsakymoID)           AS Orders,
        SUM(pg.Kiekis)                          AS Pieces,
        CAST(SUM(CAST(pg.Plotis AS decimal(18,3)) * pg.Aukstis * pg.Kiekis) / 1000000 AS decimal(18,2)) AS SquareMeters
FROM dbo.Uzsakymai u
JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
JOIN dbo.ParduotiGaminiai pg        ON pg.UzsakymoID = u.UzsakymasID
JOIN dbo.Zinynas_tipai t            ON t.TipoID = pg.ParduotoGaminioTipoID
JOIN dbo.Zinynas_rusys r            ON r.RusiesID = t.Rusis
JOIN dbo.TBD_Components c           ON c.iComponentID = pg.Spalva
LEFT JOIN dbo.TBD_ComponentCategories cc ON cc.iComponentCategoryID = c.iComponentCategoryID
WHERE u.UzsakymoData >= @from AND u.UzsakymoData < @to     -- index IX_Uzsakymai_UzsakymoData_DESC
  AND u.Sugadintas = 0
  AND us.FilialoID <> 7
  AND t.Gamininamas = 1                                    -- manufactured products only
  AND r.RusiesID NOT IN (1172751774, 985019188, 300967316)  -- Priedai, Be gaminių, Paslaugos (colour meaningless)
  -- RSR816 "BAMBOO CLASS-L" is the app's default colour for products without a real colour
  -- (frames, nets, Pliss-Langas...); keep it only for ZEBRA rollers where it is a real fabric.
  AND NOT (c.sCodeInternal = N'RSR816' AND r.RusiesID <> 1172751784)
  AND (@typeSearch IS NULL OR t.TipoPavadinimas LIKE N'%' + @typeSearch + N'%' OR r.RusiesPavadinimas LIKE N'%' + @typeSearch + N'%')
  AND (@materialSearch IS NULL OR c.sCodeInternal LIKE N'%' + @materialSearch + N'%' OR c.sName LIKE N'%' + @materialSearch + N'%')
GROUP BY c.sCodeInternal, c.sName, cc.sName, r.RusiesPavadinimas, t.TipoPavadinimas
ORDER BY Pieces DESC, SquareMeters DESC;
