-- Read-only views for the Grafana dashboard «Įplaukų judėjimas» (datasource mssql-bi, login grafana_ro).
-- Database: LKVITAIDBV1SQL on lkvitai-mssql. Run as sa. Safe to re-run.
--
-- The logic mirrors src/Modules/Finance/LKvitai.MES.Modules.Finance.Api/Incomes/IncomesDataSource.cs
-- (PaymentsSql, OrdersSql). When that file changes, change these views too and re-check
-- one closed month against the Cikada page /finance/incomes.
--
-- Cikada itself does NOT read these views: it queries under each user's own SQL login.

IF SCHEMA_ID(N'bi') IS NULL EXEC(N'CREATE SCHEMA bi AUTHORIZATION dbo');
GO

CREATE OR ALTER VIEW bi.vIncomesPayments AS
SELECT p.Src + CAST(p.MokejimoID AS varchar(20))                    AS PaymentKey,
       CAST(p.Data AS date)                                         AS PayDate,
       p.dtDate                                                     AS EnteredAt,
       ISNULL(p.Suma, 0) * ISNULL(v.Kursas, 1)                      AS AmountEur,
       CASE p.MokeijimoTipasID WHEN 1 THEN 'PPK' WHEN 2 THEN 'BANK'
            WHEN 81423987 THEN 'KASA' WHEN 834019474 THEN 'EKA' END AS Method,
       ISNULL(NULLIF(LTRIM(RTRIM(f.sCode)), N''), N'F' + CAST(f.FilialoID AS nvarchar(10))) AS BranchCode,
       ISNULL(f.FilialoPavadinimas, N'')                            AS BranchName,
       ISNULL(us.UzsakymoSerijaPav, N'')                            AS Series,
       ISNULL(us.UzsakymoSerijaPav, N'') + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(20))
           + N'-' + ISNULL(u.UzsakymoGrupe, N'')                    AS OrderNo,
       p.Employee
FROM (
    SELECT 'L' AS Src, l.MokejimoID, l.UzsakymasID, l.Data, l.dtDate, l.Suma, l.MokeijimoTipasID,
           d.DarbuotojoPavadinimas AS Employee
    FROM MTTools.dbo.LKUZAM l
    INNER JOIN dbo.Zinynas_darbuotojai d ON l.EmployeeID = d.DarbuotojoID
    UNION ALL
    SELECT 'M', m.MokejimoID, m.UzsakymasID, m.Data, m.dtDate, m.Suma, m.MokeijimoTipasID,
           d.DarbuotojoPavadinimas
    FROM dbo.Mokejimai m
    LEFT JOIN dbo.Zinynas_darbuotojai d ON m.EmployeeID = d.DarbuotojoID
) p
INNER JOIN dbo.Uzsakymai u               ON p.UzsakymasID = u.UzsakymasID
INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
INNER JOIN dbo.Zinynas_filialai f        ON us.FilialoID = f.FilialoID
INNER JOIN dbo.Zinynas_valiutos v        ON f.iCurrencyID = v.ValiutosID
INNER JOIN dbo.Zinynas_mokejimotipai mt  ON p.MokeijimoTipasID = mt.MokejimoTipasID
WHERE ISNULL(f.sCode, N'') <> N'TES'
  AND p.MokeijimoTipasID IN (1, 2, 81423987, 834019474);
GO

CREATE OR ALTER VIEW bi.vIncomesOrders AS
SELECT u.UzsakymasID                                                AS OrderId,
       CAST(u.UzsakymoData AS date)                                 AS OrderDate,
       ISNULL(NULLIF(LTRIM(RTRIM(f.sCode)), N''), N'F' + CAST(f.FilialoID AS nvarchar(10))) AS BranchCode,
       ISNULL(f.FilialoPavadinimas, N'')                            AS BranchName,
       ISNULL(us.UzsakymoSerijaPav, N'')                            AS Series,
       ISNULL(us.UzsakymoSerijaPav, N'') + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(20))
           + N'-' + ISNULL(u.UzsakymoGrupe, N'')                    AS OrderNo,
       CAST(CASE WHEN u.sNotes = N'Kliento prasymu anuliuotas' THEN 1 ELSE 0 END AS bit) AS Cancelled,
       ISNULL(CASE WHEN u.sNotes = N'Kliento prasymu anuliuotas' THEN CAST(z.Verte AS decimal(19, 4))
                   ELSE u.UzsakymoVerteSuNuolaida END, 0) * ISNULL(v.Kursas, 1) AS ValueEur,
       ISNULL(u.Nuolaida, 0)                                        AS DiscountPct,
       k.DarbuotojoPavadinimas                                      AS Consultant,
       ISNULL(paid.Amount, 0) * ISNULL(v.Kursas, 1)                 AS PaidEur
FROM dbo.Uzsakymai u
INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
INNER JOIN dbo.Zinynas_filialai f        ON us.FilialoID = f.FilialoID
INNER JOIN dbo.Zinynas_valiutos v        ON f.iCurrencyID = v.ValiutosID
LEFT JOIN MTTools.dbo.LKUZA z            ON z.iOrderID = u.UzsakymasID
LEFT JOIN dbo.Zinynas_darbuotojai k      ON k.DarbuotojoID = u.KonsultantoID
OUTER APPLY (
    SELECT SUM(x.Suma) AS Amount FROM (
        SELECT m.Suma FROM dbo.Mokejimai m WHERE m.UzsakymasID = u.UzsakymasID
        UNION ALL
        SELECT ISNULL(l.Suma, 0) FROM MTTools.dbo.LKUZAM l WHERE l.UzsakymasID = u.UzsakymasID) x
) paid
WHERE (u.Sugadintas = 0 OR u.sNotes = N'Kliento prasymu anuliuotas')
  AND ISNULL(f.sCode, N'') <> N'TES';
GO

-- Login (created once by hand, password is not kept in git):
--   CREATE LOGIN grafana_ro WITH PASSWORD = N'...', DEFAULT_DATABASE = LKVITAIDBV1SQL;
--   LKVITAIDBV1SQL: CREATE USER grafana_ro FOR LOGIN grafana_ro; GRANT SELECT ON SCHEMA::bi TO grafana_ro;
--   MTTools:        CREATE USER grafana_ro FOR LOGIN grafana_ro; GRANT SELECT ON dbo.LKUZAM TO grafana_ro; GRANT SELECT ON dbo.LKUZA TO grafana_ro;
-- Cross-database ownership chaining is off on purpose, hence the two grants in MTTools.
