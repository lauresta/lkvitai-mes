-- Sales-owned read scoped by the impersonated legacy SQL login. Legacy objects remain unchanged.
-- Portal "Search everything" (#375): customer names for the Customers group.
--   * only customers that have an order in a branch the caller may see (dbo.Loc_leidimas());
--   * customers hidden in the application are skipped, as in the order-entry catalogue;
--   * an order closed at the customer's request (privacy notice) does not make its customer
--     findable: a customer that has only such orders is not returned.
-- Name matching is the same LIKE as the orders list uses, so opening the hit with ?search=
-- shows that customer's orders.
SET NOCOUNT ON;

SELECT TOP (@Top) x.Customer
FROM (
    SELECT DISTINCT LTRIM(RTRIM(c.UzsakovoPavadinimas)) AS Customer
    FROM dbo.Zinynas_uzsakovai c
    WHERE ISNULL(c.bHideInApplicationFlag, 0) = 0
      AND c.UzsakovoPavadinimas LIKE N'%' + @Search + N'%'
      AND EXISTS (
            SELECT 1
            FROM dbo.Uzsakymai u
            INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
            WHERE u.UzsakovoID = c.UzsakovoID
              AND EXISTS (SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS = us.FilialoID)
              AND ISNULL(LTRIM(RTRIM(u.sNotes)), N'') <> N'Kliento prasymu anuliuotas'
          )
) x
WHERE x.Customer <> N''
ORDER BY CASE WHEN x.Customer LIKE @Search + N'%' THEN 0 ELSE 1 END, x.Customer;
