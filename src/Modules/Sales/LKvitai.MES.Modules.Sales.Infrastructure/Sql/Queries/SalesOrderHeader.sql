-- Header authorization follows Loc_leidimas for the impersonated SQL login.
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

SELECT TOP (100) PERCENT u.UzsakymasID AS Id, us.UzsakymoSerijaPav + '-' + CAST(u.UzsakymoNumeris AS nvarchar(10)) + '-' + u.UzsakymoGrupe AS Number, ISNULL(u.UzsakymoData, '1999-01-01') AS Date, 
                  ISNULL(u.UzsakymoVerteSuNuolaida, 99999) AS Price, ISNULL(u.UzsakymoVerteSuNuolaida, 99999) - ISNULL(u.Paid, 0) AS Debt, c.UzsakovoPavadinimas AS Customer, CASE WHEN ISNULL(u.Sugadintas,0)<>0 THEN N'Anuliuotas' ELSE s.Busena END AS Status, o.VietaPavadinimas AS Store, 
                  u.sNotes AS OrderNote, u.Sugadintas AS IsCancelled, CASE WHEN u.StateID = 0 AND u.Sugadintas = 0 THEN 'zero' WHEN u.StateID = 4 AND u.PriorityID = 1 AND u.Sugadintas = 0 THEN 'ent' WHEN u.StateID = 4 AND u.PriorityID = 2 AND 
                  u.Sugadintas = 0 THEN 'ent' WHEN u.StateID = 4 AND u.PriorityID = 3 AND u.Sugadintas = 0 THEN 'ent' WHEN u.StateID = 4 AND u.PriorityID = 4 AND u.Sugadintas = 0 THEN 'ent' WHEN u.StateID = 1173102375 AND u.PriorityID = 1 AND 
                  u.Sugadintas = 0 THEN 'con' WHEN u.StateID = 1173102375 AND u.PriorityID = 2 AND u.Sugadintas = 0 THEN 'con' WHEN u.StateID = 1173102375 AND u.PriorityID = 3 AND u.Sugadintas = 0 THEN 'con' WHEN u.StateID = 1173102375 AND 
                  u.PriorityID = 4 AND u.Sugadintas = 0 THEN 'con' WHEN u.StateID = 1 AND u.Sugadintas = 0 THEN 'proc' WHEN u.StateID = 7 AND u.Sugadintas = 0 THEN 'stopped' WHEN u.StateID = 1173102374 AND 
                  u.Sugadintas = 0 THEN 'made' WHEN u.StateID = 1173102376 AND u.Sugadintas = 0 THEN 'sent' WHEN u.StateID = 2 AND u.Sugadintas = 0 THEN 'released' WHEN u.StateID = 1173102377 AND 
                  u.Sugadintas = 0 THEN 'zero' WHEN u.Sugadintas <> 0 THEN 'del' ELSE 'ent' END AS sImageKey, ISNULL(CASE WHEN streets.iID = 4558 THEN oa.sNotes ELSE CASE WHEN streets.sName = '' THEN '---' WHEN streets.sName IS NULL 
                  THEN '---' ELSE cities.sName + ', ' + streets.sName + ', ' + CASE WHEN oa.sBuildingNo = '' THEN 'N/A' WHEN oa.sBuildingNo IS NULL 
                  THEN 'N/A' ELSE oa.sBuildingNo END + ' - ' + CASE WHEN oa.sRoomNo = '' THEN 'N/A' WHEN oa.sRoomNo IS NULL THEN 'N/A' ELSE oa.sRoomNo END + CASE WHEN oa.sPhoneNo = '' THEN '' WHEN oa.sPhoneNo IS NULL 
                  THEN '' ELSE ' / ' + oa.sPhoneNo END END END, 'N/A') AS Address, u.sUserNameInsert, u.dtDateInsert, 
				  u.UzsakymoVerteSuNuolaida AS DefinedAmount, 
				  ISNULL(u.mOrderAccessoriesCost, 0) + ISNULL(u.mOrderNONDiscountableBlindsCost, 0) + ISNULL(u.mOrderDiscountableBlindsCost, 0) AS CalculatedAmount, 
				  u.Nuolaida AS Discount, 
				  CAST(ROUND(((ISNULL(u.mOrderDiscountableBlindsCost, 0) + ISNULL(u.mOrderAccessoriesCost, 0)) / 100) * (100 - ISNULL(u.Nuolaida, 0)) + ISNULL(u.mOrderNONDiscountableBlindsCost, 0), 2) AS money) AS FinalCost, 
				  u.Montavimo_data, u.Paid, ISNULL(u.UzsakymoVerteSuNuolaida, 0) - ISNULL(u.Paid, 0) AS Debt
FROM     dbo.Zinynas_vieta AS o INNER JOIN
                  dbo.Uzsakymai AS u INNER JOIN
                  dbo.Zinynas_UzsakymoSerijos AS us ON u.UzsakymoSerija = us.UzsakymoSerijaID INNER JOIN
                  dbo.Zinynas_uzsakovai AS c ON u.UzsakovoID = c.UzsakovoID INNER JOIN
                  dbo.Zinynas_busenos AS s ON u.StateID = s.BusenosID ON o.VietaID = u.VietaID LEFT OUTER JOIN
                  dbo.DIC_Addresses AS streets INNER JOIN
                  dbo.TCR_OrderAddresses AS oa ON streets.iID = oa.iStreetID INNER JOIN
                  dbo.DIC_Addresses AS cities ON streets.iParentID = cities.iID ON u.UzsakymasID = oa.iOrderID
WHERE  (u.UzsakymasID = @OrderId)
AND EXISTS (SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS = us.FilialoID)
ORDER BY Date DESC
