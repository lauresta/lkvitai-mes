SELECT TOP (5) t.TipoPavadinimas AS TypeName, m.Matas AS Unit,
       MIN(l.mPrice) AS Price, CASE WHEN MIN(l.mPrice) <> MAX(l.mPrice) THEN 1 ELSE 0 END AS IsFrom
FROM dbo.TBD_Components AS c
JOIN dbo.TBD_ComponentsPriceGroupRows AS cgr ON cgr.iComponentID = c.iComponentID
JOIN dbo.TCR_TypeColourPriceGroup AS g ON g.iComponentsPriceGroupID = cgr.iComponentsPriceGroupID
JOIN dbo.DIC_PriceList AS l ON l.iPriceGroupID = g.sPriceGroupID
JOIN dbo.TCR_PriceGroupPeriods AS p ON p.iPeriodID = l.iPeriodID
JOIN dbo.Zinynas_tipai AS t ON t.TipoID = g.iTypeID
LEFT JOIN dbo.Zinynas_matai AS m ON m.MatoID = t.KainosMatas
WHERE c.sCodeInternal = @Code AND p.dtValidFrom <= @Today AND p.dtValidUntil >= @Today
GROUP BY t.TipoPavadinimas, m.Matas
ORDER BY Price;
