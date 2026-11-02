/* 06 – Customer debt summary (top debtors, or one customer via @search)
   Debt per order = ISNULL(UzsakymoVerteSuNuolaida,0) - ISNULL(Paid,0), only orders with debt > 0, not cancelled.
   NOTE: retail sales are booked on one generic customer per branch ("Klaipėda - mažmena", "Vilnius - mažmena"...),
   so per-customer debt is meaningful mainly for wholesale customers (bIsWholesalerFlag=1).
   AgnumDebt = Zinynas_uzsakovai.Agnum_SKOLA (balance imported from Agnum accounting) for comparison. */
DECLARE @now datetime = GETDATE();

SELECT TOP (@top)
        c.UzsakovoPavadinimas                                          AS Customer,
        CASE WHEN c.bIsWholesalerFlag <> 0 THEN 1 ELSE 0 END           AS IsWholesale,
        COUNT(*)                                                       AS OrdersInDebt,
        SUM(x.Debt)                                                    AS TotalDebt,
        SUM(CASE WHEN u.StateID = 2 THEN x.Debt ELSE 0 END)            AS DebtHandedOut,
        MIN(u.UzsakymoData)                                            AS OldestDebtOrderDate,
        DATEDIFF(day, MIN(u.UzsakymoData), @now)                       AS OldestDebtDays,
        MIN(CASE WHEN u.StateID = 2 THEN u.dtFirstReleasementDate END) AS OldestHandedOutDate,
        MAX(c.Agnum_SKOLA)                                             AS AgnumDebt,
        MAX(c.SkolosLimitas)                                           AS CreditLimit
FROM dbo.Uzsakymai u
JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID = u.UzsakymoSerija
JOIN dbo.Zinynas_uzsakovai c        ON c.UzsakovoID = u.UzsakovoID
CROSS APPLY (SELECT ISNULL(u.UzsakymoVerteSuNuolaida, 0) - ISNULL(u.Paid, 0) AS Debt) x
WHERE u.Sugadintas = 0
  AND us.FilialoID <> 7
  AND x.Debt > 0.009
  AND (@onlyHandedOut = 0 OR u.StateID = 2)
  AND (@search IS NULL OR c.UzsakovoPavadinimas LIKE N'%' + @search + N'%')
GROUP BY c.UzsakovoID, c.UzsakovoPavadinimas, c.bIsWholesalerFlag
ORDER BY TotalDebt DESC;
