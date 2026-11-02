-- Sales-owned projection scoped by the impersonated legacy SQL login. Legacy procedures remain unchanged.
    SET NOCOUNT ON;

    -- Parameter guards
    IF @Page IS NULL OR @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 100;
    IF @PageSize > 500 SET @PageSize = 500;

    IF @Search IS NOT NULL AND LTRIM(RTRIM(@Search)) = N'' SET @Search = NULL;
    IF @Status IS NOT NULL AND LTRIM(RTRIM(@Status)) = N'' SET @Status = NULL;
    IF @Store  IS NOT NULL AND LTRIM(RTRIM(@Store))  = N'' SET @Store  = NULL;

    DECLARE @Skip INT = (@Page - 1) * @PageSize;

    /* -----------------------------------------------------------------
       Step 1 — page-id resolution.

       The single hot path here is the unfiltered "first 100 most-recent
       orders" query that runs on every page change. We keep Step 1 as
       narrow as possible (only the joins required for filtering and
       sorting) so the COUNT(*) OVER () + ROW_NUMBER() pass walks a
       slim row set. The expensive Address joins (3 tables) are deferred
       to Step 2, which only joins to the 100 rows of the chosen page.

       Address text is matched in @Search via a correlated EXISTS so the
       address rows are still hit selectively without inflating Step 1.
       Sort tuple (UzsakymoData DESC, UzsakymasID DESC) matches
       IX_Uzsakymai_UzsakymoData_DESC, enabling an index range scan.
       ----------------------------------------------------------------- */
    ;WITH BaseIds AS (
        SELECT
            u.UzsakymasID                                              AS Id,
            ISNULL(u.UzsakymoData, '1999-01-01')                       AS [Date],
            us.UzsakymoSerijaPav + '-' +
                CAST(u.UzsakymoNumeris AS nvarchar(10)) + '-' +
                u.UzsakymoGrupe                                        AS Number,
            c.UzsakovoPavadinimas                                      AS Customer,
            CASE WHEN ISNULL(u.Sugadintas,0)<>0 THEN N'Anuliuotas'
                 ELSE LTRIM(RTRIM(s.Busena)) END                        AS StatusTrim,
            LTRIM(RTRIM(o.VietaPavadinimas))                           AS StoreTrim,
            ISNULL(u.UzsakymoVerteSuNuolaida, 99999) - ISNULL(u.Paid, 0) AS Debt
        FROM dbo.Uzsakymai u
        INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
        INNER JOIN dbo.Zinynas_uzsakovai      c  ON u.UzsakovoID     = c.UzsakovoID
        INNER JOIN dbo.Zinynas_busenos        s  ON u.StateID        = s.BusenosID
        INNER JOIN dbo.Zinynas_vieta          o  ON o.VietaID        = u.VietaID
        
        WHERE
            EXISTS (SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS = us.FilialoID)
            AND (@ExactOrderId IS NULL OR u.UzsakymasID = @ExactOrderId)
            AND (@DateFrom IS NULL OR u.UzsakymoData >= @DateFrom)
            AND (@DateTo   IS NULL OR u.UzsakymoData <  DATEADD(day, 1, @DateTo))
    ),
    Searched AS (
        SELECT b.Id, b.[Date]
        FROM BaseIds b
        WHERE
            (@Status  IS NULL OR b.StatusTrim = @Status)
            AND (@Store IS NULL OR b.StoreTrim = @Store)
            AND (@HasDebt IS NULL OR (CASE WHEN b.Debt > 0 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END) = @HasDebt)
            AND (@Search IS NULL
                 OR b.Number   LIKE N'%' + @Search + N'%'
                 OR b.Customer LIKE N'%' + @Search + N'%'
                 -- Address LIKE: only joins the address tables for orders
                 -- that survive the cheaper predicates above.
                 OR EXISTS (
                        SELECT 1
                        FROM dbo.TCR_OrderAddresses oa
                        LEFT JOIN dbo.DIC_Addresses streets ON streets.iID = oa.iStreetID
                        LEFT JOIN dbo.DIC_Addresses cities  ON streets.iParentID = cities.iID
                        WHERE oa.iOrderID = b.Id
                          AND (
                                streets.sName  LIKE N'%' + @Search + N'%'
                             OR cities.sName   LIKE N'%' + @Search + N'%'
                             OR oa.sBuildingNo LIKE N'%' + @Search + N'%'
                             OR oa.sPhoneNo    LIKE N'%' + @Search + N'%'
                             OR oa.sNotes      LIKE N'%' + @Search + N'%'
                          )
                    )
                 OR EXISTS (
                        SELECT 1
                        FROM dbo.ParduotiGaminiai pg
                        INNER JOIN dbo.Zinynas_tipai t  ON t.TipoID    = pg.ParduotoGaminioTipoID
                        INNER JOIN dbo.Zinynas_rusys  r  ON r.RusiesID  = t.Rusis
                        LEFT  JOIN dbo.TBD_Components c2 ON pg.Spalva   = c2.iComponentID
                        WHERE pg.UzsakymoID = b.Id
                          AND (
                                r.RusiesPavadinimas LIKE N'%' + @Search + N'%'
                             OR t.TipoPavadinimas   LIKE N'%' + @Search + N'%'
                             OR c2.sCodeInternal    LIKE N'%' + @Search + N'%'
                             OR pg.Pastabos         LIKE N'%' + @Search + N'%'
                          )
                    )
            )
    ),
    NumberedIds AS (
        SELECT
            s.Id,
            s.[Date],
            ROW_NUMBER() OVER (ORDER BY s.[Date] DESC, s.Id DESC) AS RowNum,
            COUNT(*)     OVER ()                                   AS TotalRows
        FROM Searched s
    )
    SELECT Id, RowNum, TotalRows
    INTO #PageIds
    FROM NumberedIds
    WHERE RowNum > @Skip AND RowNum <= @Skip + @PageSize;

    /* -----------------------------------------------------------------
       Step 2 — full projection.

       Only the @PageSize rows from #PageIds are joined to the address
       tables and re-projected with the legacy column shape. TotalRows
       is duplicated on every row so the C# caller can read it once
       (from any row) without a second result set.
       ----------------------------------------------------------------- */
    SELECT
        u.UzsakymasID                                                                     AS Id,
        us.UzsakymoSerijaPav + '-' +
            CAST(u.UzsakymoNumeris AS nvarchar(10)) + '-' +
            u.UzsakymoGrupe                                                               AS Number,
        ISNULL(u.UzsakymoData, '1999-01-01')                                              AS [Date],
        ISNULL(u.UzsakymoVerteSuNuolaida, 99999)                                          AS Price,
        ISNULL(u.UzsakymoVerteSuNuolaida, 99999) - ISNULL(u.Paid, 0)                      AS Debt,
        c.UzsakovoPavadinimas                                                             AS Customer,
        CASE WHEN ISNULL(u.Sugadintas,0)<>0 THEN N'Anuliuotas' ELSE s.Busena END               AS [Status],
        o.VietaPavadinimas                                                                AS Store,
        u.Sugadintas                                                                      AS IsCancelled,
        CASE
            WHEN u.StateID = 0          AND u.Sugadintas = 0                 THEN 'zero'
            WHEN u.StateID = 4          AND u.PriorityID IN (1,2,3,4) AND u.Sugadintas = 0 THEN 'ent'
            WHEN u.StateID = 1173102375 AND u.PriorityID IN (1,2,3,4) AND u.Sugadintas = 0 THEN 'con'
            WHEN u.StateID = 1          AND u.Sugadintas = 0                 THEN 'proc'
            WHEN u.StateID = 7          AND u.Sugadintas = 0                 THEN 'stopped'
            WHEN u.StateID = 1173102374 AND u.Sugadintas = 0                 THEN 'made'
            WHEN u.StateID = 1173102376 AND u.Sugadintas = 0                 THEN 'sent'
            WHEN u.StateID = 2          AND u.Sugadintas = 0                 THEN 'released'
            WHEN u.StateID = 1173102377 AND u.Sugadintas = 0                 THEN 'zero'
            WHEN u.Sugadintas <> 0                                            THEN 'del'
            ELSE 'ent'
        END                                                                               AS sImageKey,
        ISNULL(
            CASE
                WHEN streets.iID = 4558 THEN oa.sNotes
                ELSE CASE
                        WHEN streets.sName = '' OR streets.sName IS NULL THEN '---'
                        ELSE cities.sName + ', ' + streets.sName + ', ' +
                             CASE WHEN oa.sBuildingNo = '' OR oa.sBuildingNo IS NULL THEN 'N/A' ELSE oa.sBuildingNo END +
                             ' - ' +
                             CASE WHEN oa.sRoomNo = '' OR oa.sRoomNo IS NULL THEN 'N/A' ELSE oa.sRoomNo END +
                             CASE WHEN oa.sPhoneNo = '' OR oa.sPhoneNo IS NULL THEN '' ELSE ' / ' + oa.sPhoneNo END
                     END
            END, 'N/A'
        )                                                                                 AS Address,
        N''                                                                               AS ProductsSearch,
        p.TotalRows                                                                       AS TotalRows
    FROM #PageIds p
    INNER JOIN dbo.Uzsakymai                  u  ON u.UzsakymasID    = p.Id
    INNER JOIN dbo.Zinynas_UzsakymoSerijos    us ON u.UzsakymoSerija = us.UzsakymoSerijaID
    INNER JOIN dbo.Zinynas_uzsakovai          c  ON u.UzsakovoID     = c.UzsakovoID
    INNER JOIN dbo.Zinynas_busenos            s  ON u.StateID        = s.BusenosID
    INNER JOIN dbo.Zinynas_vieta              o  ON o.VietaID        = u.VietaID
    LEFT OUTER JOIN dbo.TCR_OrderAddresses    oa      ON u.UzsakymasID = oa.iOrderID
    LEFT OUTER JOIN dbo.DIC_Addresses         streets ON streets.iID  = oa.iStreetID
    LEFT OUTER JOIN dbo.DIC_Addresses         cities  ON streets.iParentID = cities.iID
    WHERE EXISTS (SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS = us.FilialoID)
    ORDER BY p.RowNum;

    DROP TABLE #PageIds;
