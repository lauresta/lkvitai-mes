-- Deferred by owner on 2026-11-03. DO NOT EXECUTE without new approval.
-- Proposed correction for the shared legacy Decolux fabric-consumption
-- formula. This changes master data used by both Access and Sales, for every
-- branch. Do not run as an automatic deployment migration.
-- Observed 2026-11-03: 1200 mm x 1 reads as 1200 m with the old formula;
-- neighbouring Roletai/Audinys types use (Width/1000)*Quantity (1.2 m).
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME()<>N'LKVITAIDBV1SQL'
        THROW 51000, 'Unexpected Sales database', 1;
    IF NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
        AND database_guid='31FC1889-3D3E-4422-A4E3-362899649FF5')
        THROW 51000, 'Unexpected Sales database identity', 1;
    IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.Zinynas_tipai') AND is_disabled=0)
        THROW 51000, 'Unreviewed type-table trigger', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Zinynas_tipai WITH(UPDLOCK,HOLDLOCK)
        WHERE Rusis=-270492797 AND Naudojamas=1
        AND ((TipoID=1170058243 AND TipoPavadinimas=N'Decolux')
             OR (TipoID=-593367416 AND TipoPavadinimas=N'Decolux laminuotas')))<>2
        THROW 51000, 'Decolux type rows differ from the reviewed baseline', 1;
    IF EXISTS(SELECT 1 FROM dbo.Zinynas_tipai
        WHERE TipoID IN(1170058243,-593367416)
        AND NPP12 NOT IN(N'Width*Quantity',N'(Width/1000)*Quantity'))
        THROW 51000, 'Decolux formula differs from the reviewed baseline', 1;
    IF EXISTS(SELECT 1 FROM dbo.ParduotiGaminiai
        WHERE ParduotoGaminioTipoID IN(1170058243,-593367416) AND PPP12 IS NOT NULL)
        THROW 51000, 'Stored Decolux consumption exists; review before changing formula', 1;
    UPDATE dbo.Zinynas_tipai SET NPP12=N'(Width/1000)*Quantity'
    WHERE TipoID IN(1170058243,-593367416) AND NPP12=N'Width*Quantity';
    DECLARE @changed int=@@ROWCOUNT;
    IF @changed NOT IN(0,2)
        THROW 51000, 'Unexpected partial Decolux update', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.Zinynas_tipai
        WHERE TipoID IN(1170058243,-593367416)
        AND NPP12=N'(Width/1000)*Quantity')<>2
        THROW 51000, 'Decolux formula verification failed', 1;
    COMMIT TRANSACTION;
    SELECT TipoID,TipoPavadinimas,NPP12,@changed AS ChangedRows
    FROM dbo.Zinynas_tipai WHERE TipoID IN(1170058243,-593367416)
    ORDER BY TipoPavadinimas;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
